using FinancialServices.Api.Analysis;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Orchestration;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  Progress events for the pkg-07 SSE streaming sweep (spec §D). Each is emitted as an SSE frame
//  `data: { "type": <Type>, "payload": <this record> }` (camelCase via PrismJson). The set mirrors the
//  live AG-UI tool-call cards so the frontend renders one Evidence Stream for both transports. The
//  numbers are the deterministic pkg-05 values / the persisted pkg-08 dossier — the stream is a
//  transport, never a second source of truth (P1/P2). No buy/sell/recommend vocabulary (P4).
// ─────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The stream event type discriminators (stable wire strings, shared with the frontend).</summary>
public static class PrismStreamEventTypes
{
    public const string ScopeConfirm = "scope-confirm";
    public const string AwaitingApproval = "awaiting-approval";
    public const string ProviderRating = "provider-rating";
    public const string Fundamentals = "fundamentals";
    public const string Divergence = "divergence";
    public const string RedFlag = "red-flag";
    public const string DossierReady = "dossier-ready";
    public const string Error = "error";
}

/// <summary>The human-in-the-loop scope gate (P5). <see cref="Approved"/> false = the run is paused.</summary>
public sealed record ScopeConfirmPayload(
    string IssuerId,
    string LegalName,
    DateTimeOffset AsOf,
    IReadOnlyList<Provider> Providers,
    bool Approved,
    string Message);

/// <summary>One provider's verdict card with its (guarded) narration — the retrieveProviderRating step.</summary>
public sealed record ProviderRatingPayload(
    ProviderVerdictDto Verdict,
    string Narrative,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>The issuer's ground-truth fundamentals + as-of filing date — the groundFundamentals step.</summary>
public sealed record FundamentalsPayload(
    string IssuerId,
    string FilingType,
    DateTimeOffset FilingDate,
    decimal? DebtToEbitda,
    decimal? InterestCoverage,
    decimal? CashAndEquivalents,
    string Narrative,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>The deterministic decomposition of every provider pair — the decomposeDivergence step (P2).</summary>
public sealed record DivergencePayload(IReadOnlyList<PairDivergenceDto> Divergences);

/// <summary>One deterministic red flag with its verbatim rule text — the evaluateRedFlags step (P2).</summary>
public sealed record RedFlagPayload(RedFlagDto Flag);

/// <summary>The assembled + persisted dossier — identical to the REST endpoint's object (acceptance #4).</summary>
public sealed record DossierReadyPayload(DossierResponse Dossier);

/// <summary>A stream-level error, surfaced in-band so the client can render it (arch-03 code + message).</summary>
public sealed record StreamErrorPayload(string Code, string Message);
