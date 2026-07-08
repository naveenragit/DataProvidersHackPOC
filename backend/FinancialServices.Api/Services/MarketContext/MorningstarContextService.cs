using FinancialServices.Api.Connectors.Mcp;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Authentication;

namespace FinancialServices.Api.Services.MarketContext;

/// <summary>
/// Live Morningstar context enrichment over the SSRF-guarded MCP seam. At runtime it reuses the OAuth
/// token the discovery-CLI login cached (headless refresh via <see cref="ProviderOAuth.HeadlessRedirect"/>
/// — it never opens a browser); a missing/expired token surfaces as <see cref="MarketContextStatus.ReloginRequired"/>.
/// Two calls per lookup: <c>morningstar-id-lookup-tool</c> (ticker/ISIN/name → Morningstar id) then
/// <c>morningstar-analyst-research-tool</c>. Every failure mode is caught and mapped to a status (P1
/// fail-soft — the reconciliation view must never break because a context provider is down). Logs ids +
/// counts only, never tool text/args (P6). Stays inert for the default demo because Morningstar defaults
/// <c>Enabled=false</c> (planning §11 — synthetic remains the default).
/// </summary>
public sealed class MorningstarContextService(
    IOptions<PrismOptions> options,
    McpToolSessionFactory factory,
    ILoggerFactory loggerFactory,
    ILogger<MorningstarContextService> logger,
    TimeProvider timeProvider) : IMorningstarContextService
{
    private const ProviderMcpKey Provider = ProviderMcpKey.Morningstar;
    private const string IdLookupTool = "morningstar-id-lookup-tool";
    private const string AnalystResearchTool = "morningstar-analyst-research-tool";
    private const int MaxSections = 4;
    private const int MaxExcerptChars = 900;

    // Headless: HeadlessRedirect throws instantly if interactive auth is needed, so this only bounds the
    // token refresh + the two tool calls. Generous enough for a cold connect, short enough to fail fast.
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromSeconds(30);

    private readonly PrismOptions _options = options.Value;

    public async Task<MorningstarContextResponse> GetResearchAsync(string identifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        identifier = identifier.Trim();

        var providerOptions = _options.Providers.Morningstar;
        if (!providerOptions.Enabled)
        {
            return MorningstarContextResponse.ForStatus(
                identifier,
                MarketContextStatus.Disabled,
                "Morningstar live enrichment is off. Enable it and complete the one-time discovery login to use it.");
        }

        try
        {
            var oauthOptions = BuildOAuthOptions(providerOptions);
            await using var session = await factory
                .ConnectAsync(Provider, providerOptions.McpUrl, oauthOptions, cancellationToken, InitializationTimeout)
                .ConfigureAwait(false);

            var lookup = await session.CallToolAsync(
                IdLookupTool,
                new Dictionary<string, object?> { ["investment_identifiers"] = new[] { identifier } },
                cancellationToken).ConfigureAwait(false);

            var investment = MorningstarResponseParser.ParseInvestment(lookup.StructuredJson);
            if (investment is null)
            {
                logger.LogInformation("Morningstar has no coverage for identifier {Identifier}.", identifier);
                return MorningstarContextResponse.ForStatus(
                    identifier,
                    MarketContextStatus.NotCovered,
                    $"Morningstar has no listed-security coverage for '{identifier}'. It covers stocks, ETFs, and funds — illustrative or private issuers won't resolve.");
            }

            var research = await session.CallToolAsync(
                AnalystResearchTool,
                new Dictionary<string, object?> { ["investment_id"] = investment.MorningstarId },
                cancellationToken).ConfigureAwait(false);

            var sections = MorningstarResponseParser.ParseSections(research.StructuredJson, MaxSections, MaxExcerptChars);
            logger.LogInformation(
                "Morningstar context for {Identifier} resolved to {MorningstarId} with {SectionCount} research section(s).",
                identifier,
                investment.MorningstarId,
                sections.Count);

            var message = sections.Count > 0
                ? $"Live Morningstar analyst research for {investment.Name}."
                : $"{investment.Name} is covered by Morningstar, but no analyst research report is currently available.";

            return new MorningstarContextResponse(
                identifier,
                MarketContextStatus.Ok,
                message,
                investment,
                sections,
                MorningstarContextResponse.Provider,
                timeProvider.GetUtcNow(),
                MorningstarContextResponse.StandardDisclaimer);
        }
        catch (ReloginRequiredException ex)
        {
            logger.LogWarning("Morningstar re-login required: {Message}", ex.Message);
            return MorningstarContextResponse.ForStatus(
                identifier,
                MarketContextStatus.ReloginRequired,
                "Morningstar sign-in has expired. Re-run the provider-discovery login, then try again.");
        }
        catch (ConfigurationException ex)
        {
            logger.LogWarning("Morningstar context misconfigured ({Setting}): {Message}", ex.Setting, ex.Message);
            return MorningstarContextResponse.ForStatus(
                identifier,
                MarketContextStatus.Unavailable,
                "Morningstar live enrichment is not configured correctly. Check the provider settings.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Log type + message only — never the tool text/args, which carry licensed data (P6).
            logger.LogError("Morningstar context lookup failed: {Type}: {Message}", ex.GetType().Name, ex.Message);
            return MorningstarContextResponse.ForStatus(
                identifier,
                MarketContextStatus.Unavailable,
                "Morningstar live enrichment is temporarily unavailable.");
        }
    }

    private ClientOAuthOptions BuildOAuthOptions(PrismOptions.ProviderMcpOptions providerOptions)
    {
        var repoRoot = ResolveRepoRoot();

        var tokenCache = new FileTokenCache(
            FileTokenCache.DefaultPathFor(Provider, repoRoot),
            Provider.ToString(),
            loggerFactory.CreateLogger<FileTokenCache>());

        var clientStore = new DcrClientStore(
            DcrClientStore.DefaultPathFor(Provider, repoRoot),
            Provider.ToString(),
            loggerFactory.CreateLogger<DcrClientStore>());

        return ProviderOAuth.BuildOptions(
            Provider,
            providerOptions,
            tokenCache,
            ProviderOAuth.HeadlessRedirect(Provider),
            clientStore.Load(),
            clientStore.OnRegisteredAsync);
    }

    // The runtime host runs from bin/; the token cache lives at the repo-root .prism/tokens (written by
    // the discovery CLI). Walk up for a repo marker so both processes share the same cache location.
    private static string ResolveRepoRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                    File.Exists(Path.Combine(directory.FullName, "backend", "FinancialServices.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }
}
