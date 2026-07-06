using FinancialServices.Api.Models;
using Microsoft.Extensions.Logging;

namespace FinancialServices.Api.Analysis;

/// <summary>
/// One raw XBRL fact straight from EDGAR companyfacts — <b>no derivation</b>. Carries the fields the
/// pure <see cref="FundamentalsCalculator"/> needs to select a coherent period: the reporting
/// <see cref="Start"/>/<see cref="End"/> window, the <see cref="Filed"/> date (drives as-of), SEC's
/// computed <see cref="Frame"/> (e.g. <c>CY2025</c>), the accession <see cref="Accn"/> (pins co-filed
/// concepts together) and the <see cref="Form"/> (10-K/10-Q/8-K).
/// </summary>
public sealed record XbrlFact(
    decimal? Value,
    DateTimeOffset? Start,
    DateTimeOffset End,
    DateTimeOffset Filed,
    string? Frame,
    string? Accn,
    string? Form);

/// <summary>
/// The <b>raw</b>, I/O-only output of the EDGAR connector (P2 — no business logic in the connector):
/// the issuer's latest filing (from the submissions API, as-of filtered) plus the unfiltered us-gaap
/// concept facts. The deterministic <see cref="FundamentalsCalculator"/> turns this into a
/// <see cref="FundamentalSnapshot"/>. <see cref="LatestFilingDate"/> is the stale-input rule's ground
/// truth and is deliberately independent of the fundamentals' own coherent filing.
/// </summary>
public sealed record EdgarCompanyFacts(
    string Cik,
    DateTimeOffset? LatestFilingDate,
    string? LatestFilingType,
    IReadOnlyDictionary<string, IReadOnlyList<XbrlFact>> UsGaap);

/// <summary>
/// Pure, deterministic issuer-fundamentals engine — core principle P2 (plain C#, no I/O, no LLM, no
/// network). Turns raw <see cref="EdgarCompanyFacts"/> + a decision date into a
/// <see cref="FundamentalSnapshot"/>, applying the as-of and coherent-period rules so a Debt/EBITDA
/// or coverage figure is never assembled from mismatched reporting windows.
/// </summary>
/// <remarks>
/// Selection semantics:
/// <list type="bullet">
/// <item><b>Flow concepts</b> (operating income, D&amp;A, interest): only a coherent
/// <b>annual / 12-month</b> fact is used — SEC's <c>CY{fy}</c> frame or a duration of 365d ± 20d.
/// Operating income and D&amp;A are taken from the <b>same</b> accession/period, never independently.
/// When only quarterly facts exist, the figure is <c>null</c> rather than mixing durations
/// (TTM-from-four-quarters is a future refinement).</item>
/// <item><b>Instant concepts</b> (debt, cash): the value as of the latest filing on or before the
/// decision date.</item>
/// <item><b>Period selection</b>: newest period <c>end ≤ asOf</c> first, then the latest
/// <c>filed ≤ asOf</c> within that period — so a restated old period cannot beat a newer one.</item>
/// <item><b>Debt fallback chain</b>: <c>LongTermDebtNoncurrent</c> (+ current portion) →
/// <c>LongTermDebt</c> → <c>LongTermDebtAndCapitalLeaseObligations</c> → <c>Liabilities</c>; absent →
/// <c>null</c> (never <c>0</c>).</item>
/// </list>
/// </remarks>
public static class FundamentalsCalculator
{
    // us-gaap concept vocabulary (order within each array = preference). Owned here in Analysis, not in
    // the connector, so the connector stays concept-agnostic I/O (P2). Debt uses a fallback chain.
    private static readonly string[] DebtNoncurrentConcepts = { "LongTermDebtNoncurrent" };
    // STK-R5: current portion of LONG-TERM debt only (mirrors the noncurrent chain). The broad
    // `DebtCurrent` was dropped so short-term operating borrowings (revolver, commercial paper) are
    // not over-scoped into interest-bearing TotalDebt.
    private static readonly string[] DebtCurrentConcepts =
        { "LongTermDebtCurrent", "LongTermDebtAndCapitalLeaseObligationsCurrent" };
    private static readonly string[] DebtFallbackConcepts =
        { "LongTermDebt", "LongTermDebtAndCapitalLeaseObligations", "Liabilities" };
    private static readonly string[] OperatingIncomeConcepts = { "OperatingIncomeLoss" };
    private static readonly string[] DepreciationConcepts =
        { "DepreciationDepletionAndAmortization", "DepreciationAndAmortization" };
    private static readonly string[] InterestExpenseConcepts =
        { "InterestExpense", "InterestExpenseNonoperating" };
    private static readonly string[] CashConcepts = { "CashAndCashEquivalentsAtCarryingValue" };

