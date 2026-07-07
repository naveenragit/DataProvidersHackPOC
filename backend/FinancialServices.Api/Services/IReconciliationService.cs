using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services;

/// <summary>
/// The fallback (synchronous, non-streaming) reconciliation surface + Cosmos ownership (spec §B,
/// arch-08). The AG-UI orchestrator (pkg 07) composes the same deterministic building blocks; this
/// service is what the REST controllers call.
/// </summary>
public interface IReconciliationService
{
    /// <summary>The issuer cast (from the Search corpus).</summary>
    Task<IReadOnlyList<Issuer>> GetIssuersAsync(CancellationToken ct);

    /// <summary>
    /// Runs the deterministic fallback sweep for one issuer as-of a date: corpus fetch → decompose +
    /// red-flag rules + scoring → persist to Cosmos → write an audit event. No LLM narration (pkg 06);
    /// the verbatim rule text is the money moment. Throws <c>NotFoundException</c> for an unknown issuer.
    /// </summary>
    Task<ReconciliationDossier> RunAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct);

    /// <summary>Point-reads a persisted dossier (partition key required), or <c>null</c>.</summary>
    Task<ReconciliationDossier?> GetAsync(string id, string issuerId, CancellationToken ct);

    /// <summary>Persists a dossier (used by the orchestrator path, pkg 07).</summary>
    Task SaveAsync(ReconciliationDossier dossier, CancellationToken ct);
}
