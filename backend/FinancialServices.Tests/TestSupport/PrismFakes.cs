using FinancialServices.Api.Analysis;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;

namespace FinancialServices.Tests.TestSupport;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
//  Test-only fakes for the Azure boundary services. These live ONLY in the test project (P1 — no
//  mocks in runtime code). They let the API pipeline + ReconciliationService be exercised offline.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>In-memory <see cref="ISearchCorpus"/>. Applies the same as-of gate as production.</summary>
internal sealed class FakeSearchCorpus(
    IEnumerable<(IssuerCorpusEntry Entry, IReadOnlyList<ProviderRating> Ratings)> issuers) : ISearchCorpus
{
    private readonly Dictionary<string, (IssuerCorpusEntry Entry, IReadOnlyList<ProviderRating> Ratings)> _issuers =
        issuers.ToDictionary(x => x.Entry.Issuer.IssuerId, x => x, StringComparer.Ordinal);

    public Task<IReadOnlyList<IssuerCorpusEntry>> GetIssuersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IssuerCorpusEntry>>(_issuers.Values.Select(v => v.Entry).ToArray());

    public Task<IssuerCorpusEntry?> GetIssuerAsync(string issuerId, CancellationToken ct) =>
        Task.FromResult(_issuers.TryGetValue(issuerId, out (IssuerCorpusEntry Entry, IReadOnlyList<ProviderRating> Ratings) v)
            ? v.Entry
            : null);

    public Task<IReadOnlyList<ProviderRating>> GetProviderRatingsAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProviderRating>>(
            _issuers.TryGetValue(issuerId, out (IssuerCorpusEntry Entry, IReadOnlyList<ProviderRating> Ratings) v)
                ? v.Ratings.Where(r => r.AsOfDate <= asOf).ToArray()
                : Array.Empty<ProviderRating>());
}

/// <summary>In-memory <see cref="ICosmosDossierStore"/> keyed by (issuerId, id) — mirrors a point read.</summary>
internal sealed class InMemoryDossierStore : ICosmosDossierStore
{
    private readonly Dictionary<string, ReconciliationDossier> _items = new(StringComparer.Ordinal);

    public int Count => _items.Count;

    public Task UpsertAsync(ReconciliationDossier dossier, CancellationToken ct)
    {
        _items[Key(dossier.Id, dossier.IssuerId)] = dossier;
        return Task.CompletedTask;
    }

    public Task<ReconciliationDossier?> ReadAsync(string id, string issuerId, CancellationToken ct) =>
        Task.FromResult(_items.TryGetValue(Key(id, issuerId), out ReconciliationDossier? d) ? d : null);

    private static string Key(string id, string issuerId) => $"{issuerId}|{id}";
}

/// <summary>Recording <see cref="IAuditService"/> — captures every written event for assertions.</summary>
internal sealed class RecordingAuditService : IAuditService
{
    public List<AuditEvent> Events { get; } = [];

    public Task WriteAsync(AuditEvent auditEvent, CancellationToken ct)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}

/// <summary>Builders for the standard fake corpus (NordStar = STALE money moment; Cedar Grove = clean).</summary>
internal static class PrismFakes
{
    public static FakeSearchCorpus StandardCorpus() => new(
    [
        (NordStarEntry(), PrismFixtures.NordStarRatings()),
        (CedarGroveEntry(), PrismFixtures.CedarGroveRatings()),
    ]);

    public static IssuerCorpusEntry NordStarEntry() =>
        new(PrismFixtures.NordStarIssuer(), PrismFixtures.NordStarLatest().FilingDate, "10-Q",
            [Provider.Moodys, Provider.MorningstarDbrs, Provider.Msci]);

    public static IssuerCorpusEntry CedarGroveEntry() =>
        new(PrismFixtures.CedarGroveIssuer(), PrismFixtures.CedarGroveLatest().FilingDate, "10-Q",
            [Provider.Moodys, Provider.MorningstarDbrs, Provider.Msci]);
}
