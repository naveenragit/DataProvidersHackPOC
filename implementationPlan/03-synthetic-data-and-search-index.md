# 03 — Provider Ratings Corpus & AI Search Index

**Purpose:** assemble the ratings the demo reconciles and index the supporting docs in Azure AI
Search with as-of metadata.

**Depends on:** 02. **Blocks:** 05, 06.

> **UPDATE (2026-07-06):** provider ratings for **Moody's** and **Morningstar DBRS** now come from
> **real APIs** (package 04), not synthetic cards. Only the **MSCI** 3rd slot is synthetic (clearly
> labeled) until entitlement is confirmed, plus per-provider **methodology docs**. We **curate the
> demo by issuer *selection*** over real data — scout genuine split ratings + a genuinely stale
> rating vs a recent filing — never by fabricating ratings. **Curation beats volume.**
>
> Azure AI Search still holds: methodology docs (for the citation/"per-methodology" narration), the
> labeled-synthetic MSCI cards, and a **cache** of fetched real ratings (with `asOfDate` /
> `ratingActionDate` metadata) so retrieval + as-of filtering work uniformly across providers.

---

## A. The issuer cast (design the divergences on purpose)

> **Now:** pick **real** issuers whose live Moody's/DBRS ratings exhibit each pattern (scout via the
> APIs in package 04). The synthetic names below are the dev/offline fallback and the target profiles.

| Target profile (dev name) | Pattern | Divergence to find/curate |
|---|---|---|
| **Stale rating** ⭐ (NordStar Industrials) | One agency's rating action predates a recent 10-Q showing improvement | **STALE_INPUT** — the money moment (real `ratingActionDate` < real EDGAR filing date) |
| **Methodology gap** (Helios Utilities) | Sector/parent-support treatment differs | **METHODOLOGY_ADJUSTMENT** — Moody's vs DBRS notch gap driven by adjustment |
| **Consensus** (Cedar Grove Foods) | Agencies agree | **CONSENSUS** control — within 1 notch (system must exonerate) |
| **Fallen-angel boundary** (Meridian Freight) | Sits at the IG/HY line | Split straddling **BBB- / BB+** — "one notch matters" |
| **Thin coverage** (Aster Bio) | One agency doesn't rate the issuer | **MISSING_COVERAGE** — confidence drops |
| **Outlier** (Onyx Capital) | One provider far from peers | **OUTLIER_PROVIDER** |

For each issuer, hand-pick real US public-company financials to ground it (map `cik`/`ticker` to a
real EDGAR filer in package 04). NordStar's Q2→Q3 deleveraging must be reflected in the real EDGAR
facts so the stale flag is provable.

---

## B. Document shapes (JSON, one file per doc under `tools/SeedData/corpus/`)

**Provider rating card** (`{issuerId}-{provider}.json`) — 3 per issuer:
```json
{
  "id": "nordstar-msci",
  "docType": "ratingCard",
  "issuerId": "nordstar",
  "provider": "Msci",
  "letter": "BBB-",
  "asOfDate": "2026-05-10",
  "inputAsOfDate": "2026-03-31",
  "factors": [
    { "name": "Leverage", "weight": 0.35, "score": 58, "sourceRef": "nordstar-fin-q2" },
    { "name": "InterestCoverage", "weight": 0.20, "score": 62, "sourceRef": "nordstar-fin-q2" },
    { "name": "SectorEsgOverlay", "weight": 0.15, "score": 45, "sourceRef": "msci-methodology" }
  ],
  "content": "MSCI illustrative issuer assessment for NordStar Industrials …",
  "disclaimer": "Synthetic, illustrative — not MSCI data."
}
```

**Methodology doc** (`{provider}-methodology.json`) — 1 per provider: prose describing factor
weights + adjustments so agents can cite *"per DBRS methodology, leverage weight 35%."*

Design the numbers so the deterministic decomposer (package 05) reproduces the intended waterfall.

---

## C. Azure AI Search index

Index name from `Azure__SearchIndex` (`prism-ratings`). Fields:

| Field | Type | Notes |
|---|---|---|
| `id` | Edm.String (key) | |
| `docType` | Edm.String (filterable, facetable) | `ratingCard` / `methodology` |
| `issuerId` | Edm.String (filterable) | |
| `provider` | Edm.String (filterable, facetable) | |
| `asOfDate`, `inputAsOfDate` | Edm.DateTimeOffset (filterable, sortable) | **as-of metadata is essential** |
| `content` | Edm.String (searchable) | |
| `contentVector` | Collection(Edm.Single) | dims match `text-embedding-3-small` (1536) |

- Vectorizer at index time **must match** query-time (`text-embedding-3-small`) or retrieval silently
  degrades (HACKATHON-FINDINGS §6).
- Configure hybrid (keyword + vector) + semantic ranking.

### Upload path
Build a small console app `tools/SeedData` (`Azure.Search.Documents` + `Azure.AI.OpenAI` for
embeddings) that: creates the index, embeds each doc's `content`, and uploads. Alternative: portal
**Import-and-vectorize** wizard (faster, but the console app is reproducible in CI/rehearsal).

---

## Acceptance for this package
- [ ] ~30–50 docs authored; every rating card carries `inputAsOfDate` + factor `sourceRef`s.
- [ ] NordStar's MSCI card `inputAsOfDate` (Q2) is **before** NordStar's real Q3 EDGAR filing date.
- [ ] Index created; hybrid query for "NordStar leverage" returns the MSCI card **with metadata intact**
      (acceptance test from HACKATHON-FINDINGS Day 1).
- [ ] Consensus issuer (Cedar Grove) returns three cards within 1 notch.
- [ ] Re-run of `tools/SeedData` is idempotent.
