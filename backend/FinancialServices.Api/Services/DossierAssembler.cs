using FinancialServices.Api.Analysis;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services;

/// <summary>
/// Pure composition of the deterministic <c>Analysis/</c> engines into a
/// <see cref="ReconciliationDossier"/> (P2 — no I/O, no LLM). It <b>orchestrates</b>
/// <see cref="DivergenceDecomposer"/>, <see cref="RedFlagEngine"/> and
/// <see cref="ReconciliationScoring"/>; it never re-implements notch/gap/flag math. Narrative fields
/// stay empty — the pkg-06 narrators fill them later; the verbatim rule text is the money moment.
/// </summary>
public static class DossierAssembler
{
    public static ReconciliationDossier Assemble(
        DivergenceDecomposer decomposer,
        RedFlagEngine redFlagEngine,
        Issuer issuer,
        FundamentalSnapshot latest,
        IReadOnlyList<ProviderRating> ratings,
        string id,
        DateTimeOffset asOf,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(decomposer);
        ArgumentNullException.ThrowIfNull(redFlagEngine);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(ratings);

        // Stable order (by provider enum) for deterministic output + pair enumeration.
        ProviderRating[] ordered = ratings.OrderBy(rating => (int)rating.Provider).ToArray();

        // Decompose every unique provider pair. No per-provider vintage snapshots exist for the
        // letter-only corpus (P1 — no fabrication), so aInputs/bInputs are null: the Input bucket is
        // 0 and the gap is honestly residual-dominated (pkg 05), never a faked precise split.
        var divergences = new List<PairDivergence>();
        for (int i = 0; i < ordered.Length; i++)
        {
            for (int j = i + 1; j < ordered.Length; j++)
            {
                divergences.Add(decomposer.Decompose(ordered[i], ordered[j], latest, aInputs: null, bInputs: null));
            }
        }

        IReadOnlyList<RedFlag> flags = redFlagEngine.Evaluate(issuer, ordered, latest, divergences);
        string consensus = ReconciliationScoring.ConsensusSummary(ordered);
        double confidence = ReconciliationScoring.ConfidenceScore(ordered, flags);

        return new ReconciliationDossier(
            Id: id,
            IssuerId: issuer.IssuerId,
            AsOfDate: asOf,
            Ratings: ordered,
            Fundamentals: latest,
            Divergences: divergences,
            Flags: flags,
            ConsensusSummary: consensus,
            ConfidenceScore: confidence,
            GeneratedAt: generatedAt);
    }
}
