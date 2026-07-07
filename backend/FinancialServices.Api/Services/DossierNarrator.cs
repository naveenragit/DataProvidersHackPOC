using FinancialServices.Api.Agents;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Services;

/// <summary>
/// Orchestrates the pkg-06 narrators over a deterministic dossier. Each flag and each divergence bucket
/// is narrated independently; a single narration fault is caught, logged (ids only, P6) and leaves that
/// field empty — it never aborts the others. When <see cref="PrismOptions.NarrationEnabled"/> is false
/// the dossier is returned untouched (deterministic-only). The deterministic numbers are never modified:
/// only the empty <c>Narrative</c>/<c>Explanation</c> strings are filled (P2).
/// </summary>
public sealed class DossierNarrator(
    RedFlagNarratorAgent redFlagNarrator,
    DivergenceNarratorAgent divergenceNarrator,
    IOptions<PrismOptions> options,
    ILogger<DossierNarrator> logger) : IDossierNarrator
{
    public async Task<ReconciliationDossier> NarrateAsync(
        Issuer issuer, ReconciliationDossier dossier, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(dossier);

        if (!options.Value.NarrationEnabled)
        {
            return dossier;
        }

        var narratedFlags = new List<RedFlag>(dossier.Flags.Count);
        foreach (RedFlag flag in dossier.Flags)
        {
            string narrative = await SafeNarrateAsync(
                () => redFlagNarrator.NarrateAsync(issuer, flag, ct), flag.Code, ct);
            narratedFlags.Add(flag with { Narrative = narrative });
        }

        var narratedDivergences = new List<PairDivergence>(dossier.Divergences.Count);
        foreach (PairDivergence divergence in dossier.Divergences)
        {
            var narratedBuckets = new List<BucketAttribution>(divergence.Attribution.Count);
            foreach (BucketAttribution bucket in divergence.Attribution)
            {
                string explanation = await SafeNarrateAsync(
                    () => divergenceNarrator.NarrateAsync(issuer, divergence, bucket, ct), bucket.Bucket, ct);
                narratedBuckets.Add(bucket with { Explanation = explanation });
            }

            narratedDivergences.Add(divergence with { Attribution = narratedBuckets });
        }

        return dossier with { Flags = narratedFlags, Divergences = narratedDivergences };
    }

    // Runs one narration; a genuine cancellation propagates (P7), any other fault falls back to empty
    // (the caller keeps the deterministic value). Never logs prompt bodies or model output (P6).
    private async Task<string> SafeNarrateAsync(Func<Task<string>> narrate, string item, CancellationToken ct)
    {
        try
        {
            return await narrate();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ConfigurationException)
        {
            // A misconfiguration (unset endpoint / unknown deployment) is permanent, not a transient
            // narration fault — fail loud (P1) rather than silently degrading to blank narration.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Narration failed for {Item}; keeping the deterministic value", item);
            return string.Empty;
        }
    }
}
