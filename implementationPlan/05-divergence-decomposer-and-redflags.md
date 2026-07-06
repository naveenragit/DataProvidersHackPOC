# 05 — Divergence Decomposer & Red-Flag Rules

**Purpose:** the **deterministic heart** of Prism — the money moment. Pure C#, **no LLM**. Given
provider ratings + real fundamentals, compute the notch gaps, attribute them to buckets, and fire
red flags with the exact rule text the UI will display.

**Depends on:** 02, 03, 04. **Blocks:** 07, 08.

Create under `backend/FinancialServices.Api/Analysis/`. Every method here must be unit-tested and
reproducible — this is what survives judge skepticism.

---

## A. DivergenceDecomposer

For each provider pair, split the notch gap into three buckets:

1. **Weighting** — same inputs, different factor weights. Contribution ≈ Σ over factors of
   `(weightA − weightB) × normalizedScore` re-expressed in notches via a fixed scale constant.
2. **Input** — factor `sourceRef`/`inputAsOfDate` differ (one provider used older financials).
   Contribution = notches explained by the input delta (recompute each provider's leverage factor on
   its own `inputAsOfDate` fundamentals vs the latest; the difference is the input bucket).
3. **MethodologyAdjustment** — residual driven by overlay factors present in one provider only
   (e.g. `SectorEsgOverlay`).

```csharp
public sealed class DivergenceDecomposer
{
    public PairDivergence Decompose(ProviderRating a, ProviderRating b,
                                    FundamentalSnapshot latest, FundamentalSnapshot? aInputs,
                                    FundamentalSnapshot? bInputs)
    {
        int gap = b.Notch - a.Notch;
        var weighting = WeightingContribution(a, b);
        var input     = InputContribution(a, b, latest, aInputs, bInputs);
        var adj       = gap - weighting - input;   // residual = methodology overlay
        return new PairDivergence(a.Provider, b.Provider, gap, new[]
        {
            new BucketAttribution("Weighting", weighting, "", RefsFor(a,b,"weight")),
            new BucketAttribution("Input", input, "", RefsFor(a,b,"input")),
            new BucketAttribution("MethodologyAdjustment", adj, "", RefsFor(a,b,"overlay")),
        });
    }
}
```

**Invariant (unit-tested):** `Weighting + Input + MethodologyAdjustment == NotchGap` exactly. The
waterfall in the UI (package 10) renders these three bars. Round to whole/half notches for display,
but keep the reconciliation exact.

The LLM (package 06 red-flag/narrator) later fills each bucket's `Explanation` and cites the
`EvidenceRefs` — it never changes the numbers.

---

## B. RedFlagEngine (deterministic rules)

```csharp
public sealed class RedFlagEngine
{
    public IReadOnlyList<RedFlag> Evaluate(Issuer issuer,
        IReadOnlyList<ProviderRating> ratings, FundamentalSnapshot latest)
    {
        var flags = new List<RedFlag>();

        // ── THE MONEY MOMENT — STALE_INPUT ────────────────────────────────
        foreach (var r in ratings)
            if (r.InputAsOfDate < latest.FilingDate)
                flags.Add(new RedFlag(
                    "STALE_INPUT", "high",
                    $"Provider input as-of {r.InputAsOfDate:yyyy-MM-dd} predates the issuer's latest " +
                    $"filing {latest.FilingType} on {latest.FilingDate:yyyy-MM-dd}.",
                    Narrative: "",   // filled by LLM narrator, cites both dates
                    EvidenceRefs: new[] { $"{issuer.IssuerId}-{r.Provider}", $"edgar:{issuer.Cik}:{latest.FilingType}" }));

        // ── MISSING_COVERAGE ──────────────────────────────────────────────
        foreach (var p in Enum.GetValues<Provider>())
            if (ratings.All(r => r.Provider != p))
                flags.Add(new RedFlag("MISSING_COVERAGE", "medium",
                    $"{p} publishes no rating for {issuer.LegalName}.", "", new[] { issuer.IssuerId }));

        // ── OUTLIER_PROVIDER ──────────────────────────────────────────────
        if (ratings.Count >= 3)
        {
            var median = Median(ratings.Select(r => r.Notch));
            foreach (var r in ratings)
                if (Math.Abs(r.Notch - median) >= 3)
                    flags.Add(new RedFlag("OUTLIER_PROVIDER", "medium",
                        $"{r.Provider} sits {Math.Abs(r.Notch-median)} notches from the peer median.",
                        "", new[] { $"{issuer.IssuerId}-{r.Provider}" }));
        }
        return flags;
    }
}
```

- Severity is fixed by rule, not by the model. The UI shows `Rule` verbatim on click-through.
- Add a `ConfidenceScore` helper: deterministic from coverage (how many providers) + freshness
  (max input staleness) → drives the dossier confidence and the consensus vs split summary.

---

## Acceptance for this package
- [ ] Attribution invariant holds for every pair (`sum == gap`) — property test.
- [ ] NordStar (package 03) → **exactly one** `STALE_INPUT` high flag on the MSCI card, citing the
      MSCI `inputAsOfDate` and the real EDGAR Q3 filing date.
- [ ] Cedar Grove (consensus) → **zero** flags, confidence high.
- [ ] Aster Bio → `MISSING_COVERAGE`. Onyx → `OUTLIER_PROVIDER`.
- [ ] Decomposer + engine run with **no network and no LLM** (pure functions over inputs).
