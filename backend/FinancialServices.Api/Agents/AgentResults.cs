namespace FinancialServices.Api.Agents;

/// <summary>
/// A grounded, cited explanation of one provider's assessment (pkg 06). <see cref="Citations"/> are the
/// source doc ids the narration is allowed to cite; an empty <see cref="Text"/> means the narration was
/// dropped by <see cref="NarrationGuard"/> (P2) and the caller should fall back to the deterministic value.
/// </summary>
public sealed record ProviderExplanation(string Text, IReadOnlyList<string> Citations);

/// <summary>
/// A grounded, cited summary of an issuer's EDGAR/FRED fundamentals + as-of filing date (pkg 06). An
/// empty <see cref="Text"/> means the narration was dropped (P2) — never a fabricated figure (P1).
/// </summary>
public sealed record FundamentalsSummary(string Text, IReadOnlyList<string> Citations);
