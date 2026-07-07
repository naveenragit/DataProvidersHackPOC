using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FinancialServices.Api.Infrastructure.Errors;
using Prism.SeedData.Configuration;
using Prism.SeedData.Corpus;
using Prism.SeedData.Search;

// tools/SeedData — Prism corpus uploader (implementationPlan/03).
//   (default)   seed the live index: create/update schema, embed, upsert (idempotent).
//   --dry-run   offline: load + validate the corpus and print the plan. No Azure calls.
//   --verify    run the acceptance queries against the live index.
// Config binds from the Azure section (Azure__SearchEndpoint, Azure__EmbeddingDeployment, …);
// auth is DefaultAzureCredential (az login) — no keys (P6). Fail loud on missing config (P1).

var mode = ParseMode(args);

// Build a minimal host WITHOUT passing args to the config pipeline (so the flags above are not parsed
// as configuration keys); environment variables and appsettings are still loaded.
var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

var options = builder.Configuration.GetSection("Azure").Get<AzureSeedOptions>() ?? new AzureSeedOptions();

using var host = builder.Build();
var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("SeedData");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    var corpusRoot = CorpusLoader.ResolveDefaultCorpusRoot();
    logger.LogInformation("Loading corpus from {Root} (mode: {Mode}).", corpusRoot, mode);
    var docs = CorpusLoader.LoadFromDirectory(corpusRoot);
    CorpusValidator.Validate(docs);
    logger.LogInformation("Corpus OK: {Count} documents, all integrity + acceptance invariants passed.", docs.Count);

    switch (mode)
    {
        case Mode.DryRun:
            PrintPlan(docs, options, logger);
            return 0;

        case Mode.Verify:
            await new SearchVerifier(options, loggerFactory.CreateLogger<SearchVerifier>())
                .VerifyAsync(docs.Count, cts.Token);
            return 0;

        case Mode.Seed:
        default:
            await new SeedRunner(options, loggerFactory.CreateLogger<SeedRunner>())
                .SeedAsync(docs, cts.Token);
            return 0;
    }
}
catch (CorpusValidationException ex)
{
    logger.LogError("{Message}", ex.Message);
    return 2;
}
catch (ConfigurationException ex)
{
    logger.LogError("Configuration error ({Setting}): {Message}", ex.Setting, ex.Message);
    return 3;
}
catch (OperationCanceledException)
{
    logger.LogWarning("Cancelled.");
    return 130;
}
catch (Exception ex)
{
    logger.LogError(ex, "SeedData failed: {Message}", ex.Message);
    return 1;
}

static Mode ParseMode(string[] args)
{
    if (args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase)))
    {
        return Mode.DryRun;
    }

    if (args.Any(a => string.Equals(a, "--verify", StringComparison.OrdinalIgnoreCase)))
    {
        return Mode.Verify;
    }

    return Mode.Seed;
}

static void PrintPlan(IReadOnlyList<Prism.SeedData.Model.CorpusDoc> docs, AzureSeedOptions options, ILogger logger)
{
    foreach (var group in docs.GroupBy(d => d.DocType).OrderBy(g => g.Key, StringComparer.Ordinal))
    {
        logger.LogInformation("  docType '{DocType}': {Count}", group.Key, group.Count());
    }

    logger.LogInformation(
        "Plan: index '{Index}' at '{Endpoint}', embeddings '{Deployment}' ({Dims} dims) via '{OpenAi}'.",
        options.SearchIndex ?? "<Azure__SearchIndex unset>",
        options.SearchEndpoint ?? "<Azure__SearchEndpoint unset>",
        options.EmbeddingDeployment,
        options.EmbeddingDimensions,
        options.OpenAiEndpoint ?? "<Azure__OpenAiEndpoint unset>");
    logger.LogInformation("Dry run complete — no Azure calls made. Run without --dry-run to seed.");
}

internal enum Mode
{
    Seed,
    DryRun,
    Verify,
}
