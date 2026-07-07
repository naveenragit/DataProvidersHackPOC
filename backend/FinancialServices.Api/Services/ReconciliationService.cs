using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services;

/// <summary>
/// Fallback reconciliation orchestrator + Cosmos owner (spec §B). Composes the pkg-03 Search corpus,
/// the deterministic pkg-05 engines (via <see cref="DossierAssembler"/>), Cosmos persistence and the
/// audit trail. <b>No LLM</b> here — the pkg-06 narrators are a later, additive step; the deterministic
/// rule text carries the demo. <see cref="CancellationToken"/> is plumbed through every call (P7).
/// </summary>
public sealed class ReconciliationService(
    ISearchCorpus corpus,
    ICosmosDossierStore store,
    IAuditService audit,
    DivergenceDecomposer decomposer,
    RedFlagEngine redFlagEngine,
    IDossierNarrator narrator,
    TimeProvider clock,
    ILogger<ReconciliationService> logger) : IReconciliationService
{
    public async Task<IReadOnlyList<Issuer>> GetIssuersAsync(CancellationToken ct)
    {
        IReadOnlyList<IssuerCorpusEntry> entries = await corpus.GetIssuersAsync(ct);
        return entries.Select(entry => entry.Issuer).ToArray();
    }

    public async Task<ReconciliationDossier> RunAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);

        IssuerCorpusEntry entry = await corpus.GetIssuerAsync(issuerId, ct)
            ?? throw new NotFoundException("issuer", issuerId);

        // Everything downstream (dossier id prefix, partition key, ratings fetch, audit) uses the
        // corpus's CANONICAL issuer id — not the raw request value. A case/whitespace variant would
        // otherwise split the write id from the persisted partition key and 404 the GET round-trip
        // (adversary STK-08-01).
        string canonicalIssuerId = entry.Issuer.IssuerId;

        IReadOnlyList<ProviderRating> ratings = await corpus.GetProviderRatingsAsync(canonicalIssuerId, asOf, ct);

        // The as-of filing boundary comes from the labeled-synthetic corpus issuer doc. Real financial
        // ratios are genuinely absent for the synthetic cast → null (P1 — never fabricated). Real-issuer
        // EDGAR/FRED enrichment (pkg 04, already wired) is a documented seam; it is deliberately not
        // invoked on synthetic CIKs (which 404).
        var latest = new FundamentalSnapshot(
            entry.Issuer.IssuerId, entry.LatestFilingDate, entry.FilingType,
            DebtToEbitda: null, InterestCoverage: null, CashAndEquivalents: null);

        DateTimeOffset now = clock.GetUtcNow();
        string dossierId = NewPartitionedId(canonicalIssuerId);

        ReconciliationDossier dossier = DossierAssembler.Assemble(
            decomposer, redFlagEngine, entry.Issuer, latest, ratings, dossierId, asOf, now);

        // pkg 06 — fill the narrative fields via the Foundry narrators (best-effort). A narration fault
        // must NOT break the reconciliation: the deterministic dossier is authoritative (P1/P2). Only a
        // genuine cancellation propagates (P7); any other fault logs and keeps the deterministic dossier.
        try
        {
            dossier = await narrator.NarrateAsync(entry.Issuer, dossier, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ConfigurationException)
        {
            // Misconfiguration is permanent — fail loud (P1); do not ship blank narration silently.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Narration unavailable for {IssuerId}; returning the deterministic dossier", canonicalIssuerId);
        }

        await store.UpsertAsync(dossier, ct);

        await audit.WriteAsync(
            new AuditEvent(
                Id: NewPartitionedId(canonicalIssuerId),
                EventType: "reconciliation",
                Timestamp: now,
                Actor: "system",
                IssuerId: canonicalIssuerId,
                Action: "dossier_generated",
                Metadata: new Dictionary<string, object>
                {
                    ["dossierId"] = dossierId,
                    ["ratingCount"] = ratings.Count,
                    ["flagCount"] = dossier.Flags.Count,
                    ["staleFlagCount"] = dossier.Flags.Count(f => string.Equals(f.Code, "STALE_INPUT", StringComparison.Ordinal)),
                    ["narratedFlagCount"] = dossier.Flags.Count(f => !string.IsNullOrEmpty(f.Narrative)),
                }),
            ct);

        using (logger.BeginScope(new Dictionary<string, object> { ["issuerId"] = canonicalIssuerId }))
        {
            // ids + counts only (P6) — never the dossier payload/financials.
            logger.LogInformation(
                "Reconciliation dossier {DossierId} generated: {RatingCount} ratings, {FlagCount} flags",
                dossierId, ratings.Count, dossier.Flags.Count);
        }

        return dossier;
    }

    public Task<ReconciliationDossier?> GetAsync(string id, string issuerId, CancellationToken ct) =>
        store.ReadAsync(id, issuerId, ct);

    public Task SaveAsync(ReconciliationDossier dossier, CancellationToken ct) =>
        store.UpsertAsync(dossier, ct);

    /// <summary>
    /// Builds an id that encodes the partition key: <c>{issuerId}:{guid}</c>. This lets
    /// <c>GET /reconciliations/{id}</c> (which carries the id only) recover the issuerId for a Cosmos
    /// point read. The guid keeps each run's dossier immutable (arch-08).
    /// </summary>
    private static string NewPartitionedId(string issuerId) =>
        $"{issuerId}:{Guid.NewGuid():N}";
}
