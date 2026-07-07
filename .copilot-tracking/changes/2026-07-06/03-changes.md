# Package 03 — Provider Ratings Corpus & AI Search Index — Changes Log

**Date:** 2026-07-06 · **Plan:** `.copilot-tracking/plans/2026-07-06/03-plan.md`
**Build:** `dotnet build backend/FinancialServices.slnx` ✅ 0 warnings ·
**Test:** `dotnet test backend/FinancialServices.slnx` ✅ 144 passing (123 existing + 21 new) ·
**Dry-run:** `dotnet run --project tools/SeedData -- --dry-run` ✅ 30 docs, all invariants pass.

---

## Files created — corpus (30 labeled-synthetic JSON docs under `tools/SeedData/corpus/`)

| Group | Files | Notes |
|---|---|---|
| `issuers/` (6) | nordstar, helios, cedargrove, meridian, asterbio, onyx | `docType:"issuer"`, `dataClass:"synthetic"`, each carries `latestFilingDate`/`filingType` |
| `cards/` (17) | nordstar-{msci,moodys,morningstardbrs} + helios×3 + cedargrove×3 + meridian×3 + asterbio×2 + onyx×3 | `docType:"ratingCard"`, `dataClass:"synthetic"` + `disclaimer`; every card has `inputAsOfDate` + factor `sourceRef`s; weights sum to 1.0; `notch` matches `NotchLadder(letter)` |
| `methodology/` (3) | moodys, morningstardbrs, msci | `docType:"methodology"`, `dataClass:"illustrative"`; citation targets ("per DBRS methodology, leverage 35%") |
| `reference/` (4) | notch-scale, as-of-policy, divergence-taxonomy, data-quality | `docType:"reference"`, `dataClass:"illustrative"` |

The cast is coherent with pkg-05 `PrismFixtures` (same issuer ids + notch profiles): NordStar
{9,9,10} MSCI stale (`inputAsOfDate` 2025-08-20 < Q3 filing 2025-11-05); Helios {8,10,9} 2-notch
methodology gap; Cedar Grove {6,6,6} consensus (zero flags); Meridian {10,11,10} IG/HY straddle;
Aster Bio {12,12,—} missing coverage; Onyx {6,6,10} outlier. **No P4 vocabulary** in any content
(regex-enforced + tested).

## Files created — `tools/SeedData/` (console uploader)

| File | Responsibility |
|---|---|
| `SeedData.csproj` | net9.0 Exe, Nullable + warnaserror + InvariantGlobalization; refs `Azure.Search.Documents` 11.6.0, `Azure.AI.OpenAI` 2.1.0, `Azure.Identity` 1.13.2, `Microsoft.Extensions.Hosting` 9.0.0; ProjectReference → `FinancialServices.Api` (reuse `NotchLadder`/`Provider`, P2/P8); copies `corpus/**` to output |
| `Model/CorpusDoc.cs` | `CorpusDoc` + `CorpusFactor` records; one shape for all docTypes; `required` members fail loud (P1) |
| `Corpus/CorpusLoader.cs` | Loads/parses every `corpus/**/*.json`; fail loud on missing dir / empty / malformed / null |
| `Corpus/CorpusValidator.cs` | Pure invariants (no network): unique ids; card `notch == NotchLadder.ToNotch(letter)` (P2); card `dataClass=="synthetic"` + disclaimer (P1); `inputAsOfDate` + factor `sourceRef`s; weights ≈ 1; **P4 vocabulary** regex; NordStar stale relationship; Cedar Grove within-1-notch. Throws `CorpusValidationException` listing all failures |
| `Configuration/AzureSeedOptions.cs` | Bound from `Azure` section; `ValidateForSeeding()` fails loud naming any missing setting (P1); no keys — `DefaultAzureCredential` (P6) |
| `Search/IndexDoc.cs` | Index projection (`[JsonPropertyName]` matches schema) + embedding vector |
| `Search/PrismSearchIndex.cs` | Builds the `prism-ratings` `SearchIndex`: filterable/sortable as-of metadata, `dataClass` label, 1536-dim `contentVector` (HNSW), Azure OpenAI vectorizer pinned to the embedding deployment, semantic config |
| `Search/SeedRunner.cs` | `CreateOrUpdateIndex` → embed each `content` (1536 dims) → `MergeOrUpload` (idempotent by id); fail loud on any rejected doc; logs ids + counts only (P6); `CancellationToken` throughout (P7) |
| `Search/SearchVerifier.cs` | Live acceptance checks: index config (vector dims, vectorizer, semantic), doc count == 30, hybrid "NordStar leverage" → `nordstar-msci` with metadata, Cedar Grove 3 cards within 1 notch |
| `Program.cs` | Host builder + console logging + config; modes `seed` (default), `--dry-run` (offline), `--verify`; Ctrl+C → cancellation; non-zero exit on failure |