    /// <summary>
    /// Every us-gaap concept this engine can consume. The EDGAR connector extracts exactly these from
    /// companyfacts, keeping the concept vocabulary in <c>Analysis/</c> and out of the I/O layer (P2).
    /// </summary>
    public static readonly IReadOnlySet<string> RelevantConcepts =
        new HashSet<string>(
            DebtNoncurrentConcepts
                .Concat(DebtCurrentConcepts)
                .Concat(DebtFallbackConcepts)
                .Concat(OperatingIncomeConcepts)
                .Concat(DepreciationConcepts)
                .Concat(InterestExpenseConcepts)
                .Concat(CashConcepts),
            StringComparer.Ordinal);

    /// <summary>
    /// Derive a point-in-time <see cref="FundamentalSnapshot"/> for <paramref name="issuerId"/> from
    /// raw <paramref name="facts"/> as of <paramref name="asOf"/>. The caller owns the issuer join and
    /// supplies <paramref name="issuerId"/> — a CIK-only fetch cannot honestly invent one (P1). Ratios
    /// are <c>null</c> whenever an input is genuinely absent or a denominator is zero (no fabrication).
    /// </summary>
    public static FundamentalSnapshot Derive(
        string issuerId, EdgarCompanyFacts facts, DateTimeOffset asOf, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);
        ArgumentNullException.ThrowIfNull(facts);

        var debt = SelectDebt(facts, asOf, logger);
        var ebitda = SelectEbitda(facts, asOf);
        var interest = SelectInterest(facts, asOf, ebitda.Anchor);
        var cash = SelectInstant(facts, CashConcepts, asOf);

        var debtToEbitda = debt.Value is { } d && ebitda.Value is { } e && e != 0m
            ? d / e
            : (decimal?)null;
        var interestCoverage = ebitda.Value is { } cov && interest.Value is { } i && i != 0m
            ? cov / i
            : (decimal?)null;

        // The fundamentals' OWN filing vintage = the newest filing among the facts actually used. It
        // is deliberately separate from the issuer's LatestFilingDate (submissions API), which is the
        // stale-input rule's ground truth (P3): a later 8-K can move LatestFilingDate without changing
        // the coherent leverage filing that Debt/EBITDA/coverage were pinned to.
        var usedFilings = new[] { debt.Filing, ebitda.Filing, interest.Filing, cash.Filing }
            .Where(f => f is not null)
            .Select(f => f!)
            .ToList();
        var latestUsed = usedFilings.Count > 0 ? usedFilings.MaxBy(f => f.Filed) : null;
        var filingDate = latestUsed?.Filed ?? facts.LatestFilingDate ?? default;
        var filingType = latestUsed?.Form ?? facts.LatestFilingType ?? string.Empty;

