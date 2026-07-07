using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services;

/// <summary>
/// Cosmos read/write boundary for reconciliation dossiers (arch-08: ReconciliationService owns Cosmos
/// I/O; this is the testable seam behind it). Container <c>rating_reconciliations</c>, partition key
/// <c>/issuerId</c>. Dossiers are immutable — a re-run writes a new id, never mutates.
/// </summary>
public interface ICosmosDossierStore
{
    /// <summary>Persists a dossier (point write on <c>/issuerId</c>).</summary>
    Task UpsertAsync(ReconciliationDossier dossier, CancellationToken ct);

    /// <summary>Point-reads a dossier by id + partition, or <c>null</c> when it does not exist.</summary>
    Task<ReconciliationDossier?> ReadAsync(string id, string issuerId, CancellationToken ct);
}
