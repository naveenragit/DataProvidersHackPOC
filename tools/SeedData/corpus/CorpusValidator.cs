using System.Text.RegularExpressions;
using FinancialServices.Api.Analysis;
using Prism.SeedData.Model;

namespace Prism.SeedData.Corpus;

/// <summary>Thrown when the corpus violates one or more integrity or acceptance invariants (P1 — fail loud).</summary>
public sealed class CorpusValidationException(IReadOnlyList<string> failures)
    : Exception("Corpus validation failed:" + "\n  - " + string.Join("\n  - ", failures))
{
    public IReadOnlyList<string> Failures { get; } = failures;
}

/// <summary>
/// Deterministic corpus integrity checks (no network). Enforces the package-03 acceptance invariants
/// and the non-negotiable principles: every rating card is labeled synthetic with a disclaimer (P1);
/// no buy/sell/recommend vocabulary appears anywhere (P4); notch matches <see cref="NotchLadder"/>
/// (P2 — one source of truth); the NordStar stale relationship and the Cedar Grove consensus hold.
/// Reused by the seeder (pre-upload gate) and the test suite.
/// </summary>
public static class CorpusValidator
{
    /// <summary>The authored corpus size (acceptance target ~30–50; curation beats volume).</summary>
    public const int ExpectedDocCount = 30;

    private static readonly string[] AllowedDocTypes = ["ratingCard", "methodology", "reference", "issuer"];

    // P4 — Prism is a data-quality/reconciliation tool, never a trading agent. These terms must not
    // appear in any indexed text. Word-boundary + invariant culture so "Holdings"/"bondholder" are safe.
    private static readonly string[] ForbiddenTerms =
        ["buy", "sell", "hold", "recommend", "allocate", "trade", "alpha", "signal"];

