using System.Diagnostics;
using FinancialServices.Api.Analysis;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;

namespace FinancialServices.Api.Orchestration;

/// <summary>
/// The pkg-07 streaming reconciliation orchestrator (spec §D — the guaranteed-green fallback transport).
/// Runs the ordered sweep and emits progress events (gate → provider cards → fundamentals → waterfall →
/// red-flags → dossier) through <paramref name="emit"/>. The <b>authoritative</b> dossier (decomposition,
/// red flags, narration, persistence) is produced by the SAME <see cref="IReconciliationService.RunAsync"/>
/// as the atomic REST endpoint, so the streamed <c>dossier-ready</c> object is byte-for-byte the REST
/// object (acceptance #4) and the numbers are the deterministic pkg-05 values (P2). The model never
/// orders or recomputes anything here — this is a deterministic C# orchestration; the LLM only narrates
/// the individual cards. A span is opened per step (arch-07); ids + counts are logged, never payloads (P6).
/// </summary>
public sealed class PrismStreamingOrchestrator(
    PrismSweepSteps steps,
    IReconciliationService reconciliation,
    ILogger<PrismStreamingOrchestrator> logger)
{
    private static readonly ActivitySource Activity = new("FinancialServices.Orchestration");

    /// <summary>
    /// Runs the sweep for one issuer, emitting typed events. Pauses at the scope gate (P5): when
    /// <paramref name="confirmed"/> is false only the <c>scope-confirm</c> (+ <c>awaiting-approval</c>)
    /// events are emitted and the run stops. <paramref name="emit"/> is <c>(type, payload, ct)</c>.
    /// </summary>
    public async Task RunAsync(
        string? issuerIdRaw,
        DateTimeOffset asOf,
        bool confirmed,
        Func<string, object, CancellationToken, Task> emit,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(emit);

        IssuerCorpusEntry entry = await steps.ResolveIssuerAsync(issuerIdRaw, ct);
        string issuerId = entry.Issuer.IssuerId;

        using (logger.BeginScope(new Dictionary<string, object> { ["issuerId"] = issuerId }))
        {
            // 1 ── scope gate (P5). The run does not proceed until the caller confirms the scope.
            await emit(PrismStreamEventTypes.ScopeConfirm, new ScopeConfirmPayload(
                issuerId, entry.Issuer.LegalName, asOf, entry.Coverage, confirmed,
                confirmed
                    ? "Scope approved: reconcile provider ratings and cite the evidence."
                    : "Confirm the reconciliation scope to proceed."), ct);

            if (!confirmed)
            {
                await emit(PrismStreamEventTypes.AwaitingApproval, new ScopeConfirmPayload(
                    issuerId, entry.Issuer.LegalName, asOf, entry.Coverage, false,
                    "Awaiting scope confirmation."), ct);
                logger.LogInformation("Stream paused at scope gate for {IssuerId} (unconfirmed)", issuerId);
                return;
            }

            // 2 ── provider verdict cards (retrieveProviderRating per provider), each narrated (pkg 06).
            foreach (Provider provider in entry.Coverage)
            {
                using Activity? span = Activity.StartActivity("Sweep.retrieveProviderRating");
                span?.SetTag("prism.issuer", issuerId);
                span?.SetTag("prism.provider", provider.ToString());

                var card = await steps.RetrieveProviderRatingAsync(entry.Issuer, provider, asOf, ct);
                if (card is null)
                {
                    continue; // no coverage → deterministic MISSING_COVERAGE flag reports it later.
                }

                await emit(PrismStreamEventTypes.ProviderRating, new ProviderRatingPayload(
                    card.Value.Rating.ToVerdictDto(),
                    card.Value.Explanation.Text,
                    card.Value.Explanation.Citations), ct);
            }

            // 3 ── fundamentals card (groundFundamentals), narrated (pkg 06).
            using (Activity? span = Activity.StartActivity("Sweep.groundFundamentals"))
            {
                span?.SetTag("prism.issuer", issuerId);
                (FundamentalSnapshot snapshot, var summary) = await steps.GroundFundamentalsAsync(entry, ct);
                await emit(PrismStreamEventTypes.Fundamentals, new FundamentalsPayload(
                    issuerId, snapshot.FilingType, snapshot.FilingDate,
                    snapshot.DebtToEbitda, snapshot.InterestCoverage, snapshot.CashAndEquivalents,
                    summary.Text, summary.Citations), ct);
            }

            // 4 ── the authoritative dossier: decompose + red-flags + narration + persist (P2). This is
            //       the SAME code path as POST /api/v1/reconciliations → identical object (acceptance #4).
            ReconciliationDossier dossier;
            using (Activity? span = Activity.StartActivity("Sweep.assembleDossier"))
            {
                span?.SetTag("prism.issuer", issuerId);
                dossier = await reconciliation.RunAsync(issuerId, asOf, ct);
            }

            // 5 ── decomposition waterfall (deterministic buckets, pkg 05).
            await emit(PrismStreamEventTypes.Divergence, new DivergencePayload(
                dossier.Divergences.Select(d => d.ToDivergenceDto()).ToArray()), ct);

            // 6 ── each deterministic red flag with its verbatim rule text (pkg 05).
            foreach (RedFlag flag in dossier.Flags)
            {
                await emit(PrismStreamEventTypes.RedFlag, new RedFlagPayload(flag.ToFlagDto()), ct);
            }

            // 7 ── the full persisted dossier (id included) — the fallback REST object.
            await emit(PrismStreamEventTypes.DossierReady, new DossierReadyPayload(dossier.ToResponse()), ct);

            logger.LogInformation(
                "Stream complete for {IssuerId}: {RatingCount} ratings, {FlagCount} flags, dossier {DossierId}",
                issuerId, dossier.Ratings.Count, dossier.Flags.Count, dossier.Id);
        }
    }
}
