using System.Text;

namespace FinancialServices.Api.Analysis;

/// <summary>Credit-rating data providers reconciled by Prism (Moody's + Morningstar DBRS real APIs; MSCI = labeled-synthetic slot).</summary>
public enum Provider
{
    Moodys,
    MorningstarDbrs,
    Msci
}

/// <summary>
/// Deterministic credit-rating notch ladder — core principle P2 (plain C#, no LLM, no network).
/// Every provider label is mapped onto one canonical 1–21 integer scale (1 = AAA/Aaa … 21 = C,
/// higher = weaker) so a "3-notch gap" is precise, reproducible, and auditable.
/// </summary>
public static class NotchLadder
{
    /// <summary>
    /// The investment-grade floor on the canonical ladder: <c>BBB-</c> / <c>Baa3</c> = notch 10. Any
    /// notch ≤ this is investment grade; notch ≥ 11 (<c>BB+</c> / <c>Ba1</c>) is high yield. This is the
    /// single boundary constant the IG/HY straddle logic reuses (see <see cref="IsInvestmentGrade"/>).
    /// </summary>
    public const int IgHyFloorNotch = 10;

    // Canonical long-term ladder (S&P/Fitch style anchor). 1 = AAA … 21 = C.
    private static readonly string[] Canonical =
    {
        "AAA", "AA+", "AA", "AA-", "A+", "A", "A-", "BBB+", "BBB", "BBB-",
        "BB+", "BB", "BB-", "B+", "B", "B-", "CCC+", "CCC", "CCC-", "CC", "C"
    };

    // Non-grade statuses that are NOT points on the notch ladder — a rating agency legitimately emits
    // these instead of a grade. They must never crash the deterministic path (P1): the ingestion
    // boundary treats them as "no active comparable rating" and drops the card, which surfaces as
    // MISSING_COVERAGE rather than a fabricated notch. NR/WR = not-rated / withdrawn (no opinion);
    // D/SD/RD/LD = default states (an opinion, but a terminal one outside the 1–21 opinion ladder;
    // mapping a default to a distinct notch is a deliberate future refinement).
    private static readonly IReadOnlySet<string> NonGradeStatuses =
        new HashSet<string>(new[] { "NR", "WR", "D", "SD", "RD", "LD" }, StringComparer.Ordinal);

    // Known outlook / CreditWatch decorations that ride ALONGSIDE a grade (e.g. "BBB- (Negative)",
    // "A2 *-"). They carry direction, not level, so they are stripped before the ladder lookup. DBRS
    // "(HIGH)/(MID)/(LOW)" are level modifiers and are deliberately NOT in this set.
    private static readonly string[] OutlookDecorations =
    {
        "(NEGATIVE)", "(POSITIVE)", "(STABLE)", "(DEVELOPING)",
        ",NEGATIVE", ",POSITIVE", ",STABLE", ",DEVELOPING",
        "*-", "*+", "*",
    };

    // Provider-specific label → canonical notch. DBRS uses "(high)/(mid)/(low)"; Moody's uses Aaa/Aa1…
    private static readonly Dictionary<string, int> Map = BuildMap();

    /// <summary>Resolve a provider label to its canonical notch (1–21). Tolerant of case and whitespace.</summary>
    /// <exception cref="ArgumentException">The label is null, blank, or not a known rating grade.</exception>
    public static int ToNotch(string label)
    {
        // Fail-loud on null/blank (P1) — a clear ArgumentException, never a NullReferenceException.
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return TryToNotch(label, out var notch)
            ? notch
            // An unrecognized grade or a non-grade status is a bad *value* for a strict notch lookup →
            // ArgumentException. Tolerant callers (the ingestion mapper) use TryToNotch instead (P1).
            : throw new ArgumentException($"Unknown rating '{label}'.", nameof(label));
    }

    /// <summary>
    /// Tolerant notch resolution for the ingestion boundary (R2): resolves a grade — including one that
    /// carries an outlook / CreditWatch decoration (e.g. <c>"BBB- (Negative)"</c>, <c>"A2 *-"</c>) — to
    /// its canonical notch, and returns <c>false</c> (never throws) for a blank label or a non-grade
    /// status such as <c>NR</c> / <c>WR</c> / <c>D</c> / <c>SD</c> / <c>RD</c> / <c>LD</c>. Callers drop
    /// unresolved cards so a withdrawn/not-rated feed row becomes MISSING_COVERAGE, not a crash (P1).
    /// </summary>
    public static bool TryToNotch(string? label, out int notch)
    {
        notch = 0;
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var key = Normalize(label);

        // Exact grade first — keeps DBRS "(HIGH)/(MID)/(LOW)" and every canonical/Moody's alias intact.
        if (Map.TryGetValue(key, out notch))
        {
            return true;
        }

        // A pure non-grade status (possibly decorated) resolves to no notch — the card has no
        // comparable opinion on the ladder.
        var undecorated = StripOutlookDecorations(key);
        if (undecorated.Length == 0 || NonGradeStatuses.Contains(undecorated))
        {
            notch = 0;
            return false;
        }

        // Otherwise retry the ladder with the outlook/watch decoration removed (level, not direction).
        return Map.TryGetValue(undecorated, out notch);
    }

