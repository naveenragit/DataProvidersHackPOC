using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services;

/// <summary>
/// Fills the LLM narrative fields of a deterministic dossier (pkg 06): each
/// <see cref="RedFlag.Narrative"/> and each <see cref="BucketAttribution.Explanation"/>. It is
/// <b>best-effort</b> — a narration fault never breaks the reconciliation; the deterministic dossier is
/// authoritative (P1/P2). Returns a new dossier (records are immutable) with narratives filled where
/// the narration passed validation, and unchanged elsewhere.
/// </summary>
public interface IDossierNarrator
{
    Task<ReconciliationDossier> NarrateAsync(Issuer issuer, ReconciliationDossier dossier, CancellationToken ct);
}
