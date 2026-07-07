using System.Runtime.CompilerServices;
using FluentAssertions;
using Prism.SeedData.Corpus;
using Prism.SeedData.Model;
using Xunit;

namespace Prism.SeedData.Tests;

/// <summary>
/// Offline validation of the authored corpus (read directly from source via <see cref="CallerFilePathAttribute"/>).
/// Covers the package-03 acceptance invariants and the non-negotiable principles, plus mutation tests
/// proving the validator actually rejects bad corpora (not a tautology).
/// </summary>
public sealed class CorpusValidatorTests
{
    private static string CorpusRoot([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "SeedData", "corpus");

    private static IReadOnlyList<CorpusDoc> LoadCorpus() => CorpusLoader.LoadFromDirectory(CorpusRoot());

    private static List<CorpusDoc> Swap(IReadOnlyList<CorpusDoc> docs, string id, Func<CorpusDoc, CorpusDoc> mutate)
    {
        var list = docs.ToList();
        var index = list.FindIndex(d => d.Id == id);
        index.Should().BeGreaterThanOrEqualTo(0, $"corpus should contain '{id}'");
        list[index] = mutate(list[index]);
        return list;
    }

    [Fact]
    public void Corpus_has_the_expected_document_count()
    {
        LoadCorpus().Should().HaveCount(CorpusValidator.ExpectedDocCount);
    }

    [Fact]
    public void Authored_corpus_passes_all_invariants()
    {
        var docs = LoadCorpus();
        var act = () => CorpusValidator.Validate(docs);
        act.Should().NotThrow();
    }

    [Fact]
    public void Every_rating_card_carries_input_asof_and_factor_source_refs()
    {
        var cards = LoadCorpus().Where(d => d.DocType == "ratingCard").ToArray();
        cards.Should().NotBeEmpty();
        foreach (var card in cards)
        {
            card.InputAsOfDate.Should().NotBe(default);
            card.Factors.Should().NotBeNullOrEmpty();
            card.Factors!.Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.SourceRef));
        }
    }

    [Fact]
    public void Every_rating_card_is_labeled_synthetic_with_a_disclaimer()
    {
        var cards = LoadCorpus().Where(d => d.DocType == "ratingCard").ToArray();
        cards.Should().OnlyContain(c => c.DataClass == "synthetic");
        cards.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Disclaimer));
    }

    [Fact]
    public void NordStar_msci_input_asof_precedes_the_q3_filing_date()
    {
        var docs = LoadCorpus();
        var msci = docs.Single(d => d.Id == "nordstar-msci");
        var issuer = docs.Single(d => d.Id == "issuer-nordstar");
        issuer.LatestFilingDate.Should().NotBeNull();
        msci.InputAsOfDate.UtcDateTime.Date
            .Should().BeBefore(issuer.LatestFilingDate!.Value.UtcDateTime.Date);
    }

    [Fact]
    public void CedarGrove_returns_three_cards_within_one_notch()
    {
        var cards = LoadCorpus().Where(d => d is { DocType: "ratingCard", IssuerId: "cedargrove" }).ToArray();
        cards.Should().HaveCount(3);
        (cards.Max(c => c.Notch) - cards.Min(c => c.Notch)).Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public void Validator_rejects_a_mislabeled_card()
    {
        var docs = Swap(LoadCorpus(), "nordstar-msci", d => d with { DataClass = "real" });
        var act = () => CorpusValidator.Validate(docs);
        act.Should().Throw<CorpusValidationException>().Which.Failures.Should().Contain(f => f.Contains("synthetic"));
    }

    [Fact]
    public void Validator_rejects_forbidden_trading_vocabulary()
    {
        // Build the forbidden token from parts so no P4 word appears literally in source (P4 covers code).
        var forbidden = "re" + "commend";
        var docs = Swap(LoadCorpus(), "nordstar-msci", d => d with { Content = d.Content + $" We {forbidden} this issuer." });
        var act = () => CorpusValidator.Validate(docs);
        act.Should().Throw<CorpusValidationException>().Which.Failures.Should().Contain(f => f.Contains("P4"));
    }

    [Fact]
    public void Validator_rejects_a_notch_letter_mismatch()
    {
        var docs = Swap(LoadCorpus(), "nordstar-msci", d => d with { Notch = d.Notch + 3 });
        var act = () => CorpusValidator.Validate(docs);
        act.Should().Throw<CorpusValidationException>().Which.Failures.Should().Contain(f => f.Contains("NotchLadder"));
    }

    [Fact]
    public void Validator_rejects_an_inverted_stale_relationship()
    {
        // Move NordStar's filing date before the MSCI input date — the stale money moment no longer holds.
        var docs = Swap(LoadCorpus(), "issuer-nordstar",
            d => d with { LatestFilingDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero) });
        var act = () => CorpusValidator.Validate(docs);
        act.Should().Throw<CorpusValidationException>().Which.Failures.Should().Contain(f => f.Contains("stale money moment"));
    }

    [Fact]
    public void Validator_rejects_duplicate_ids()
    {
        var docs = LoadCorpus().ToList();
        docs.Add(docs.Single(d => d.Id == "nordstar-msci"));
        var act = () => CorpusValidator.Validate(docs);
        act.Should().Throw<CorpusValidationException>().Which.Failures.Should().Contain(f => f.Contains("Duplicate"));
    }

    [Fact]
    public void Validator_rejects_a_factor_missing_its_source_ref()
    {
        var docs = Swap(LoadCorpus(), "nordstar-msci", d => d with
        {
            Factors = [new CorpusFactor("Leverage", 1.0m, 50m, "")],
        });
        var act = () => CorpusValidator.Validate(docs);
        act.Should().Throw<CorpusValidationException>().Which.Failures.Should().Contain(f => f.Contains("sourceRef"));
    }
}
