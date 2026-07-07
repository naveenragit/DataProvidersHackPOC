using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;
using FinancialServices.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinancialServices.Tests.Services;

/// <summary>
/// The fallback orchestration end-to-end against test fakes (P1 — fakes only in test code): a run
/// persists a dossier carrying the STALE_INPUT flag (A2), writes an audit event (A4) and round-trips
/// through the store (A3); an unknown issuer fails loud (404).
/// </summary>
public sealed class ReconciliationServiceTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (ReconciliationService Service, InMemoryDossierStore Store, RecordingAuditService Audit) Build()
    {
        var store = new InMemoryDossierStore();
        var audit = new RecordingAuditService();
        var service = new ReconciliationService(
            PrismFakes.StandardCorpus(), store, audit,
            new DivergenceDecomposer(), new RedFlagEngine(),
            new PassthroughDossierNarrator(),
            TimeProvider.System, NullLogger<ReconciliationService>.Instance);
        return (service, store, audit);
    }

    [Fact]
    public async Task RunAsync_persists_a_NordStar_dossier_with_the_stale_flag()
    {
        (ReconciliationService service, InMemoryDossierStore store, _) = Build();

        ReconciliationDossier dossier = await service.RunAsync("nordstar", AsOf, CancellationToken.None);

        dossier.Flags.Should().Contain(f => f.Code == "STALE_INPUT");
        dossier.Id.Should().StartWith("nordstar:");
        store.Count.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_writes_an_audit_event_with_counts_only()
    {
        (ReconciliationService service, _, RecordingAuditService audit) = Build();

        ReconciliationDossier dossier = await service.RunAsync("nordstar", AsOf, CancellationToken.None);

        audit.Events.Should().ContainSingle();
        AuditEvent evt = audit.Events[0];
        evt.Action.Should().Be("dossier_generated");
        evt.IssuerId.Should().Be("nordstar");
        evt.Metadata["dossierId"].Should().Be(dossier.Id);
        evt.Metadata["staleFlagCount"].Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_round_trips_the_persisted_dossier()
    {
        (ReconciliationService service, _, _) = Build();
        ReconciliationDossier saved = await service.RunAsync("nordstar", AsOf, CancellationToken.None);

        ReconciliationDossier? fetched = await service.GetAsync(saved.Id, "nordstar", CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.Id.Should().Be(saved.Id);
        fetched.Flags.Should().Contain(f => f.Code == "STALE_INPUT");
    }

    [Fact]
    public async Task RunAsync_throws_NotFound_for_an_unknown_issuer()
    {
        (ReconciliationService service, _, _) = Build();

        Func<Task> act = () => service.RunAsync("ghost", AsOf, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetIssuersAsync_returns_the_cast()
    {
        (ReconciliationService service, _, _) = Build();

        IReadOnlyList<Issuer> issuers = await service.GetIssuersAsync(CancellationToken.None);

        issuers.Select(i => i.IssuerId).Should().Contain(["nordstar", "cedar-grove"]);
    }
}
