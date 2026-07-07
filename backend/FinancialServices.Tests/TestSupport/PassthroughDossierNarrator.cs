using FinancialServices.Api.Models;
using FinancialServices.Api.Services;

namespace FinancialServices.Tests.TestSupport;

/// <summary>
/// Test double for <see cref="IDossierNarrator"/> that returns the deterministic dossier unchanged
/// (no LLM narration). Used where a test exercises the deterministic persistence/orchestration path
/// and narration is irrelevant (fakes only in test code — P1).
/// </summary>
public sealed class PassthroughDossierNarrator : IDossierNarrator
{
    public Task<ReconciliationDossier> NarrateAsync(
        Issuer issuer, ReconciliationDossier dossier, CancellationToken ct) => Task.FromResult(dossier);
}
