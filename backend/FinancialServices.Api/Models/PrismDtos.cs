using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FinancialServices.Api.Analysis;

namespace FinancialServices.Api.Models;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  Wire DTOs — the REST contract. SEPARATE from the domain records in PrismModels.cs (arch-09): the
//  API never binds a domain record straight from the wire, and responses expose only what the UI
//  needs (no Cosmos/Search internals). Field names + shapes mirror frontend/src/types/prism.ts
//  EXACTLY (camelCase via PrismJson; Provider serializes as its member name, e.g. "Msci").
//  P4: no buy/sell/recommend vocabulary anywhere in these names.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>A corporate-bond issuer in the reconciliation cast (list/detail projection).</summary>
public sealed record IssuerListItem(
    string IssuerId,
    string LegalName,
    string Ticker,
    string Cik,
    string Sector,
    string SampleBondIsin,
    IReadOnlyList<Provider> Coverage);

/// <summary>One provider's verdict for an issuer, with the as-of dates behind it.</summary>
public sealed record ProviderVerdictDto(
    Provider Provider,
    string Letter,
    int Notch,
    DateTimeOffset AsOfDate,
    DateTimeOffset InputAsOfDate,
    string MethodologyDocId);

/// <summary>One attribution bucket's signed contribution to a provider pair's notch gap.</summary>
public sealed record BucketAttributionDto(
    string Bucket,
    decimal Notches,
    string Explanation,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>The notch gap between two providers, decomposed into attribution buckets.</summary>
public sealed record PairDivergenceDto(
    Provider A,
    Provider B,
    int NotchGap,
    IReadOnlyList<BucketAttributionDto> Attribution);

/// <summary>A deterministic red flag with its verbatim rule text and supporting evidence.</summary>
public sealed record RedFlagDto(
    string Code,
    string Severity,
    string Rule,
    string Narrative,
    IReadOnlyList<string> EvidenceRefs);

/// <summary>The full reconciliation result for one issuer (response body).</summary>
public sealed record DossierResponse(
    string Id,
    string IssuerId,
    DateTimeOffset AsOf,
    IReadOnlyList<ProviderVerdictDto> Verdicts,
    IReadOnlyList<PairDivergenceDto> Divergences,
    IReadOnlyList<RedFlagDto> Flags,
    string ConsensusSummary,
    double Confidence);

/// <summary>
/// Request body for <c>POST /api/v1/reconciliations</c>. Validated with DataAnnotations; unknown
/// fields are rejected by the serializer (<c>UnmappedMemberHandling.Disallow</c>). <see cref="AsOf"/>
/// is nullable + <c>[Required]</c> so an omitted value fails loudly (P1) instead of defaulting to
/// year 1.
/// </summary>
public sealed record ReconciliationRequest(
    [Required] string? IssuerId,
    [Required] DateTimeOffset? AsOf,
    IReadOnlyList<Provider>? Providers);

/// <summary>
/// Request body for <c>POST /api/v1/reconciliations/stream</c> (the pkg-07 SSE streaming sweep).
/// <see cref="Confirmed"/> is the human-in-the-loop scope gate (P5): the sweep proceeds past the
/// <c>scope-confirm</c> event only when the client has confirmed the scope (default <c>false</c>
/// emits the gate and stops). Tool args are re-authorized server-side (P6).
/// </summary>
public sealed record ReconciliationStreamRequest(
    [Required] string? IssuerId,
    [Required] DateTimeOffset? AsOf,
    bool Confirmed,
    IReadOnlyList<Provider>? Providers);

// ── Standard error envelope (arch-03) — one shape for every non-2xx response. ────────────────────

/// <summary>The error payload: a stable UPPER_SNAKE <c>code</c>, human <c>message</c>, safe <c>details</c>.</summary>
public sealed record ApiError(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Details);

/// <summary>The <c>{ "error": { … } }</c> envelope wrapper.</summary>
public sealed record ApiErrorBody(ApiError Error);
