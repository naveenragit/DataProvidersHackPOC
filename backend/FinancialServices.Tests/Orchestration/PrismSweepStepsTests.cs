using FinancialServices.Api.Agents;
using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using FinancialServices.Api.Orchestration;
using FinancialServices.Api.Services;
using FinancialServices.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinancialServices.Tests.Orchestration;

/// <summary>
/// Proves the pkg-07 stream-robustness contract of <see cref="PrismSweepSteps"/> (adversary ARC-01 /
/// STK-03): the decorative card narration is best-effort, so a <b>transient</b> Foundry fault degrades
/// to an empty narrative and the sweep keeps the deterministic corpus rating (letting the stream reach
/// the authoritative dossier), whereas a <b>permanent</b> misconfiguration fails loud (P1). The Azure
/// SDK is replaced by <see cref="FakeAgentTextRunner"/> and the corpus by <see cref="FakeSearchCorpus"/>.
/// </summary>
public sealed class PrismSweepStepsTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static PrismSweepSteps Build(IAgentTextRunner runner)
    {
        IOptions<PrismOptions> options = Options.Create(new PrismOptions());
        var provider = new ProviderExplainerAgent(runner, options, NullLogger<ProviderExplainerAgent>.Instance);
        var fundamentals = new FundamentalsAgent(runner, options, NullLogger<FundamentalsAgent>.Instance);
        return new PrismSweepSteps(
            PrismFakes.StandardCorpus(), provider, fundamentals, NullLogger<PrismSweepSteps>.Instance);
    }

    [Fact]
    public async Task Provider_card_survives_a_transient_narration_fault_with_the_deterministic_rating()
    {
        // The narrator agent throws a transient fault (429/timeout). The card must still return the
        // corpus rating with an empty explanation so the stream continues to the authoritative dossier.
        PrismSweepSteps steps = Build(FakeAgentTextRunner.Throws(new TimeoutException("429 throttled")));
        Issuer nordstar = PrismFakes.NordStarEntry().Issuer;

        var card = await steps.RetrieveProviderRatingAsync(nordstar, Provider.Msci, AsOf, CancellationToken.None);

        card.Should().NotBeNull();
        card!.Value.Rating.Provider.Should().Be(Provider.Msci);
        card.Value.Explanation.Text.Should().BeEmpty("a dropped narration falls back to the deterministic value");
    }

    [Fact]
    public async Task Provider_card_fails_loud_on_a_permanent_configuration_error()
    {
        // A misconfiguration is permanent, not a transient card fault — it must propagate (P1), never
        // masquerade as a silently un-narrated card.
        PrismSweepSteps steps = Build(
            FakeAgentTextRunner.Throws(new ConfigurationException("Azure:OpenAiEndpoint", "not configured")));
        Issuer nordstar = PrismFakes.NordStarEntry().Issuer;

        Func<Task> act = () => steps.RetrieveProviderRatingAsync(nordstar, Provider.Msci, AsOf, CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationException>();
    }

    [Fact]
    public async Task Fundamentals_card_survives_a_transient_narration_fault()
    {
        PrismSweepSteps steps = Build(FakeAgentTextRunner.Throws(new TimeoutException("429 throttled")));
        IssuerCorpusEntry entry = PrismFakes.NordStarEntry();

        (FundamentalSnapshot snapshot, FundamentalsSummary summary) =
            await steps.GroundFundamentalsAsync(entry, CancellationToken.None);

        snapshot.IssuerId.Should().Be(entry.Issuer.IssuerId);
        summary.Text.Should().BeEmpty();
    }
}
