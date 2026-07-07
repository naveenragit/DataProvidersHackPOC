# Package 03 — Provider Ratings Corpus & AI Search Index — Detailed Plan

**Date:** 2026-07-06 · **Package:** `implementationPlan/03-synthetic-data-and-search-index.md`
**Depends on:** 02 (✅ done). **Blocks:** 05, 06.
**Planner lens** (Fin Task Planner). Conforms to `architecturalPlan/` 00–11.

---

## 0. Orchestration note

The `/prism-build-package` workflow delegates phases to sub-agents via `run-subagent`. That tool is
**not available** in this environment (verified: `tool_search` returns no dispatch tool; the
orchestrator agent's `handoffs` are `send:false` UI buttons). Per the orchestrator's "never fabricate
results" rule, the orchestrator executes each phase itself while adopting each specialist lens
(Planner → Implementor → Standards/Architect/Security/Stack adversaries → Reviewer) and writes the
mandated `.copilot-tracking/` artifacts — identical to how packages 02/04/05/09 were completed here.

## 1. Gating dependency (live Azure)

`az cognitiveservices account deployment list -n foundry-dataproviders-poc -g rg-data-providers-poc`
returns **only `gpt-5.4`**. There is **no embedding deployment**. A chat model cannot produce
embeddings, so the 1536-dim `contentVector` (spec §C: `text-embedding-3-small`) cannot be populated
until a `text-embedding-3-small` deployment exists on the account.

**Consequence:** everything except live vector population is buildable + offline-verifiable now. The
live seed/verify (acceptance items 3–5 against real Search) is gated on that one deployment. This is a
**named missing config** per the workflow STOP condition — handled at the live gate, not faked.

## 2. Scope decisions (locked)

- **Labeled-synthetic corpus** (user directive + P1): every rating card carries `dataClass:"synthetic"`
  + a `disclaimer`; methodology/reference docs carry `dataClass:"illustrative"`. Provider field still
  reads `Moodys`/`MorningstarDbrs`/`Msci` so retrieval/filtering is uniform, but nothing is presented
  as real provider data. When the real Moody's/DBRS APIs land (pkg 04 completion) their cache overlays
  this substrate with `dataClass:"real"`.
- **Real-EDGAR issuer anchoring is deferred** (fictional issuers). Acceptance item 2 is satisfied as a
  data invariant (MSCI `inputAsOfDate` < issuer `latestFilingDate`), enforced + tested; substituting a
  real filer is a documented follow-up for real-data curation (pkg 04/12).
- **API-side Search *reader* (`SearchCorpus` service) is out of scope** for pkg 03 — arch 08 assigns it
  to the consumers (pkg 06/08). Pkg 03 = author corpus + build/populate/verify index via the tool. This
  keeps the 123 backend tests untouched.
- **Tool placement:** `tools/SeedData/` (spec-exact). Both the tool and its test project are added to
  `backend/FinancialServices.slnx` (via `..\..\tools\...` paths) so the existing gate commands
  (`dotnet build`/`dotnet test backend/FinancialServices.slnx`) cover them in one run. `tools/` does
  not inherit `backend/Directory.Build.props`, so `Nullable`+`TreatWarningsAsErrors` are set in each
  new csproj (arch 11).
- **Reuse (P8/P2):** `SeedData` references `FinancialServices.Api` to reuse `NotchLadder` + `Provider`
  — notch math stays in one place; the tool never re-implements it.

## 3. Corpus design — the issuer cast (30 docs)

Notches use `NotchLadder`. Coherent with pkg 05 `PrismFixtures` (same issuer ids + notch profiles).

| Issuer (`issuerId`) | Moody's | Morningstar DBRS | MSCI (synthetic) | Notches | Pattern / flag |
|---|---|---|---|---|---|
| NordStar Industrials (`nordstar`) | `Baa2` | `BBB (mid)` | `BBB-` | 9 / 9 / 10 | **STALE_INPUT** — MSCI `inputAsOfDate` 2025-08-20 < filing 2025-11-05 |
| Helios Utilities (`helios`) | `Baa1` | `BBB (low)` | `BBB` | 8 / 10 / 9 | **METHODOLOGY_CONFLICT** — Moody's↔DBRS gap = 2, fresh inputs |
| Cedar Grove Foods (`cedargrove`) | `A2` | `A (mid)` | `A` | 6 / 6 / 6 | **CONSENSUS** control — spread 0, must yield **zero** flags |
| Meridian Freight (`meridian`) | `Baa3` | `BB (high)` | `BBB-` | 10 / 11 / 10 | Fallen-angel — straddles IG/HY (`BBB-`↔`BB+`), UI highlight |
| Aster Bio (`asterbio`) | `Ba2` | `BB (mid)` | *(absent)* | 12 / 12 / — | **MISSING_COVERAGE** — MSCI not present (2 cards) |
| Onyx Capital (`onyx`) | `A2` | `A (mid)` | `BBB-` | 6 / 6 / 10 | **OUTLIER_PROVIDER** — MSCI ≥3 notches from median 6 |

Doc inventory: **6 issuer** + **17 rating cards** (3+3+3+3+2+3) + **3 methodology** (one per provider)
+ **4 reference** (notch-scale glossary, as-of/observation policy, divergence taxonomy, data-quality
provenance) = **30**. "Curation beats volume" (spec) — the count meets `~30–50`.

Dates (coherent story): fresh inputs `inputAsOfDate` ≈ 2025-10-15..2025-11-01, rating `asOfDate`
2025-09..2025-11; NordStar MSCI is the deliberate stale one (`inputAsOfDate` 2025-08-20). Issuer docs
carry `latestFilingDate` (NordStar = 2025-11-05 Q3 10-Q) — the value the stale rule compares against.

Every card: `factors[]` each with a `sourceRef` (→ methodology doc id or issuer fundamentals ref) and
weights summing ≈ 1; `inputAsOfDate` present (acceptance 1). **No P4 vocabulary** anywhere in content
or fields (tool + tests grep-assert `buy|sell|hold|recommend|allocate|trade|alpha|signal`).

## 4. Index schema (`prism-ratings`, spec §C + P1 labeling)

| Field | Type | Attributes |
|---|---|---|
| `id` | String | key |
| `docType` | String | filterable, facetable (`ratingCard`/`methodology`/`reference`/`issuer`) |
| `issuerId` | String | filterable, facetable |
| `provider` | String | filterable, facetable (empty for non-card docs) |
| `letter` | String | filterable (card letter; empty otherwise) |
| `notch` | Int32 | filterable, sortable (card notch; 0 otherwise) |
| `dataClass` | String | filterable, facetable (`synthetic`/`illustrative`) — P1 machine-readable label |
| `asOfDate` | DateTimeOffset | filterable, sortable |
| `inputAsOfDate` | DateTimeOffset | filterable, sortable |
| `content` | String | searchable (standard analyzer) |
| `contentVector` | Collection(Single) | searchable, dims 1536, HNSW profile + Azure OpenAI vectorizer |

- Vector: HNSW algorithm config + a vector-search *profile*; the profile carries an
  `AzureOpenAIVectorizer` → the `text-embedding-3-small` deployment, so **query-time integrated
  vectorization uses the same model as index time** (HACKATHON-FINDINGS §6 — matched vectorizer).
- Semantic: one `SemanticConfiguration` (content field = `content`; keyword = `issuerId`).
- Hybrid = keyword + vector + semantic ranking.

## 5. Files to create

### `tools/SeedData/`
1. `SeedData.csproj` — `Microsoft.NET.Sdk`, net9.0, `OutputType=Exe`, Nullable + warnaserror,
   `LangVersion=latest`. Packages: `Azure.Search.Documents` 11.6.0, `Azure.AI.OpenAI` 2.1.0,
   `Azure.Identity` 1.13.1, `Microsoft.Extensions.Hosting` 9.0.x. ProjectReference →
   `..\..\backend\FinancialServices.Api\FinancialServices.Api.csproj`. Corpus JSON → `CopyToOutputDirectory`.
2. `corpus/**/*.json` — 30 docs (`corpus/issuers`, `corpus/cards`, `corpus/methodology`, `corpus/reference`).
3. `Model/CorpusDoc.cs` — deserialization record (nullable-correct; `factors` optional).
4. `Corpus/CorpusLoader.cs` — loads + parses all JSON from the corpus dir; fail-loud on malformed/empty (P1).
5. `Corpus/CorpusValidator.cs` — **pure** invariant checks (reused by tool + tests): unique ids;
   card `notch == NotchLadder.ToNotch(letter)`; every card `dataClass=="synthetic"` + non-empty
   `disclaimer`; every card has `inputAsOfDate` + every factor a non-empty `sourceRef`; NordStar MSCI
   `inputAsOfDate` < NordStar issuer `latestFilingDate`; Cedar Grove 3 cards within 1 notch; provider/
   docType parse to known enums; no P4 vocabulary. Throws `CorpusValidationException` listing all failures.
6. `Search/PrismSearchIndex.cs` — builds the `SearchIndex` (fields + vector + vectorizer + semantic)
   from `AzureSeedOptions`.
7. `Search/SeedRunner.cs` — orchestrates: validate → `CreateOrUpdateIndexAsync` → embed each
   `content` via `EmbeddingClient` (dims 1536) → `MergeOrUpload` upsert (idempotent) → structured
   logs (ids + counts, no doc bodies). `CancellationToken` plumbed everywhere (P7).
8. `Search/SearchVerifier.cs` — the `--verify` acceptance queries: hybrid "NordStar leverage"
   (`VectorizableTextQuery`) ⇒ asserts `nordstar-msci` present with `inputAsOfDate`/`asOfDate`/
   `provider` intact; Cedar Grove filter ⇒ 3 cards within 1 notch; doc count == 30 (idempotency proof).
9. `Configuration/AzureSeedOptions.cs` — bound from `Azure` section: `SearchEndpoint`, `SearchIndex`,
   `OpenAiEndpoint`, `EmbeddingDeployment` (default `text-embedding-3-small`), `EmbeddingModel`,
   `EmbeddingDimensions` (1536). `Validate()` fails loud naming any missing setting (P1/arch 05).
10. `Program.cs` — `Host.CreateApplicationBuilder` (env + args config, console logging, options);
    args: `seed` (default, live), `--dry-run` (offline: load+validate+print plan, no Azure),
    `--verify` (run acceptance queries). `DefaultAzureCredential` (P6, no keys). Ctrl+C → cancellation.
    Exit non-zero on any failure.

### `tools/SeedData.Tests/`
11. `SeedData.Tests.csproj` — xUnit 2.9.2, xunit.runner.visualstudio 2.8.2, Microsoft.NET.Test.Sdk
    17.12.0, FluentAssertions `[6.12.1,7.0.0)` (match backend pins). Nullable + warnaserror.
    ProjectReference → `SeedData.csproj`.
12. `CorpusValidatorTests.cs` — loads the real shipped corpus; asserts: 30 docs; all invariants pass;
    NordStar stale relationship; Cedar Grove within-1-notch; every card labeled synthetic + disclaimer;
    P4 vocabulary absent across all content; notch↔letter consistency; unique ids; a mutated fixture
    (bad notch / missing disclaimer / stale-inverted / P4 word) makes the validator throw.
13. `PrismSearchIndexTests.cs` — schema asserts: key `id`; `contentVector` is Collection(Single) dims
    1536; vectorizer references the configured deployment; semantic config present; expected filterable/
    sortable flags. No Azure network.

### Edits
14. `backend/FinancialServices.slnx` — add the two `tools/` projects.
15. `.env.example` — add `Azure__EmbeddingDeployment`/`Azure__EmbeddingModel`/`Azure__EmbeddingDimensions`
    (+ note `Azure__OpenAiEndpoint` is reused for embeddings; auth via `az login`/`DefaultAzureCredential`).

## 6. Phases & gates

- **P2 Implement:** create files → `dotnet build backend/FinancialServices.slnx` (0 warn) →
  `dotnet test backend/FinancialServices.slnx` (123 existing + new, all green) →
  `dotnet run --project tools/SeedData -- --dry-run` (offline validate + plan). Log to
  `.copilot-tracking/changes/2026-07-06/03-changes.md`.
- **P3 Adversarial:** Standards (P1–P8, acceptance, labeling), Architect (layering, failure modes,
  idempotency, matched vectorizer), Security (DefaultAzureCredential/no keys, no PII in logs,
  SSRF/endpoint trust, corpus as untrusted input), Stack Critic (Azure.Search.Documents 11.6 +
  Azure.AI.OpenAI 2.1 API accuracy, dims, semantic API, pinning). → `.../reviews/2026-07-06/03-review.md`.
- **P4 Correct:** fix all Critical/High + feasible Medium; re-run build/tests; loop P3↔P4 ≤ 2.
- **Live gate:** decide `text-embedding-3-small` deployment (cheap, reversible, spec-required). If
  deployed → `seed` + `--verify` against live Search (acceptance 3–5). Else → report named missing
  config; mark items 3–5 blocked-on-deployment; items 1–2 + idempotency-by-construction still pass.
- **P5 Reviewer:** validate all 5 acceptance items + arch conformance + build/tests green.
- **P6 Track:** check TASKS.md items; append Implementation Log entry; update README status row.

## 7. Acceptance → coverage map

| Acceptance item | Covered by |
|---|---|
| ~30–50 docs; every card `inputAsOfDate` + factor `sourceRef`s | 30 corpus docs; `CorpusValidator` + tests |
| NordStar MSCI `inputAsOfDate` (Q2) < NordStar Q3 filing date | data invariant + `CorpusValidator` + test (real-EDGAR anchoring deferred) |
| Index created; hybrid "NordStar leverage" returns MSCI card w/ metadata | `PrismSearchIndex` + `SeedRunner` + `SearchVerifier` (live; gated on embedding deployment) |
| Cedar Grove returns 3 cards within 1 notch | corpus {6,6,6}; `SearchVerifier` filter + `CorpusValidator` + test |
| `tools/SeedData` re-run idempotent | `CreateOrUpdateIndex` + `MergeOrUpload` by id; `--verify` count==30 |

## 8. Risks

- **R1 (gating):** no embedding deployment → live items blocked until `text-embedding-3-small` deployed.
- **R2:** Azure.Search.Documents 11.6 vector/vectorizer/semantic API surface — verify exact type names
  at implement time (Stack Critic).
- **R3:** corpus/pkg05-fixture date drift — keep NordStar/CedarGrove/AsterBio/Onyx notch profiles aligned.
- **R4:** doc count 30 is the low end of `~30–50` — justified by "curation beats volume"; flag as Low.
