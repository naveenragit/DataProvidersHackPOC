# Package 03 — Adversarial Review (consolidated)

**Date:** 2026-07-06 · **Under review:** `.copilot-tracking/changes/2026-07-06/03-changes.md`
**Lenses:** Prism Standards Adversary · Fin Adversary Architect · Fin Adversary Security · Fin Adversary Stack Critic
**Verdict:** **PASS (conditional)** — no Critical; **1 High** (live-robustness) + 3 Medium + 3 Low, all fixed in Phase 4.
The offline package (corpus + tool + tests) is sound; build 0-warn, 144 tests green. Live acceptance
(items 3–5) is gated on a `text-embedding-3-small` deployment (named missing config, not a code defect).

---

## Findings

### A03-01 — [High · Architect/Stack] `--verify` acceptance query depended on the Search-service managed identity
`Search/SearchVerifier.cs` used `VectorizableTextQuery("NordStar leverage")`, which asks the **Search
service** to embed the query via the index's Azure OpenAI vectorizer. That requires the Search
service's managed identity to hold *Cognitive Services OpenAI User* on the Foundry/AOAI resource — an
RBAC grant that is **not** part of package 03's setup. If absent, the acceptance query fails at
query time (or, worse, a misconfigured vectorizer degrades silently — HACKATHON-FINDINGS §6). The
seed path already embeds **client-side** with the operator's `DefaultAzureCredential` (which has the
roles), so the two paths had different trust requirements.
**Fix (Phase 4):** the verifier embeds the query **client-side** with the same
`text-embedding-3-small` deployment (matched model) and issues a `VectorizedQuery`. The index
vectorizer is still configured (spec §C, for the app's future integrated-vectorization path) and its
presence is asserted via `GetIndex`, but the acceptance query no longer depends on the Search MI.

### A03-02 — [Medium · Standards] Config-missing error used `InvalidOperationException`
`Configuration/AzureSeedOptions.ValidateForSeeding()` threw `InvalidOperationException`. The repo has
an error taxonomy (architecturalPlan/03) with `FinancialServices.Api.Infrastructure.Errors.ConfigurationException`
(already referenced transitively). Using it makes the fail-loud-on-missing-config path consistent with
the API (P1/arch 03/05).
**Fix (Phase 4):** throw `ConfigurationException` from `ValidateForSeeding()`.

### A03-03 — [Medium · Architect/Stack] Embedding-dimension coupling (silent-degradation trap)
Integrated (query-time) vectorization via `AzureOpenAIVectorizer` uses the model's **default** output
dimension (1536 for `text-embedding-3-small`), while the client-side seed embedding passes
`Dimensions = EmbeddingDimensions`. If someone set `Azure__EmbeddingDimensions` to anything other than
1536, the seed vectors and the (future) integrated-vectorization query vectors would silently differ
in length — the exact §6 trap. Latent today (default is 1536).
**Fix (Phase 4):** document the coupling on `AzureSeedOptions.EmbeddingDimensions`; the verifier
already asserts the index field dimension equals `EmbeddingDimensions`, catching a field/config
mismatch loudly. (With A03-01, the *acceptance* path is client-side and self-consistent regardless.)

### A03-04 — [Low · Standards] P4 vocabulary scan omitted issuer `LegalName`/`Sector`
`Corpus/CorpusValidator` scanned `content`, `disclaimer`, `letter`, and factor names, but not the
issuer-only `LegalName`/`Sector`. No current issuer name trips it, but defense-in-depth is cheap.
**Fix (Phase 4):** include `LegalName`, `Sector`, `FilingType` in the scanned text.

### A03-05 — [Low · Standards] `dataClass` not asserted for non-card docTypes
The validator enforced `dataClass=="synthetic"` on cards only. Issuer docs should be `synthetic`;
methodology/reference should be `illustrative` (P1 — labels are the honesty contract).
**Fix (Phase 4):** assert `issuer → synthetic`, `methodology|reference → illustrative`.

### A03-06 — [Low · Standards] Doc count is exactly 30 (low end of "~30–50")
Acceptance says "~30–50 docs authored." 30 meets it; the spec itself says "curation beats volume."
**Resolution:** accepted as-is; recorded as a residual (add peers only if a later package needs them).

---

## Attacked and found safe (no action)

- **P1 fail-loud:** `seed` requires config (`ValidateForSeeding`); a rejected upload throws; embeddings
  are all-computed-then-uploaded (atomic-ish, no partial index write). No silent fallback; `--dry-run`
  is an explicit offline mode, not a degrade path.
- **P1 labeling:** every card `dataClass:"synthetic"` + disclaimer; the machine-readable `dataClass`
  field is indexed and filterable.
- **P2:** notch math reused from `NotchLadder` (no re-implementation); no LLM in this package.
- **P4:** regex `\b(buy|sell|hold|recommend|allocate|trade|alpha|signal)\b` (word-boundary, so
  "Holdings"/"bondholder" are safe); enforced + mutation-tested. Manual sweep of all 30 docs clean.
- **P6:** `DefaultAzureCredential`, no keys in code/config; logs ids + counts only, never doc bodies;
  the embedded content is synthetic (no PII/financials); OData filter is a hardcoded literal (no injection);
  endpoints are operator config, not user input (no SSRF).
- **P7:** `CancellationToken` plumbed through `SeedAsync`/`VerifyAsync`, embeddings, upload, and every
  `await foreach` (`WithCancellation`); Ctrl+C wired to a `CancellationTokenSource`.
- **P8:** builds on the existing API project + `NotchLadder`.
- **Idempotency:** `CreateOrUpdateIndex` (schema upsert) + `MergeOrUpload` (doc upsert by id); the
  verifier asserts count == 30 after any number of runs.
- **Stack:** Azure.Search.Documents 11.6.0 + Azure.AI.OpenAI 2.1.0 surfaces confirmed against Microsoft
  Learn samples and the compiler; versions pinned; net9.0 interop clean (tool refs the Web-SDK API project).

## Live-gate blocker (environment, not code)

`foundry-dataproviders-poc` has only `gpt-5.4` deployed — **no `text-embedding-3-small`**. Acceptance
items 3–5 (index created + hybrid query + Cedar Grove query + idempotency, all against live Search)
require an embedding deployment. Named missing config per the workflow STOP condition; resolved at the
live gate by deploying `text-embedding-3-small` (cheap, reversible) then running `seed` + `--verify`.
