using FinancialServices.Api.Analysis;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Analysis;

/// <summary>
/// Deterministic notch-ladder tests (core principle P2 — no LLM, no network). Covers canonical
/// round-trip, DBRS + Moody's aliases, tolerant whitespace, Gap sign, the IG/HY boundary, ToLabel
/// out-of-range throw (fail-loud), and null/blank/unknown/out-of-family label failure.
/// </summary>
public sealed class NotchLadderTests
{
    [Theory]
    [InlineData("AAA", 1)]
    [InlineData("AA+", 2)]
    [InlineData("AA", 3)]
    [InlineData("AA-", 4)]
    [InlineData("A+", 5)]
    [InlineData("A", 6)]
    [InlineData("A-", 7)]
    [InlineData("BBB+", 8)]
    [InlineData("BBB", 9)]
    [InlineData("BBB-", 10)]
    [InlineData("BB+", 11)]
    [InlineData("BB", 12)]
    [InlineData("BB-", 13)]
    [InlineData("B+", 14)]
    [InlineData("B", 15)]
    [InlineData("B-", 16)]
    [InlineData("CCC+", 17)]
    [InlineData("CCC", 18)]
    [InlineData("CCC-", 19)]
    [InlineData("CC", 20)]
    [InlineData("C", 21)]
    public void Canonical_Label_RoundTrips(string label, int notch)
    {
        NotchLadder.ToNotch(label).Should().Be(notch);
        NotchLadder.ToLabel(notch).Should().Be(label);
    }

    [Theory]
    [InlineData("AA (high)", 2)]
    [InlineData("AA (mid)", 3)]
    [InlineData("AA (low)", 4)]
    [InlineData("A (high)", 5)]
    [InlineData("A (mid)", 6)]   // D2: (mid) resolves to the bare family notch.
    [InlineData("A (low)", 7)]
    [InlineData("BBB (mid)", 9)]
    [InlineData("BBB (low)", 10)]
    [InlineData("BB (high)", 11)]
    [InlineData("B (low)", 16)]
    [InlineData("CCC (low)", 19)]
    public void Dbrs_Aliases_Map(string label, int notch)
    {
        NotchLadder.ToNotch(label).Should().Be(notch);
    }

    [Theory]
    [InlineData("aaa", 1)]    // lowercase (Moody's convention is "Aaa") — proves case-folding.
    [InlineData("AA1", 2)]    // uppercase (convention "Aa1").
    [InlineData("aa2", 3)]
    [InlineData("AA3", 4)]
    [InlineData("a1", 5)]
    [InlineData("a2", 6)]
    [InlineData("a3", 7)]
    [InlineData("baa1", 8)]   // lowercase (convention "Baa1") — the canonical case-folding proof.
    [InlineData("BAA2", 9)]
    [InlineData("baa3", 10)]
    [InlineData("ba1", 11)]
    [InlineData("b1", 14)]
    [InlineData("caa1", 17)]
    [InlineData("ca", 20)]
    [InlineData("c", 21)]
    public void Moody_Aliases_Map_CaseInsensitive(string label, int notch)
    {
        // Each input uses a different letter-case than the stored (uppercased) key, so a pass
        // genuinely proves Normalize folds case before matching.
        NotchLadder.ToNotch(label).Should().Be(notch);
    }

    [Fact]
    public void Normalize_Is_Trim_Case_And_Whitespace_Tolerant()
    {
        // D6: leading/trailing trim, case folding, and ALL internal whitespace collapse to one key.
        NotchLadder.ToNotch("  a (low)  ").Should().Be(7);
        NotchLadder.ToNotch("A (low)").Should().Be(7);
        NotchLadder.ToNotch("A(low)").Should().Be(7);
        NotchLadder.ToNotch("A  (low)").Should().Be(7);
        NotchLadder.ToNotch("A (LOW)").Should().Be(7);
    }

    [Fact]
    public void Gap_Is_Signed_B_Minus_A()
    {
        NotchLadder.Gap("A (low)", "BBB-").Should().Be(3);
        NotchLadder.Gap("BBB-", "A (low)").Should().Be(-3);
        NotchLadder.Gap("A", "A").Should().Be(0);
    }

    [Fact]
    public void IgHy_Boundary_Is_At_BBB_Minus()
    {
        NotchLadder.ToNotch("BBB-").Should().Be(10);                       // IG floor
        NotchLadder.CrossesIgHyBoundary("BBB-", "BB+").Should().BeTrue();  // 10 (IG) vs 11 (HY)
        NotchLadder.CrossesIgHyBoundary("A", "BBB").Should().BeFalse();    // both IG
        NotchLadder.CrossesIgHyBoundary("A (low)", "BBB-").Should().BeFalse(); // 7 & 10, both IG
    }

    [Theory]
    [InlineData(1, "AAA")]
    [InlineData(10, "BBB-")]
    [InlineData(21, "C")]
    public void ToLabel_Valid_Notch_Returns_Canonical(int notch, string label)
    {
        NotchLadder.ToLabel(notch).Should().Be(label);
    }

    [Theory]
    [InlineData(0)]     // below the 1–21 range
    [InlineData(99)]    // above the 1–21 range
    [InlineData(-1)]
    public void ToLabel_Out_Of_Range_Throws(int notch)
    {
        // Fail-loud (P1): an out-of-range notch is a range violation → ArgumentOutOfRangeException, not a clamp.
        var act = () => NotchLadder.ToLabel(notch);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("ZZZ")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A+X")]
    public void ToNotch_Unknown_Or_Blank_Throws(string label)
    {
        // An unknown/blank grade is a bad *value*, not a numeric range violation → ArgumentException.
        var act = () => NotchLadder.ToNotch(label);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void ToNotch_Null_Or_Blank_Throws_ArgumentException(string? label)
    {
        // ArgumentException.ThrowIfNullOrWhiteSpace surfaces a clear ArgumentException, never a
        // NullReferenceException. (ArgumentNullException derives from ArgumentException.)
        var act = () => NotchLadder.ToNotch(label!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("AAA (high)")]   // DBRS high/mid/low exists only for AA…CCC families, not AAA.
    [InlineData("Aa4")]          // Moody's Aa family stops at Aa3 — "Aa4" is out-of-family.
    public void ToNotch_OutOfFamily_Alias_Throws_ArgumentException(string label)
    {
        var act = () => NotchLadder.ToNotch(label);
        act.Should().Throw<ArgumentException>();
    }
}