        return new FundamentalSnapshot(
            IssuerId: issuerId,
            FilingDate: filingDate,
            FilingType: filingType,
            DebtToEbitda: debtToEbitda,
            InterestCoverage: interestCoverage,
            CashAndEquivalents: cash.Value);
    }

    // TotalDebt = interest-bearing long-term debt (+ current portion when present); fallback chain
    // then a genuine null. Never fabricates from unrelated line items or defaults to 0 (P1).
    private static Pick SelectDebt(EdgarCompanyFacts facts, DateTimeOffset asOf, ILogger? logger)
    {
        var noncurrent = SelectInstant(facts, DebtNoncurrentConcepts, asOf);
        if (noncurrent.Value is not null)
        {
            logger?.LogDebug("EDGAR debt matched concept {Concept}.", noncurrent.Concept);
            var current = SelectInstant(facts, DebtCurrentConcepts, asOf);
            // STK-R1: only fold in the current portion when it shares the chosen noncurrent debt fact's
            // balance-sheet date (same End). The two legs are selected by independent SelectInstant
            // calls and can otherwise land on different reporting dates — summing a stale current
            // portion onto a newer noncurrent balance would mix vintages into one TotalDebt.
            var currentSameDate = current.Filing is not null
                && noncurrent.Filing is not null
                && current.Filing.End == noncurrent.Filing.End;
            var total = noncurrent.Value.Value + (currentSameDate ? (current.Value ?? 0m) : 0m);
            var filing = currentSameDate
                         && current.Filing is not null && noncurrent.Filing is not null
                         && current.Filing.Filed > noncurrent.Filing.Filed
                ? current.Filing
                : noncurrent.Filing;
            return new Pick(total, filing, noncurrent.Concept);
        }

        var fallback = SelectInstant(facts, DebtFallbackConcepts, asOf);
        if (fallback.Value is not null)
        {
            logger?.LogDebug("EDGAR debt matched fallback concept {Concept}.", fallback.Concept);
            return fallback;
        }

        logger?.LogWarning(
            "EDGAR debt: no matching concept on or before {AsOf:yyyy-MM-dd}; TotalDebt is null.", asOf);
        return Pick.None;
    }

    // EBITDA has no single us-gaap tag: operating income + D&A from the SAME annual accession/period
    // (never independently). Returns the OI fact as the coherence anchor for interest coverage.
    private static (decimal? Value, XbrlFact? Filing, XbrlFact? Anchor) SelectEbitda(
        EdgarCompanyFacts facts, DateTimeOffset asOf)
    {
        var operating = BestAnnual(facts, OperatingIncomeConcepts, asOf);
        if (operating?.Value is null)
        {
            return (null, null, null);
        }

        var depreciation = FindSameAccnAnnual(facts, DepreciationConcepts, operating);
        var ebitda = operating.Value.Value + (depreciation?.Value ?? 0m); // operating-income based (EBIT when D&A absent).
        var filing = depreciation is not null && depreciation.Filed > operating.Filed ? depreciation : operating;
        return (ebitda, filing, operating);
    }

    // Interest coverage requires EBITDA; take interest from the SAME annual period (end) as EBITDA so
    // the ratio is coherent, else null (never a lone quarter or a mismatched window).
    private static Pick SelectInterest(EdgarCompanyFacts facts, DateTimeOffset asOf, XbrlFact? ebitdaAnchor)
    {
        if (ebitdaAnchor is null)
        {
            return Pick.None;
        }

        foreach (var concept in InterestExpenseConcepts)
        {
            if (!facts.UsGaap.TryGetValue(concept, out var list))
            {
                continue;
            }

            var match = list
                .Where(f => IsAnnual(f) && f.End == ebitdaAnchor.End && f.Filed <= asOf && f.Value.HasValue)
                .MaxBy(f => f.Filed);
            if (match is not null)
            {
                return new Pick(match.Value, match, concept);
            }
        }

        return Pick.None;
    }

    // Instant concept (debt, cash): newest period end ≤ asOf FIRST, then the latest filing ≤ asOf
    // within that period — so a restated old period cannot beat a newer period (P3).
    private static Pick SelectInstant(EdgarCompanyFacts facts, string[] concepts, DateTimeOffset asOf)
    {
        foreach (var concept in concepts)
        {
            if (!facts.UsGaap.TryGetValue(concept, out var list))
            {
                continue;
            }

            var eligible = list.Where(f => f.End <= asOf && f.Filed <= asOf && f.Value.HasValue).ToList();
            if (eligible.Count == 0)
            {
                continue;
            }

            var maxEnd = eligible.Max(f => f.End);
            var pick = eligible.Where(f => f.End == maxEnd).MaxBy(f => f.Filed);
            if (pick is not null)
            {
                return new Pick(pick.Value, pick, concept);
            }
        }

        return Pick.None;
    }

    // The best coherent annual (12-month) fact ≤ asOf: newest period end first, then latest filing.
    private static XbrlFact? BestAnnual(EdgarCompanyFacts facts, string[] concepts, DateTimeOffset asOf)
    {
        foreach (var concept in concepts)
        {
            if (!facts.UsGaap.TryGetValue(concept, out var list))
            {
                continue;
            }

            var eligible = list
                .Where(f => IsAnnual(f) && f.End <= asOf && f.Filed <= asOf && f.Value.HasValue)
                .ToList();
            if (eligible.Count == 0)
            {
                continue;
            }

            var maxEnd = eligible.Max(f => f.End);
            var pick = eligible.Where(f => f.End == maxEnd).MaxBy(f => f.Filed);
            if (pick is not null)
            {
                return pick;
            }
        }

        return null;
    }

    // D&A must share the anchor's period end AND accession so EBITDA is assembled from one filing.
    private static XbrlFact? FindSameAccnAnnual(EdgarCompanyFacts facts, string[] concepts, XbrlFact anchor)
    {
        if (anchor.Accn is null)
        {
            return null;
        }

        foreach (var concept in concepts)
        {
            if (!facts.UsGaap.TryGetValue(concept, out var list))
            {
                continue;
            }

            var match = list.FirstOrDefault(f =>
                f.End == anchor.End
                && IsAnnual(f) // STK-R2: D&A must be the 12-month fact, not a 3-month leg co-filed in the 10-K.
                && f.Value.HasValue
                && string.Equals(f.Accn, anchor.Accn, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    // Annual if SEC's calendar-year frame (e.g. "CY2025") OR a ~12-month reporting duration (365d±20d).
    private static bool IsAnnual(XbrlFact fact)
    {
        if (fact.Frame is { } frame && IsCalendarYearFrame(frame))
        {
            return true;
        }

        if (fact.Start is { } start)
        {
            var days = (fact.End - start).TotalDays;
            return days is >= 345 and <= 385;
        }

        return false;
    }

    // "CY" + 4 digits, no quarter/instant suffix ("CY2025Q4"/"CY2025Q4I" are quarterly, not annual).
    private static bool IsCalendarYearFrame(string frame)
    {
        if (frame.Length != 6 || !frame.StartsWith("CY", StringComparison.Ordinal))
        {
            return false;
        }

        for (var i = 2; i < 6; i++)
        {
            if (!char.IsAsciiDigit(frame[i]))
            {
                return false;
            }
        }

        return true;
    }

    private readonly record struct Pick(decimal? Value, XbrlFact? Filing, string? Concept)
    {
        public static Pick None => new(null, null, null);
    }
}