## Files created — `tools/SeedData.Tests/` (21 tests, offline)

| File | Coverage |
|---|---|
| `SeedData.Tests.csproj` | xUnit 2.9.2 + FluentAssertions `[6.12.1,7.0.0)` + Test.Sdk 17.12.0; Nullable + warnaserror; refs `SeedData` |
| `CorpusValidatorTests.cs` | Reads the authored corpus via `[CallerFilePath]`; asserts doc count 30, all invariants pass, cards labeled synthetic + `inputAsOfDate` + `sourceRef`s, NordStar stale, Cedar Grove within-1-notch; **7 mutation tests** prove the validator rejects mislabeled cards, P4 vocab, notch mismatch, inverted stale, duplicate ids, and a missing `sourceRef` |
| `PrismSearchIndexTests.cs` | Offline schema checks: `id` key, 1536-dim vector field, vectorizer → configured deployment + model, semantic config present, as-of + facet fields filterable/sortable |

## Files edited

- `backend/FinancialServices.slnx` — added `../tools/SeedData/SeedData.csproj` + `../tools/SeedData.Tests/SeedData.Tests.csproj` so the single gate command covers them.
- `.env.example` — added `Azure__EmbeddingDeployment` / `Azure__EmbeddingModel` / `Azure__EmbeddingDimensions` (auth via `DefaultAzureCredential`, no keys).

## Build/test evidence

- `dotnet build backend/FinancialServices.slnx` → Build succeeded, **0 Warnings, 0 Errors**.
- `dotnet test backend/FinancialServices.slnx` → **123** (FinancialServices.Tests) + **21** (SeedData.Tests) = **144 passing**.
- `dotnet run --project tools/SeedData -- --dry-run` → "Corpus OK: 30 documents, all integrity + acceptance invariants passed" (issuer 6 / methodology 3 / ratingCard 17 / reference 4).

## Only compiler fix during implementation

`SearchDocument.GetInt32(...)` returns `int?` → guarded null (fail loud on a missing notch) instead of
adding `int?` to `List<int>`. Every other Azure SDK call (index build, vectorizer, embeddings, hybrid
query) compiled on the first attempt (surface confirmed against Microsoft Learn samples).

## Corrections (post-adversarial-review)

Applied from `.copilot-tracking/reviews/2026-07-06/03-review.md`. Rebuild 0-warn; **144 tests still green**.

| Finding | Fix |
|---|---|
| A03-01 High | `Search/SearchVerifier.cs` — the acceptance query now embeds "NordStar leverage" **client-side** with the operator credential + configured deployment and issues a `VectorizedQuery`, instead of `VectorizableTextQuery` (which needed the Search service's managed identity to hold AOAI RBAC). The index vectorizer stays configured (spec §C) and its presence is still asserted via `GetIndex`. |
| A03-02 Med | `Configuration/AzureSeedOptions.cs` — `ValidateForSeeding()` throws `FinancialServices.Api.Infrastructure.Errors.ConfigurationException` (arch 03 taxonomy) instead of `InvalidOperationException`; `Program.cs` gains a dedicated catch (exit 3, names the setting). |
| A03-03 Med | `AzureSeedOptions.EmbeddingDimensions` documented: integrated vectorization uses the model default (1536), so this must equal it; the verifier asserts the index field dim == this value. |
| A03-04 Low | `Corpus/CorpusValidator.cs` — the P4 vocabulary scan now also covers issuer `LegalName`, `Sector`, `FilingType`. |
| A03-05 Low | `Corpus/CorpusValidator.cs` — new `RequireDataClass`: issuer ⇒ `synthetic`, methodology/reference ⇒ `illustrative` (cards already required `synthetic`). |
| A03-06 Low | Doc count 30 accepted ("curation beats volume"); recorded as residual. |

## Residual risks (deferred)

- **Live gate:** `text-embedding-3-small` must be deployed on `foundry-dataproviders-poc` for acceptance
  items 3–5 (index create + hybrid query + Cedar Grove query + idempotency against live Search).
- **Real-EDGAR anchoring:** issuers are fictional + labeled synthetic; mapping NordStar to a real EDGAR
  filer (and using its real Q3 filing date) is real-data curation for pkg 04 completion / pkg 12.
- **API-side Search reader:** `SearchCorpus` service that *reads* the index is pkg 06/08 (arch 08).
- **Runtime integrated vectorization:** if pkg 06/07 uses `VectorizableTextQuery`, the Search service's
  managed identity needs *Cognitive Services OpenAI User* on the Foundry resource.
- **Doc count 30** is the low end of "~30–50"; add peer issuers only if a later package needs them.

