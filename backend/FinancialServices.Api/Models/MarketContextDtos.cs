namespace FinancialServices.Api.Models;

/// <summary>
/// Outcome of a live Morningstar market-context lookup. Every state is a <b>200</b> the panel can render
/// (P1 fail-soft — a provider being off/unreachable/uncovered must never surface as a 5xx and break the
/// reconciliation view). Only genuinely bad input (blank identifier) is a 400 via model validation.
/// </summary>
public enum MarketContextStatus
{
    /// <summary>Live Morningstar research was retrieved.</summary>
    Ok,

    /// <summary>The identifier resolved to nothing in Morningstar's universe (illustrative/private issuer, or a bad ticker).</summary>
    NotCovered,

    /// <summary>The Morningstar live provider is switched off (<c>Prism:Providers:Morningstar:Enabled=false</c>).</summary>
    Disabled,

    /// <summary>The cached OAuth token is missing/expired and cannot refresh headlessly — re-run the discovery CLI login.</summary>
    ReloginRequired,

    /// <summary>Morningstar was enabled but the call failed (network, config, upstream). Details are logged server-side only (P6).</summary>
    Unavailable,
}

/// <summary>The security Morningstar matched an identifier to (from <c>morningstar-id-lookup-tool</c>).</summary>
public sealed record MarketContextInvestment(
    string MorningstarId,
    string Name,
    string? Ticker,
    string? InvestmentType,
    string? Exchange);

/// <summary>
/// One attributed Morningstar analyst-research section (from <c>morningstar-analyst-research-tool</c>).
/// <see cref="Excerpt"/> is truncated server-side; <see cref="Url"/> links to the full report on Morningstar.
/// </summary>
public sealed record MarketContextSection(
    string Title,
    DateTimeOffset? PublishedAt,
    string Excerpt,
    string? Url);

/// <summary>
/// The live-Morningstar context panel payload. This is <b>third-party attributed research shown for
/// context only</b> — it is deliberately kept separate from Prism's own rating reconciliation and never
/// implies a buy/sell/hold view (P4). <see cref="Disclaimer"/> carries the standing attribution/label.
/// </summary>
public sealed record MorningstarContextResponse(
    string Identifier,
    MarketContextStatus Status,
    string Message,
    MarketContextInvestment? Investment,
    IReadOnlyList<MarketContextSection> Sections,
    string SourceProvider,
    DateTimeOffset? RetrievedAt,
    string Disclaimer)
{
    /// <summary>The standing P4-safe attribution/label shown with every live-Morningstar panel.</summary>
    public const string StandardDisclaimer =
        "Third-party Morningstar analyst research, shown for context only — not investment advice, " +
        "and not part of Prism's rating reconciliation.";

    /// <summary>The provider label surfaced to the UI.</summary>
    public const string Provider = "Morningstar";

    /// <summary>Builds a non-Ok response (no live sections) for a given <paramref name="status"/>.</summary>
    public static MorningstarContextResponse ForStatus(string identifier, MarketContextStatus status, string message) =>
        new(identifier, status, message, Investment: null, Sections: [], Provider, RetrievedAt: null, StandardDisclaimer);
}