    private static readonly Regex ForbiddenRegex = new(
        @"\b(" + string.Join('|', ForbiddenTerms) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const decimal WeightSumTolerance = 0.001m;

    /// <summary>Validate the corpus; throw <see cref="CorpusValidationException"/> listing every failure.</summary>
    public static IReadOnlyList<CorpusDoc> Validate(IReadOnlyList<CorpusDoc> docs)
    {
        ArgumentNullException.ThrowIfNull(docs);
        var failures = new List<string>();

        foreach (var group in docs.GroupBy(d => d.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            failures.Add($"Duplicate document id '{group.Key}' ({group.Count()} occurrences).");
        }

        foreach (var doc in docs)
        {
            ValidateDoc(doc, failures);
        }

        ValidateStaleRelationship(docs, failures);
        ValidateConsensus(docs, failures);

        if (failures.Count > 0)
        {
            throw new CorpusValidationException(failures);
        }

        return docs;
    }

    private static void ValidateDoc(CorpusDoc doc, List<string> failures)
    {
        var at = $"[{(string.IsNullOrWhiteSpace(doc.Id) ? "<blank id>" : doc.Id)}]";

        if (string.IsNullOrWhiteSpace(doc.Id))
        {
            failures.Add("A document has a blank id.");
        }

        if (!AllowedDocTypes.Contains(doc.DocType, StringComparer.Ordinal))
        {
            failures.Add($"{at} unknown docType '{doc.DocType}'.");
        }

        if (string.IsNullOrWhiteSpace(doc.Content))
        {
            failures.Add($"{at} has blank content.");
        }

        if (string.IsNullOrWhiteSpace(doc.DataClass))
        {
            failures.Add($"{at} has blank dataClass.");
        }

        // P4 vocabulary across every indexed / human-visible string on the doc (incl. issuer metadata).
        var scanned = string.Join(
            ' ',
            doc.Content,
            doc.Disclaimer,
            doc.Letter,
            doc.LegalName ?? string.Empty,
            doc.Sector ?? string.Empty,
            doc.FilingType ?? string.Empty,
            doc.Factors is null ? string.Empty : string.Join(' ', doc.Factors.Select(f => f.Name)));
        var forbidden = ForbiddenRegex.Match(scanned);
        if (forbidden.Success)
        {
            failures.Add($"{at} contains forbidden trading term '{forbidden.Value}' (P4).");
        }

        switch (doc.DocType)
        {
            case "ratingCard":
                ValidateCard(doc, at, failures);
                break;
            case "methodology":
                RequireProvider(doc, at, failures);
                RequireDataClass(doc, "illustrative", at, failures);
                break;
            case "issuer":
                RequireEmptyProvider(doc, at, failures);
                RequireDataClass(doc, "synthetic", at, failures);
                if (doc.LatestFilingDate is null)
                {
                    failures.Add($"{at} issuer document is missing latestFilingDate.");
                }
                break;
            case "reference":
                RequireEmptyProvider(doc, at, failures);
                RequireDataClass(doc, "illustrative", at, failures);
                break;
        }
    }

    private static void ValidateCard(CorpusDoc doc, string at, List<string> failures)
    {
        // P1 — every card is unmistakably labeled synthetic and disclaimed.
        if (!string.Equals(doc.DataClass, "synthetic", StringComparison.Ordinal))
        {
            failures.Add($"{at} rating card dataClass must be 'synthetic' (P1), was '{doc.DataClass}'.");
        }

        if (string.IsNullOrWhiteSpace(doc.Disclaimer))
        {
            failures.Add($"{at} rating card is missing a synthetic disclaimer (P1).");
        }

        // Acceptance: every card carries inputAsOfDate.
        if (doc.InputAsOfDate == default)
        {
            failures.Add($"{at} rating card is missing inputAsOfDate.");
        }

        if (doc.AsOfDate == default)
        {
            failures.Add($"{at} rating card is missing asOfDate.");
        }

        RequireProvider(doc, at, failures);

        // P2 — notch is derived from the label by the single canonical ladder, not hand-typed drift.
        if (string.IsNullOrWhiteSpace(doc.Letter))
        {
            failures.Add($"{at} rating card is missing a letter grade.");
        }
        else
        {
            try
            {
                var expected = NotchLadder.ToNotch(doc.Letter);
                if (expected != doc.Notch)
                {
                    failures.Add($"{at} notch {doc.Notch} does not match NotchLadder('{doc.Letter}') = {expected}.");
                }
            }
            catch (ArgumentException)
            {
                failures.Add($"{at} letter '{doc.Letter}' is not a known rating grade.");
            }
        }

        // Acceptance: every factor carries a sourceRef; weights sum to ~1.
        if (doc.Factors is null || doc.Factors.Count == 0)
        {
            failures.Add($"{at} rating card has no factors.");
            return;
        }

        foreach (var factor in doc.Factors)
        {
            if (string.IsNullOrWhiteSpace(factor.SourceRef))
            {
                failures.Add($"{at} factor '{factor.Name}' is missing a sourceRef.");
            }
        }

        var weightSum = doc.Factors.Sum(f => f.Weight);
        if (Math.Abs(weightSum - 1m) > WeightSumTolerance)
        {
            failures.Add($"{at} factor weights sum to {weightSum}, expected ~1.0.");
        }
    }

    private static void RequireProvider(CorpusDoc doc, string at, List<string> failures)
    {
        if (!Enum.TryParse<Provider>(doc.Provider, ignoreCase: false, out _))
        {
            failures.Add($"{at} provider '{doc.Provider}' is not a known Provider (Moodys | MorningstarDbrs | Msci).");
        }
    }

    private static void RequireEmptyProvider(CorpusDoc doc, string at, List<string> failures)
    {
        if (!string.IsNullOrEmpty(doc.Provider))
        {
            failures.Add($"{at} {doc.DocType} document must not carry a provider (was '{doc.Provider}').");
        }
    }

    // P1 — the dataClass label is the honesty contract: synthetic issuers/cards, illustrative docs.
    private static void RequireDataClass(CorpusDoc doc, string expected, string at, List<string> failures)
    {
        if (!string.Equals(doc.DataClass, expected, StringComparison.Ordinal))
        {
            failures.Add($"{at} {doc.DocType} dataClass must be '{expected}' (P1), was '{doc.DataClass}'.");
        }
    }

    // Acceptance: NordStar's MSCI card inputAsOfDate (Q2) is before NordStar's Q3 filing date.
    private static void ValidateStaleRelationship(IReadOnlyList<CorpusDoc> docs, List<string> failures)
    {
        var msci = docs.FirstOrDefault(d => d is { DocType: "ratingCard", IssuerId: "nordstar", Provider: "Msci" });
        var issuer = docs.FirstOrDefault(d => d is { DocType: "issuer", IssuerId: "nordstar" });

        if (msci is null)
        {
            failures.Add("Missing NordStar MSCI rating card (needed for the stale-input money moment).");
            return;
        }

        if (issuer?.LatestFilingDate is null)
        {
            failures.Add("Missing NordStar issuer document / latestFilingDate (needed for the stale check).");
            return;
        }

        var input = msci.InputAsOfDate.UtcDateTime.Date;
        var filing = issuer.LatestFilingDate.Value.UtcDateTime.Date;
        if (input >= filing)
        {
            failures.Add(
                $"NordStar MSCI inputAsOfDate {input:yyyy-MM-dd} must be before the Q3 filing date {filing:yyyy-MM-dd} (stale money moment).");
        }
    }

    // Acceptance: the Cedar Grove consensus control returns three cards within one notch.
    private static void ValidateConsensus(IReadOnlyList<CorpusDoc> docs, List<string> failures)
    {
        var cards = docs
            .Where(d => d is { DocType: "ratingCard", IssuerId: "cedargrove" })
            .ToArray();

        if (cards.Length != 3)
        {
            failures.Add($"Cedar Grove consensus control must have exactly 3 rating cards, found {cards.Length}.");
            return;
        }

        var spread = cards.Max(c => c.Notch) - cards.Min(c => c.Notch);
        if (spread > 1)
        {
            failures.Add($"Cedar Grove consensus cards span {spread} notches; must be within 1.");
        }
    }
}
