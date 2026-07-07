using FinancialServices.Api.Analysis;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;
using FinancialServices.Tests.TestSupport;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Services;

/// <summary>
/// The pure composition of the deterministic engines into a dossier (P2). Exercises the four
/// package-03 casts through the real <see cref="DivergenceDecomposer"/> + <see cref="RedFlagEngine"/>
/// — the money moment (NordStar STALE_INPUT), the clean control (Cedar Grove), missing coverage
/// (Aster Bio) and an outlier (Onyx).
/// </summary>
public sealed class DossierAssemblerTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    private static ReconciliationDossier Assemble(
        Issuer issuer, FundamentalSnapshot latest, IReadOnlyList<ProviderRating> ratings) =>
        DossierAssembler.Assemble(
            new DivergenceDecomposer(), new RedFlagEngine(),
            issuer, latest, ratings, $"{issuer.IssuerId}:test", AsOf, Now);

    [Fact]
    public void NordStar_yields_the_stale_input_money_moment()
    {
        ReconciliationDossier dossier = Assemble(
            PrismFixtures.NordStarIssuer(), PrismFixtures.NordStarLatest(), PrismFixtures.NordStarRatings());

        dossier.Flags.Should().ContainSingle(f => f.Code == "STALE_INPUT")
            .Which.Severity.Should().Be("high");
        dossier.Divergences.Should().HaveCount(3); // three provider pairs
        dossier.ConsensusSummary.Should().Be("consensus within 1 notch");
        dossier.ConfidenceScore.Should().BeApproximately(0.70, 1e-9);
        dossier.Id.Should().Be("nordstar:test");
        dossier.Ratings.Should().HaveCount(3);
    }

    [Fact]
    public void CedarGrove_is_a_clean_full_consensus_with_no_flags()
    {
        ReconciliationDossier dossier = Assemble(
            PrismFixtures.CedarGroveIssuer(), PrismFixtures.CedarGroveLatest(), PrismFixtures.CedarGroveRatings());

        dossier.Flags.Should().BeEmpty();
        dossier.ConsensusSummary.Should().Be("full consensus");
        dossier.ConfidenceScore.Should().Be(1.0);
    }

    [Fact]
    public void AsterBio_flags_missing_coverage_and_reflects_it_in_confidence()
    {
        ReconciliationDossier dossier = Assemble(
            PrismFixtures.AsterBioIssuer(), PrismFixtures.AsterBioLatest(), PrismFixtures.AsterBioRatings());

        dossier.Flags.Should().Contain(f => f.Code == "MISSING_COVERAGE");
        dossier.ConfidenceScore.Should().BeApproximately(2.0 / 3.0, 1e-9); // 2 of 3 providers, no severity penalty
    }

    [Fact]
    public void Onyx_flags_the_outlier_provider()
    {
        ReconciliationDossier dossier = Assemble(
            PrismFixtures.OnyxIssuer(), PrismFixtures.OnyxLatest(), PrismFixtures.OnyxRatings());

        dossier.Flags.Should().Contain(f => f.Code == "OUTLIER_PROVIDER");
    }

    [Fact]
    public void Narrative_fields_are_empty_until_the_narrators_land()
    {
        ReconciliationDossier dossier = Assemble(
            PrismFixtures.NordStarIssuer(), PrismFixtures.NordStarLatest(), PrismFixtures.NordStarRatings());

        dossier.Flags.Should().OnlyContain(f => f.Narrative == "");
        dossier.Divergences.SelectMany(d => d.Attribution).Should().OnlyContain(b => b.Explanation == "");
    }
}
