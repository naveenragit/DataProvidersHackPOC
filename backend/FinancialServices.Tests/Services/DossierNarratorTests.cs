using FinancialServices.Api.Agents;
using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;
using FinancialServices.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinancialServices.Tests.Services;

/// <summary>
/// Proves the pkg-06 orchestration contract of <see cref="DossierNarrator"/> over a <b>real</b>
/// deterministic dossier (the NordStar money moment): narration only fills the empty
/// <c>Narrative</c>/<c>Explanation</c> fields (P2), a P4 term is dropped end-to-end while the rule
/// survives (P4), disabling narration is a pure passthrough, and a permanent misconfiguration fails
/// loud instead of silently degrading (P1). The Azure SDK is replaced by <see cref="FakeAgentTextRunner"/>.
/// </summary>
public sealed class DossierNarratorTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

    private static ReconciliationDossier NordStarDossier() =>
        DossierAssembler.Assemble(
            new DivergenceDecomposer(), new RedFlagEngine(),
            PrismFixtures.NordStarIssuer(), PrismFixtures.NordStarLatest(), PrismFixtures.NordStarRatings(),
            "nordstar:test", AsOf, Now);

    private static DossierNarrator Build(IAgentTextRunner runner, bool narrationEnabled = true)
    {
        IOptions<PrismOptions> options = Options.Create(new PrismOptions { NarrationEnabled = narrationEnabled });
        var redFlag = new RedFlagNarratorAgent(runner, options, NullLogger<RedFlagNarratorAgent>.Instance);
        var divergence = new DivergenceNarratorAgent(runner, options, NullLogger<DivergenceNarratorAgent>.Instance);
        return new DossierNarrator(redFlag, divergence, options, NullLogger<DossierNarrator>.Instance);
    }

    [Fact]
    public async Task Returns_the_dossier_untouched_when_narration_is_disabled()
    {
        var runner = FakeAgentTextRunner.Echo();
        ReconciliationDossier input = NordStarDossier();

        ReconciliationDossier result = await Build(runner, narrationEnabled: false)
            .NarrateAsync(PrismFixtures.NordStarIssuer(), input, CancellationToken.None);

        result.Should().BeSameAs(input);
        runner.Calls.Should().Be(0, "a disabled narrator must not call Foundry at all");
    }

    [Fact]
    public async Task Fills_narratives_without_altering_any_deterministic_value()
    {
        // The echo runner returns the grounded facts verbatim, so NarrationGuard accepts every narration.
        ReconciliationDossier input = NordStarDossier();

        ReconciliationDossier result = await Build(FakeAgentTextRunner.Echo())
            .NarrateAsync(PrismFixtures.NordStarIssuer(), input, CancellationToken.None);

        // Deterministic fields are byte-for-byte unchanged (P2 — the narrator only fills prose).
        result.Flags.Select(f => (f.Code, f.Severity, f.Rule))
            .Should().Equal(input.Flags.Select(f => (f.Code, f.Severity, f.Rule)));
        result.Divergences.SelectMany(d => d.Attribution).Select(b => (b.Bucket, b.Notches))
            .Should().Equal(input.Divergences.SelectMany(d => d.Attribution).Select(b => (b.Bucket, b.Notches)));

        // The STALE_INPUT money moment now carries a non-empty, guard-approved narrative.
        result.Flags.Should().Contain(f => f.Code == "STALE_INPUT" && f.Narrative.Length > 0);
    }

    [Fact]
    public async Task Drops_a_flag_narrative_that_gives_trading_advice_and_keeps_the_rule()
    {
        // The model appends a P4 term to otherwise-grounded prose -> the guard drops the whole narration,
        // leaving the deterministic rule authoritative (P2/P4).
        var runner = new FakeAgentTextRunner(prompt => prompt + " Investors should sell now.");
        ReconciliationDossier input = NordStarDossier();

        ReconciliationDossier result = await Build(runner)
            .NarrateAsync(PrismFixtures.NordStarIssuer(), input, CancellationToken.None);

        RedFlag stale = result.Flags.Single(f => f.Code == "STALE_INPUT");
        stale.Narrative.Should().BeEmpty();
        stale.Rule.Should().Be(input.Flags.Single(f => f.Code == "STALE_INPUT").Rule);
    }

    [Fact]
    public async Task Propagates_a_configuration_error_instead_of_degrading_silently()
    {
        // A permanent misconfiguration (unset endpoint / unknown deployment) must fail loud (P1), not
        // be swallowed into a blank narrative that ships forever.
        var runner = FakeAgentTextRunner.Throws(
            new ConfigurationException("Azure:OpenAiEndpoint", "not configured"));

        Func<Task> act = () => Build(runner)
            .NarrateAsync(PrismFixtures.NordStarIssuer(), NordStarDossier(), CancellationToken.None);

        await act.Should().ThrowAsync<ConfigurationException>();
    }
}
