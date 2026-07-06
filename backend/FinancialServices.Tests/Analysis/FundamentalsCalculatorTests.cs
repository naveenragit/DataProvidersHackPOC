using FinancialServices.Api.Analysis;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinancialServices.Tests.Analysis;

/// <summary>
/// Pure fundamentals-derivation coverage (P2). Fixtures are built as raw <see cref="XbrlFact"/>s so the
/// coherent-period rules are tested in isolation from EDGAR I/O: annual-only flow selection,
/// same-accession EBITDA, newest-period-then-latest-filing selection, the debt fallback chain, instant
/// as-of, and honest nulls.
/// </summary>
public sealed class FundamentalsCalculatorTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset D(int y, int m, int d) => new(y, m, d, 0, 0, 0, TimeSpan.Zero);

    private static XbrlFact Annual(decimal value, int fy, DateTimeOffset filed, string accn, string form = "10-K") =>
        new(value, D(fy, 1, 1), D(fy, 12, 31), filed, $"CY{fy}", accn, form);

    private static XbrlFact Quarter(decimal value, int fy, int q, DateTimeOffset filed, string accn) =>
        new(value, D(fy, ((q - 1) * 3) + 1, 1), D(fy, q * 3, q is 1 or 4 ? 31 : 30), filed, $"CY{fy}Q{q}", accn, "10-Q");

    private static XbrlFact Instant(
        decimal value, DateTimeOffset end, DateTimeOffset filed, string accn, string form = "10-Q") =>
        new(value, null, end, filed, null, accn, form);

    // STK-R4: the issuer's LatestFilingDate/Type (submissions API) is an 8-K on 2026-05-25 —
    // deliberately LATER and a different form than any 10-Q whose figures the fundamentals use
    // (filed 2026-05-20 in these fixtures). Breaking the degenerate date collision lets the
    // provenance test catch a regression that stamps the snapshot with facts.LatestFilingDate.
    private static EdgarCompanyFacts Facts(params (string Concept, XbrlFact[] Facts)[] concepts)
    {
        var dict = concepts.ToDictionary(
            c => c.Concept, c => (IReadOnlyList<XbrlFact>)c.Facts, StringComparer.Ordinal);
        return new EdgarCompanyFacts("0001234567", D(2026, 5, 25), "8-K", dict);
    }

    [Fact] // Coherent annual EBITDA (OI 80 + D&A 20) + instant debt 500 + cash 150 → ratios.
    public void Derive_AnnualCoherent_ComputesRatios()
    {
        var facts = Facts(
            ("LongTermDebtNoncurrent", new[] { Instant(500m, D(2026, 3, 31), D(2026, 5, 20), "a-2") }),
            ("OperatingIncomeLoss", new[] { Annual(80m, 2025, D(2026, 2, 15), "fy25") }),
            ("DepreciationDepletionAndAmortization", new[] { Annual(20m, 2025, D(2026, 2, 15), "fy25") }),
            ("InterestExpense", new[] { Annual(10m, 2025, D(2026, 2, 15), "fy25") }),
            ("CashAndCashEquivalentsAtCarryingValue", new[] { Instant(150m, D(2026, 3, 31), D(2026, 5, 20), "a-2") }));

        var snapshot = FundamentalsCalculator.Derive("nordstar", facts, AsOf, NullLogger.Instance);

        snapshot.IssuerId.Should().Be("nordstar");
        snapshot.DebtToEbitda.Should().Be(5m);      // 500 / (80 + 20)
        snapshot.InterestCoverage.Should().Be(10m); // 100 / 10
        snapshot.CashAndEquivalents.Should().Be(150m);
    }

    [Fact] // Flow selection ignores a same-end quarter and uses the 12-month annual fact.
    public void Derive_AnnualAndQuarterShareEnd_PicksAnnual()
    {
        var facts = Facts(
            ("LongTermDebtNoncurrent", new[] { Instant(500m, D(2025, 12, 31), D(2026, 2, 15), "fy25") }),
            ("OperatingIncomeLoss", new[]
            {
                Quarter(25m, 2025, 4, D(2026, 2, 15), "q4-25"),   // 3-month, same end 2025-12-31
                Annual(80m, 2025, D(2026, 2, 15), "fy25"),        // 12-month
            }));

        var snapshot = FundamentalsCalculator.Derive("nordstar", facts, AsOf, NullLogger.Instance);

        snapshot.DebtToEbitda.Should().Be(500m / 80m); // annual OI (80), not the quarter (25)
    }

    [Fact] // Newest period end wins first: a restated older FY filed later must NOT beat the newer FY.
    public void Derive_RestatedOldPeriodFiledAfterNewer_PicksNewerPeriod()
    {
        var facts = Facts(
            ("LongTermDebtNoncurrent", new[] { Instant(1000m, D(2025, 12, 31), D(2026, 2, 15), "fy25") }),
            ("OperatingIncomeLoss", new[]
            {
                Annual(80m, 2025, D(2026, 2, 15), "fy25"),   // newer period (end 2025-12-31), filed earlier
                Annual(40m, 2024, D(2026, 5, 1), "fy24-r"),  // restated OLD period (end 2024-12-31), filed later
            }));

        var snapshot = FundamentalsCalculator.Derive("nordstar", facts, AsOf, NullLogger.Instance);

        snapshot.DebtToEbitda.Should().Be(1000m / 80m); // FY2025 (80), never the restated FY2024 (40)
    }

    [Fact] // No debt concept at all → TotalDebt null → Debt/EBITDA null (never 0), cash still present.
    public void Derive_MissingDebtConcept_LeavesDebtToEbitdaNull()
    {
        var facts = Facts(
            ("OperatingIncomeLoss", new[] { Annual(80m, 2025, D(2026, 2, 15), "fy25") }),
            ("CashAndCashEquivalentsAtCarryingValue", new[] { Instant(150m, D(2026, 3, 31), D(2026, 5, 20), "a-2") }));

        var snapshot = FundamentalsCalculator.Derive("nordstar", facts, AsOf, NullLogger.Instance);

        snapshot.DebtToEbitda.Should().BeNull();
        snapshot.CashAndEquivalents.Should().Be(150m);
    }

    [Fact] // Debt fallback chain: no noncurrent tag → LongTermDebt is used.
    public void Derive_DebtFallbackChain_UsesLongTermDebt()
    {
        var facts = Facts(
            ("LongTermDebt", new[] { Instant(600m, D(2026, 3, 31), D(2026, 5, 20), "a-2") }),
            ("OperatingIncomeLoss", new[] { Annual(100m, 2025, D(2026, 2, 15), "fy25") }));

        var snapshot = FundamentalsCalculator.Derive("nordstar", facts, AsOf, NullLogger.Instance);

        snapshot.DebtToEbitda.Should().Be(6m); // 600 / 100 via the fallback concept
    }

    [Fact] // Instant as-of: a fact filed AFTER asOf is excluded (no hindsight) — the older filing wins.
    public void Derive_InstantConcept_ExcludesFactFiledAfterAsOf()
    {
        var facts = Facts(
            ("CashAndCashEquivalentsAtCarryingValue", new[]
            {
                Instant(100m, D(2025, 12, 31), D(2026, 3, 1), "a-1"),   // filed ≤ asOf
                Instant(150m, D(2026, 3, 31), D(2026, 7, 20), "a-2"),   // filed AFTER asOf 2026-06-01
            }));

        var snapshot = FundamentalsCalculator.Derive("nordstar", facts, AsOf, NullLogger.Instance);

        snapshot.CashAndEquivalents.Should().Be(100m);
    }

    [Fact] // STK-R1: a current portion from an OLDER balance-sheet date (Q2) is NOT summed onto the
           // newer noncurrent debt (Q3) — mixing two vintages into one TotalDebt is the defect.
    public void Derive_CurrentPortionDifferentBalanceSheetDate_ExcludesStaleCurrentPortion()
    {
        var facts = Facts(
            ("LongTermDebtNoncurrent", new[] { Instant(500m, D(2025, 9, 30), D(2025, 11, 5), "q3-25") }),
            ("LongTermDebtCurrent", new[] { Instant(50m, D(2025, 6, 30), D(2025, 8, 5), "q2-25") }),
            ("OperatingIncomeLoss", new[] { Annual(100m, 2024, D(2025, 2, 15), "fy24") }));

        var snapshot = FundamentalsCalculator.Derive("nordstar", facts, AsOf, NullLogger.Instance);

        snapshot.DebtToEbitda.Should().Be(5m); // 500 / 100 — the stale Q2 current portion (50) is excluded
    }

    [Fact] // STK-R1 companion: a current portion sharing the noncurrent debt's balance-sheet date (same
           // End) IS folded in — the guard is a coherence check, not a blanket exclusion.
    public void Derive_CurrentPortionSameBalanceSheetDate_IncludesCurrentPortion()
    {
        var facts = Facts(
            ("LongTermDebtNoncurrent", new[] { Instant(500m, D(2025, 9, 30), D(2025, 11, 5), "q3-25") }),
            ("LongTermDebtCurrent", new[] { Instant(50m, D(2025, 9, 30), D(2025, 11, 5), "q3-25") }),
            ("OperatingIncomeLoss", new[] { Annual(100m, 2024, D(2025, 2, 15), "fy24") }));

        var snapshot = FundamentalsCalculator.Derive("nordstar", facts, AsOf, NullLogger.Instance);

        snapshot.DebtToEbitda.Should().Be(5.5m); // (500 + 50) / 100 — same-date current portion included
    }

    [Fact] // STK-R2: with D&A carried as BOTH a 3-month and a 12-month fact under the same 10-K accession
           // and FY end, EBITDA must use the ANNUAL (12-month) D&A, never the quarterly leg.
    public void Derive_DepreciationAnnualAndQuarterSameAccn_UsesAnnualDepreciation()
    {
        var facts = Facts(
            ("LongTermDebtNoncurrent", new[] { Instant(500m, D(2025, 12, 31), D(2026, 2, 15), "fy25") }),
            ("OperatingIncomeLoss", new[] { Annual(80m, 2025, D(2026, 2, 15), "fy25") }),
            ("DepreciationDepletionAndAmortization", new[]
            {
                // 3-month D&A (Q4 window) co-filed under the SAME 10-K accession + FY end — must be ignored.
                new XbrlFact(5m, D(2025, 10, 1), D(2025, 12, 31), D(2026, 2, 15), "CY2025Q4", "fy25", "10-K"),
                Annual(20m, 2025, D(2026, 2, 15), "fy25"), // 12-month annual D&A — the one EBITDA must use.
            }));

        var snapshot = FundamentalsCalculator.Derive("nordstar", facts, AsOf, NullLogger.Instance);

        snapshot.DebtToEbitda.Should().Be(500m / 100m); // 80 + annual D&A 20 = 100 (never 80 + 5)
    }

    [Fact] // STK-03 lock: the snapshot's own FilingDate/Type come from the newest USED fundamentals filing
           // (the 10-Q whose figures were selected), NOT the issuer's later 8-K LatestFilingDate. A
           // regression that stamped the snapshot with facts.LatestFilingDate would fail here.
    public void Derive_SnapshotFilingProvenance_UsesUsedFactsFilingNotIssuerLatest()
    {
        var facts = Facts(
            ("LongTermDebtNoncurrent", new[] { Instant(500m, D(2026, 3, 31), D(2026, 5, 20), "q1-26", "10-Q") }),
            ("OperatingIncomeLoss", new[] { Annual(100m, 2025, D(2026, 2, 15), "fy25") }),
            ("CashAndCashEquivalentsAtCarryingValue",
                new[] { Instant(150m, D(2026, 3, 31), D(2026, 5, 20), "q1-26", "10-Q") }));

        var snapshot = FundamentalsCalculator.Derive("nordstar", facts, AsOf, NullLogger.Instance);

        // Issuer latest filing (submissions) is a LATER 8-K with a different form.
        facts.LatestFilingDate.Should().Be(D(2026, 5, 25));
        facts.LatestFilingType.Should().Be("8-K");
        // The snapshot's provenance is the used 10-Q (2026-05-20 / 10-Q), never the issuer 8-K.
        snapshot.FilingDate.Should().Be(D(2026, 5, 20));
        snapshot.FilingType.Should().Be("10-Q");
    }
}
