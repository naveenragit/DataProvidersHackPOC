using FinancialServices.Api.Agents;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Agents;

/// <summary>
/// Proves the pkg-06 acceptance guarantee "narrators never alter deterministic numbers" (spec §C, P2).
/// <see cref="NarrationGuard.Sanitize"/> is the mechanical enforcement: a narration is accepted only if
/// it cites every evidence reference and every number it contains also appears in the grounding facts.
/// </summary>
public sealed class NarrationGuardTests
{
    private static readonly IReadOnlyList<string> Refs = new[] { "nordstar-Msci", "edgar:0000000001:10-Q" };

    private const string Grounding =
        "Flag: STALE_INPUT (severity high). Rule: Rating action dated 2025-09-15 predates the issuer's " +
        "latest filing (10-Q) on 2025-11-05. Evidence references: nordstar-Msci, edgar:0000000001:10-Q.";

    [Fact]
    public void Accepts_a_grounded_narration_that_cites_every_reference()
    {
        const string narrative =
            "The rating action dated 2025-09-15 predates the latest 10-Q filing on 2025-11-05 " +
            "(nordstar-Msci, edgar:0000000001:10-Q).";

        NarrationGuard.Sanitize(narrative, Grounding, Refs).Should().Be(narrative.Trim());
    }

    [Fact]
    public void Drops_a_narration_that_ALTERS_a_deterministic_date()
    {
        // The model changed the filing date 2025-11-05 -> 2025-11-06. This must be rejected (P2).
        const string tampered =
            "The rating action dated 2025-09-15 predates the latest 10-Q filing on 2025-11-06 " +
            "(nordstar-Msci, edgar:0000000001:10-Q).";

        NarrationGuard.Sanitize(tampered, Grounding, Refs).Should().BeEmpty();
    }

    [Fact]
    public void Drops_a_narration_that_INVENTS_a_number()
    {
        const string invented =
            "The rating action dated 2025-09-15 predates the latest 10-Q filing on 2025-11-05 by 61 days " +
            "(nordstar-Msci, edgar:0000000001:10-Q).";

        NarrationGuard.Sanitize(invented, Grounding, Refs).Should().BeEmpty();
    }

    [Fact]
    public void Drops_a_narration_that_omits_a_required_evidence_reference()
    {
        const string missingRef =
            "The rating action dated 2025-09-15 predates the latest 10-Q filing on 2025-11-05 (nordstar-Msci).";

        NarrationGuard.Sanitize(missingRef, Grounding, Refs).Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Drops_empty_or_whitespace_narration(string? narrative)
    {
        NarrationGuard.Sanitize(narrative, Grounding, Refs).Should().BeEmpty();
    }

    [Fact]
    public void Treats_thousands_separated_and_plain_numbers_as_equal()
    {
        const string grounding = "Total debt is 1234 as of 2025-11-05. Evidence references: ref-a.";
        var refs = new[] { "ref-a" };
        const string narrative = "Total debt is 1,234 as of 2025-11-05 (ref-a).";

        NarrationGuard.Sanitize(narrative, grounding, refs).Should().Be(narrative.Trim());
    }

    [Fact]
    public void Drops_a_narration_that_gives_trading_advice_even_when_otherwise_grounded()
    {
        // Cites both refs and uses only grounded numbers, so it clears the numeric/reference gates.
        // The single P4 term "buy" must still drop it — instructions are not enforcement (adversary C1).
        const string advice =
            "The rating action dated 2025-09-15 predates the latest 10-Q filing on 2025-11-05, so " +
            "investors should buy (nordstar-Msci, edgar:0000000001:10-Q).";

        NarrationGuard.Sanitize(advice, Grounding, Refs).Should().BeEmpty();
    }

    [Theory]
    [InlineData("buy")]
    [InlineData("sell")]
    [InlineData("hold")]
    [InlineData("recommend")]
    [InlineData("allocate")]
    [InlineData("trade")]
    [InlineData("alpha")]
    [InlineData("signal")]
    public void Drops_a_narration_for_every_prohibited_P4_term(string term)
    {
        string advice =
            $"The action dated 2025-09-15 predates the 10-Q on 2025-11-05; treat this as a {term} " +
            "(nordstar-Msci, edgar:0000000001:10-Q).";

        NarrationGuard.Sanitize(advice, Grounding, Refs).Should().BeEmpty();
    }

    [Fact]
    public void Allows_words_that_merely_contain_a_prohibited_substring()
    {
        // "Bondholders" contains "hold" but is a legitimate noun; word-boundary matching must not trip
        // on it, otherwise every dossier would silently lose its narration.
        const string grounding = "Bondholders of record on 2025-11-05 total 12. Evidence references: ref-a.";
        var refs = new[] { "ref-a" };
        const string narrative = "Bondholders of record on 2025-11-05 number 12 (ref-a).";

        NarrationGuard.Sanitize(narrative, grounding, refs).Should().Be(narrative.Trim());
    }
}