    /// <summary>
    /// True when <paramref name="label"/> is a non-grade rating status (<c>NR</c>, <c>WR</c>, <c>D</c>,
    /// <c>SD</c>, <c>RD</c>, <c>LD</c>) rather than a point on the notch ladder. Tolerant of case,
    /// whitespace, and any trailing outlook / CreditWatch decoration.
    /// </summary>
    public static bool IsNonGradeStatus(string? label) =>
        !string.IsNullOrWhiteSpace(label)
        && NonGradeStatuses.Contains(StripOutlookDecorations(Normalize(label)));

    /// <summary>True when <paramref name="notch"/> is investment grade (≤ <see cref="IgHyFloorNotch"/>).</summary>
    public static bool IsInvestmentGrade(int notch) => notch <= IgHyFloorNotch;

    /// <summary>Canonical S&amp;P-style label for a notch (1–21).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The notch is outside the valid 1–21 range.</exception>
    public static string ToLabel(int notch) =>
        notch is < 1 or > 21
            ? throw new ArgumentOutOfRangeException(nameof(notch), notch, "Notch must be 1..21.")
            : Canonical[notch - 1];

    /// <summary>Signed notch gap b − a (positive = b is weaker/lower rated than a).</summary>
    public static int Gap(string a, string b) => ToNotch(b) - ToNotch(a);

    /// <summary>True when a and b sit on opposite sides of the investment-grade / high-yield line.</summary>
    public static bool CrossesIgHyBoundary(string a, string b) =>
        IsInvestmentGrade(ToNotch(a)) != IsInvestmentGrade(ToNotch(b)); // BBB- (10) is the IG floor

    // Tolerant match key (D6): uppercase and strip ALL whitespace so "A (LOW)", "A(LOW)", and
    // "A  (low)" resolve to the same notch. Public labels stay canonical (see ToLabel).
    private static string Normalize(string label)
    {
        var builder = new StringBuilder(label.Length);
        foreach (var ch in label)
        {
            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }

        return builder.ToString();
    }

    // Removes any known outlook / CreditWatch decoration from an already-normalized key. Only the
    // decorations in <see cref="OutlookDecorations"/> are stripped (direction markers), so DBRS level
    // modifiers "(HIGH)/(MID)/(LOW)" and every grade alias are untouched.
    private static string StripOutlookDecorations(string normalizedKey)
    {
        var key = normalizedKey;
        foreach (var decoration in OutlookDecorations)
        {
            if (key.Contains(decoration, StringComparison.Ordinal))
            {
                key = key.Replace(decoration, string.Empty, StringComparison.Ordinal);
            }
        }

        return key;
    }

    private static Dictionary<string, int> BuildMap()
    {
        // Ordinal comparer is safe and deterministic because every key is pre-normalized to
        // uppercase, whitespace-free form (D5). Indexer assignment is idempotent for the handful
        // of value-consistent overlaps (e.g. "AAA", "C") shared across alias sets (D4).
        var map = new Dictionary<string, int>(StringComparer.Ordinal);

        // 1) Canonical S&P/Fitch ladder (the anchor).
        for (var i = 0; i < Canonical.Length; i++)
        {
            map[Normalize(Canonical[i])] = i + 1;
        }

        // 2) Morningstar DBRS "(high)/(mid)/(low)" grades: (high) = one notch stronger,
        //    (mid) = the bare family notch (D2), (low) = one notch weaker.
        (string Family, int MidNotch)[] dbrsFamilies =
        {
            ("AA", 3), ("A", 6), ("BBB", 9), ("BB", 12), ("B", 15), ("CCC", 18)
        };
        foreach (var (family, midNotch) in dbrsFamilies)
        {
            map[Normalize($"{family} (high)")] = midNotch - 1;
            // "(mid)" is an INPUT alias only (bare family notch); it is never emitted by ToLabel as a display label.
            map[Normalize($"{family} (mid)")] = midNotch;
            map[Normalize($"{family} (low)")] = midNotch + 1;
        }

        // 3) Moody's Aaa/Aa1…C grades (1:1 rank cross-walk onto the 21 canonical notches, D3).
        (string Label, int Notch)[] moodyGrades =
        {
            ("Aaa", 1), ("Aa1", 2), ("Aa2", 3), ("Aa3", 4),
            ("A1", 5), ("A2", 6), ("A3", 7),
            ("Baa1", 8), ("Baa2", 9), ("Baa3", 10),
            ("Ba1", 11), ("Ba2", 12), ("Ba3", 13),
            ("B1", 14), ("B2", 15), ("B3", 16),
            ("Caa1", 17), ("Caa2", 18), ("Caa3", 19),
            ("Ca", 20), ("C", 21)
        };
        foreach (var (label, notch) in moodyGrades)
        {
            map[Normalize(label)] = notch;
        }

        return map;
    }
}
