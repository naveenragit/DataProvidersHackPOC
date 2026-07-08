using System.Text.Json;
using FinancialServices.Api.Connectors.Mcp;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prism.ProviderDiscovery;

// tools/ProviderDiscovery — Phase-0 live-provider MCP discovery CLI (implementationPlan/13, Round 1).
//   --provider morningstar|moodys   (required) which hosted MCP server to discover
//   --call <toolName>               (optional) also run one sample tools/call
//   --args <json>                   (optional) JSON object of arguments for --call
//   --timeout <minutes>             (optional) login wait (default 5)
//   --out <dir>                     (optional) findings-note directory (default .prism/discovery/<date>, git-ignored)
// Reads Prism__Providers__<Name>__* from the environment (load .env first, e.g. via
// run-provider-discovery.bat). Auth is a ONE-TIME interactive browser login; the refresh token is
// stored in a git-ignored FileTokenCache. A ClientId is optional — when blank, the SDK registers a
// client dynamically (RFC 7591) and it is persisted for reuse. Fails loud (P1) when the provider is
// disabled. Exit codes: 0 success · 1 fault · 2 usage error.

var parsed = CliArgs.TryParse(args, out var cli, out var usageError);
if (!parsed)
{
    Console.Error.WriteLine(usageError);
    Console.Error.WriteLine(CliArgs.Usage);
    return 2;
}

var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

var prism = builder.Configuration.GetSection("Prism").Get<PrismOptions>() ?? new PrismOptions();
using var host = builder.Build();
var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("ProviderDiscovery");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var providerOptions = cli.Provider == ProviderMcpKey.Morningstar ? prism.Providers.Morningstar : prism.Providers.Moodys;

// Fail loud (P1) when the operator has not supplied credentials — this is the Phase-0 creds gate.
if (!providerOptions.Enabled)
{
    logger.LogError(
        "{Provider} is not enabled. Set Prism__Providers__{Provider}__Enabled=true (and ClientId/ClientSecret) in your .env, then re-run.",
        cli.Provider,
        cli.Provider);
    return 1;
}

if (string.IsNullOrWhiteSpace(providerOptions.ClientId))
{
    logger.LogInformation(
        "{Provider} has no OAuth ClientId configured — the SDK will register a client dynamically (RFC 7591) and persist it for reuse.",
        cli.Provider);
}

try
{
    var repoRoot = RepoLayout.ResolveRoot();
    var tokenPath = FileTokenCache.DefaultPathFor(cli.Provider, repoRoot);
    var tokenCache = new FileTokenCache(tokenPath, cli.Provider.ToString(), loggerFactory.CreateLogger<FileTokenCache>());

    // Persist / reuse the client_id the SDK mints via dynamic registration — the cached refresh token is
    // bound to it, so a re-run must present the same client (else the refresh fails and it re-registers).
    var clientStore = new DcrClientStore(
        DcrClientStore.DefaultPathFor(cli.Provider, repoRoot),
        cli.Provider.ToString(),
        loggerFactory.CreateLogger<DcrClientStore>());
    var persistedClient = clientStore.Load();

    var redirect = LoopbackAuthorizationHandler.CreateDelegate(
        loggerFactory.CreateLogger("OAuthLoopback"),
        TimeSpan.FromMinutes(cli.TimeoutMinutes));
    var oauthOptions = ProviderOAuth.BuildOptions(
        cli.Provider, providerOptions, tokenCache, redirect, persistedClient, clientStore.OnRegisteredAsync);

    logger.LogInformation(
        "Connecting to {Provider} MCP at {Url} (a browser will open for a one-time sign-in) …",
        cli.Provider,
        providerOptions.McpUrl);

    // The SDK's InitializationTimeout bounds the ENTIRE connect, including the interactive login, so it
    // must exceed the human sign-in budget (+1 min for the post-callback token exchange + handshake);
    // otherwise the SDK cancels the browser login at its 60s default.
    var initializationTimeout = TimeSpan.FromMinutes(cli.TimeoutMinutes) + TimeSpan.FromMinutes(1);
    var factory = new McpToolSessionFactory(loggerFactory);
    await using var session = await factory.ConnectAsync(
        cli.Provider, providerOptions.McpUrl, oauthOptions, cts.Token, initializationTimeout);

    var tools = await session.ListToolsAsync(cts.Token);
    logger.LogInformation("tools/list returned {Count} tool(s):", tools.Count);
    foreach (var tool in tools)
    {
        logger.LogInformation("  • {Name}{Title}", tool.Name, string.IsNullOrWhiteSpace(tool.Title) ? "" : $" — {tool.Title}");
    }

    (string Tool, McpToolCallResult Result)? sample = null;
    if (!string.IsNullOrWhiteSpace(cli.CallTool))
    {
        var callArgs = ParseCallArgs(cli.CallArgsJson);
        logger.LogInformation("Running sample tools/call {Tool} …", cli.CallTool);
        var result = await session.CallToolAsync(cli.CallTool!, callArgs, cts.Token);
        sample = (cli.CallTool!, result);
    }

    // Default under the git-ignored .prism/ (never .copilot-tracking/, which is committed) — a --call
    // sample can carry licensed provider data, so the findings note must not be committable (SEC-13R1-01).
    var outDir = cli.OutputDirectory ?? Path.Combine(
        repoRoot, ".prism", "discovery", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"));
    var notePath = await DiscoveryReportWriter.WriteAsync(
        outDir, cli.Provider.ToString(), providerOptions.McpUrl!, tools, sample, cts.Token);

    logger.LogInformation("Findings written to {Path}", notePath);
    logger.LogInformation("Phase-0 discovery complete for {Provider}. Review the note for the go/no-go.", cli.Provider);
    return 0;
}
catch (ReloginRequiredException ex)
{
    logger.LogError("Re-login required for {Provider}: {Message}", ex.Provider, ex.Message);
    return 1;
}
catch (ConfigurationException ex)
{
    logger.LogError("Configuration error ({Setting}): {Message}", ex.Setting, ex.Message);
    return 1;
}
catch (OperationCanceledException)
{
    logger.LogWarning("Discovery cancelled.");
    return 1;
}
catch (Exception ex)
{
    // Log the type + message only (never a token/secret fragment — P6).
    logger.LogError("Discovery failed: {Type}: {Message}", ex.GetType().Name, ex.Message);
    return 1;
}

static IReadOnlyDictionary<string, object?> ParseCallArgs(string? json)
{
    if (string.IsNullOrWhiteSpace(json))
    {
        return new Dictionary<string, object?>();
    }

    var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
    return parsed ?? new Dictionary<string, object?>();
}
