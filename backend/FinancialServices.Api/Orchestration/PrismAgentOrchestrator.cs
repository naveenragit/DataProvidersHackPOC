using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Core;
using FinancialServices.Api.Agents;
using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Orchestration;

/// <summary>
/// The pkg-07 <b>live AG-UI</b> orchestrator (spec §A) — exactly ONE AG-UI agent (the landmine
/// workaround: .NET multi-agent workflow streaming over AG-UI is Python-only). The specialist
/// retrievers/narrators (pkg 06) and the deterministic engines (pkg 05) are registered as in-process
/// <see cref="AIFunctionFactory"/> function tools; each tool call streams to CopilotKit as a
/// generative-UI card. <c>confirmScope</c> is an <see cref="ApprovalRequiredAIFunction"/> — the
/// human-in-the-loop scope gate (P5). The model orchestrates the ordering and narrates; the
/// deterministic tools (<c>decomposeDivergence</c>/<c>evaluateRedFlags</c>) return the authoritative
/// numbers and the model never recomputes them (P2). All tool arguments are re-authorized against the
/// corpus (P6); auth is <see cref="DefaultAzureCredential"/> only (no keys, P6); <see cref="CancellationToken"/>
/// is plumbed through every tool (P7). Exposed via <c>MapAGUI("/prism", … .Agent)</c>, gated on
/// <c>Prism:AgUiEnabled</c> so the prerelease hosting package never gates a bare boot.
/// </summary>
public sealed class PrismAgentOrchestrator(
    IOptions<AzureOptions> azureOptions,
    IOptions<PrismOptions> prismOptions,
    TokenCredential credential,
    ISearchCorpus corpus,
    PrismSweepSteps steps,
    DivergenceDecomposer decomposer,
    RedFlagEngine redFlagEngine,
    RedFlagNarratorAgent redFlagNarrator,
    IReconciliationService reconciliation,
    TimeProvider clock,
    ILogger<PrismAgentOrchestrator> logger)
{
    private const string AgentName = "PrismOrchestrator";

    // P4-clean instructions: reconcile + narrate, never advise. The ordering below is what produces the
    // "gate → provider cards → fundamentals → waterfall → red-flag" stream; the numbers come from the tools.
    private const string Instructions =
        "You are Prism's reconciliation orchestrator for corporate-bond credit ratings — a data-quality " +
        "and provenance tool, not an investment tool. Given an issuer and an as-of date, follow this order: " +
        "(1) call confirmScope and WAIT for the user's approval; " +
        "(2) call retrieveProviderRating once for EACH provider in scope; " +
        "(3) call groundFundamentals; " +
        "(4) call decomposeDivergence; " +
        "(5) call evaluateRedFlags; " +
        "(6) call assembleDossier LAST. " +
        "Use ONLY the values the tools return — never invent or recompute a notch, gap, attribution, or " +
        "flag; the deterministic tools return the authoritative numbers. Cite the evidence the tools " +
        "return. Never give investment advice and never use the words buy, sell, hold, recommend, " +
        "allocate, trade, alpha, or signal.";

    private static readonly JsonSerializerOptions Json = PrismJson.Options;

    private readonly object _gate = new();
    private AIAgent? _agent;

    /// <summary>The AG-UI-hosted orchestrator agent (built once, cached). Fails loud if unconfigured (P1).</summary>
    public AIAgent Agent
    {
        get
        {
            if (_agent is not null)
            {
                return _agent;
            }

            lock (_gate)
            {
                _agent ??= BuildAgent();
                return _agent;
            }
        }
    }

    private AIAgent BuildAgent()
    {
        AzureOptions azure = azureOptions.Value;
        if (string.IsNullOrWhiteSpace(azure.OpenAiEndpoint))
        {
            throw new ConfigurationException(
                "Azure:OpenAiEndpoint",
                "Azure OpenAI endpoint is not configured for the AG-UI orchestrator.");
        }

        IChatClient chat = new AzureOpenAIClient(new Uri(azure.OpenAiEndpoint), credential)
            .GetChatClient(prismOptions.Value.Models.Orchestrator)
            .AsIChatClient();

        // confirmScope is the human-in-the-loop gate (P5); the rest are backend function tools.
#pragma warning disable MEAI001 // ApprovalRequiredAIFunction is an evaluation-only Microsoft.Extensions.AI type.
        AITool[] tools =
        [
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create(ConfirmScope)),
            AIFunctionFactory.Create(RetrieveProviderRatingAsync),
            AIFunctionFactory.Create(GroundFundamentalsAsync),
            AIFunctionFactory.Create(DecomposeDivergenceAsync),
            AIFunctionFactory.Create(EvaluateRedFlagsAsync),
            AIFunctionFactory.Create(AssembleDossierAsync),
        ];
#pragma warning restore MEAI001

        logger.LogInformation(
            "AG-UI orchestrator built on model {Model} with {ToolCount} tools", prismOptions.Value.Models.Orchestrator, tools.Length);

        return chat.AsAIAgent(instructions: Instructions, name: AgentName, tools: tools);
    }

    // ── Tools ────────────────────────────────────────────────────────────────────────────────────

    [Description("Confirm the reconciliation scope (issuer, as-of date, and providers) before the sweep begins.")]
    private string ConfirmScope(
        [Description("The issuer id, e.g. 'nordstar'")] string issuerId,
        [Description("The as-of date in yyyy-MM-dd form")] string asOf,
        [Description("The providers in scope, e.g. Moodys, MorningstarDbrs, Msci")] string[]? providers)
    {
        string scoped = providers is { Length: > 0 } ? string.Join(", ", providers) : "all covered providers";
        return string.Create(CultureInfo.InvariantCulture,
            $"Scope confirmed: reconcile {PrismSweepSteps.NormalizeIssuerId(issuerId)} as of {asOf} across {scoped}.");
    }

    [Description("Retrieve one credit provider's rating card for an issuer and explain it with a source citation. Returns JSON.")]
    private async Task<string> RetrieveProviderRatingAsync(
        [Description("The issuer id, e.g. 'nordstar'")] string issuerId,
        [Description("The provider: Moodys, MorningstarDbrs, or Msci")] string provider,
        [Description("The as-of date in yyyy-MM-dd form")] string asOf,
        CancellationToken ct)
    {
        IssuerCorpusEntry entry = await steps.ResolveIssuerAsync(issuerId, ct);
        Provider prov = PrismSweepSteps.ParseProvider(provider);
        DateTimeOffset when = ParseAsOf(asOf);

        var card = await steps.RetrieveProviderRatingAsync(entry.Issuer, prov, when, ct);
        if (card is null)
        {
            return Serialize(new { provider = prov.ToString(), covered = false, message = "No coverage as of this date." });
        }

        return Serialize(new ProviderRatingPayload(
            card.Value.Rating.ToVerdictDto(), card.Value.Explanation.Text, card.Value.Explanation.Citations));
    }

    [Description("State the issuer's ground-truth fundamentals and the as-of filing date from EDGAR. Returns JSON.")]
    private async Task<string> GroundFundamentalsAsync(
        [Description("The issuer id, e.g. 'nordstar'")] string issuerId,
        [Description("The as-of date in yyyy-MM-dd form")] string asOf,
        CancellationToken ct)
    {
        IssuerCorpusEntry entry = await steps.ResolveIssuerAsync(issuerId, ct);
        (FundamentalSnapshot snapshot, FundamentalsSummary summary) = await steps.GroundFundamentalsAsync(entry, ct);
        return Serialize(new FundamentalsPayload(
            entry.Issuer.IssuerId, snapshot.FilingType, snapshot.FilingDate,
            snapshot.DebtToEbitda, snapshot.InterestCoverage, snapshot.CashAndEquivalents,
            summary.Text, summary.Citations));
    }

    [Description("Decompose the notch gap between every provider pair into deterministic attribution buckets. Returns JSON.")]
    private async Task<string> DecomposeDivergenceAsync(
        [Description("The issuer id, e.g. 'nordstar'")] string issuerId,
        [Description("The as-of date in yyyy-MM-dd form")] string asOf,
        CancellationToken ct)
    {
        IssuerCorpusEntry entry = await steps.ResolveIssuerAsync(issuerId, ct);
        ProviderRating[] ordered = await OrderedRatingsAsync(entry.Issuer.IssuerId, ParseAsOf(asOf), ct);
        FundamentalSnapshot latest = LatestOf(entry);

        IReadOnlyList<PairDivergence> divergences = DecomposeAll(ordered, latest);
        return Serialize(new DivergencePayload(divergences.Select(d => d.ToDivergenceDto()).ToArray()));
    }

    [Description("Evaluate the deterministic data-quality red flags (stale input, missing coverage, outliers, methodology conflict) and narrate them. Returns JSON.")]
    private async Task<string> EvaluateRedFlagsAsync(
        [Description("The issuer id, e.g. 'nordstar'")] string issuerId,
        [Description("The as-of date in yyyy-MM-dd form")] string asOf,
        CancellationToken ct)
    {
        IssuerCorpusEntry entry = await steps.ResolveIssuerAsync(issuerId, ct);
        ProviderRating[] ordered = await OrderedRatingsAsync(entry.Issuer.IssuerId, ParseAsOf(asOf), ct);
        FundamentalSnapshot latest = LatestOf(entry);

        IReadOnlyList<PairDivergence> divergences = DecomposeAll(ordered, latest);
        IReadOnlyList<RedFlag> flags = redFlagEngine.Evaluate(entry.Issuer, ordered, latest, divergences);

        var narrated = new List<RedFlagDto>(flags.Count);
        foreach (RedFlag flag in flags)
        {
            string narrative = await redFlagNarrator.NarrateAsync(entry.Issuer, flag, ct);
            narrated.Add((flag with { Narrative = narrative }).ToFlagDto());
        }

        return Serialize(new { flags = narrated });
    }

    [Description("Assemble and persist the full reconciliation dossier for an issuer. Returns JSON with the dossier id.")]
    private async Task<string> AssembleDossierAsync(
        [Description("The issuer id, e.g. 'nordstar'")] string issuerId,
        [Description("The as-of date in yyyy-MM-dd form")] string asOf,
        CancellationToken ct)
    {
        IssuerCorpusEntry entry = await steps.ResolveIssuerAsync(issuerId, ct);
        ReconciliationDossier dossier = await reconciliation.RunAsync(entry.Issuer.IssuerId, ParseAsOf(asOf), ct);
        return Serialize(new DossierReadyPayload(dossier.ToResponse()));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private async Task<ProviderRating[]> OrderedRatingsAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct)
    {
        IReadOnlyList<ProviderRating> ratings = await corpus.GetProviderRatingsAsync(issuerId, asOf, ct);
        return ratings.OrderBy(r => (int)r.Provider).ToArray();
    }

    private static FundamentalSnapshot LatestOf(IssuerCorpusEntry entry) =>
        new(entry.Issuer.IssuerId, entry.LatestFilingDate, entry.FilingType,
            DebtToEbitda: null, InterestCoverage: null, CashAndEquivalents: null);

    // Same pairing + deterministic decomposition the DossierAssembler performs (P2 — the engine is the
    // single source of truth; letter-only ratings have no vintage snapshots so the Input bucket is 0).
    private IReadOnlyList<PairDivergence> DecomposeAll(ProviderRating[] ordered, FundamentalSnapshot latest)
    {
        var divergences = new List<PairDivergence>();
        for (int i = 0; i < ordered.Length; i++)
        {
            for (int j = i + 1; j < ordered.Length; j++)
            {
                divergences.Add(decomposer.Decompose(ordered[i], ordered[j], latest, aInputs: null, bInputs: null));
            }
        }

        return divergences;
    }

    // Clamps a model-supplied as-of to <= now (P6 — never trust the model to time-travel forward).
    private DateTimeOffset ParseAsOf(string? raw)
    {
        DateTimeOffset now = clock.GetUtcNow();
        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
            ? (parsed > now ? now : parsed)
            : now;
    }

    private static string Serialize(object payload) => JsonSerializer.Serialize(payload, Json);
}
