# ⚔️ Fin Adversary Architect: Prism Work-Package 08 — API & Persistence

> Adversarial architecture red-team. Attacks the design; strengths kept brief and honest.
> Scope: the REST surface + Cosmos persistence + audit trail that shipped for pkg 08.
> Overlaps with already-filed pkg-07 findings (idempotency, auth) are flagged and de-duplicated;
> the focus is **08-specific persistence/API design flaws**.

---

## VERDICT (one line)

**Demo: GO-WITH-FIXES** (works live end-to-end, but every re-run silently piles up duplicate Cosmos
docs and the audit trail is non-atomic) — **Prod: NO-GO** (no idempotency, no TTL, non-transactional
audit, `Actor:"system"`, no auth/rate-limit inherited from pkg 07).

---

## Threat Model Summary

- **Topology attacked:** browser → C# API `:8000` (`ReconciliationsController` / `IssuersController`)
  → `ReconciliationService` → **Cosmos** (`rating_reconciliations`, `audit_events`, both pk `/issuerId`)
  + **AI Search** (`prism-ratings`). The SSE `/stream` path (pkg 07) re-enters the same
  `IReconciliationService.RunAsync`, so it inherits every persistence flaw below.
- **Highest-risk assumption:** *"Documents are immutable once written; regenerate = new id, don't
  mutate"* (arch-08). Followed literally, this **guarantees** an unbounded, hot single-partition
  dossier pile-up with no canonical record — the design doc is itself the defect.

---

## Findings

### [ARC-08-01] Non-idempotent write mints a duplicate dossier **and** a duplicate audit on every re-run — Severity: **Critical**
- **Target:** [backend/FinancialServices.Api/Services/ReconciliationService.cs](../../../backend/FinancialServices.Api/Services/ReconciliationService.cs#L116) `NewPartitionedId` = `$"{issuerId}:{Guid.NewGuid():N}"`; `RunAsync` unconditionally [`store.UpsertAsync`](../../../backend/FinancialServices.Api/Services/ReconciliationService.cs#L82) then a fresh-guid audit; also reached via [PrismStreamingOrchestrator.cs](../../../backend/FinancialServices.Api/Orchestration/PrismStreamingOrchestrator.cs#L95) step 4 `reconciliation.RunAsync`.
- **Attack:** the id is `issuerId` + a **fresh GUID per call** — never derived from `(issuerId, asOf)`. The frontend auto-runs the POST on mount (`useReconciliationRun`) and a page refresh re-fires it; a demo operator clicks *Reconcile NordStar* repeatedly. Each `POST /reconciliations` **and** each `POST /reconciliations/stream {confirmed:true}` writes a **new immutable dossier doc + a new audit doc**, all on the **same logical partition** `/issuerId=nordstar`. `UpsertItemAsync` with an always-new id is a pure insert; nothing dedupes.
- **Impact:** (Reliability/Cost) hot single-partition write amplification and RU burn; the 20 GB logical-partition ceiling accrues **per issuer** with no bound; and — worst for a *reconciliation/data-quality* tool — the store holds **N contradictory "as-of 2026-07-06" dossiers for the same issuer with no canonical one**. A client retry after a lost `200` silently double-persists. Acceptance *"GET /{id} round-trips"* passes yet hides that there is **no stable id to GET** — you can only fetch the specific guid a given response happened to return.
- **Hardening:** deterministic id `"{issuerId}:{asOf:yyyyMMdd}"` (or `+contentHash`) so a re-run is an **idempotent overwrite of the same document**; add a **Cosmos TTL** on `rating_reconciliations` (dossiers are regenerable) while keeping `audit_events` permanent; accept an `Idempotency-Key` header on `POST`. This requires **amending arch-08** — its "immutable + new id + (implicit) no TTL" rule is the root cause, not the code.
- **Best-practice reference:** WA Reliability & Cost Optimization; Cosmos "avoid hot partitions / bound growth."

### [ARC-08-02] Dossier and its audit event are two **non-atomic** Cosmos writes — the financial audit trail can silently drop the audit — Severity: **Critical** (High for a single demo run)
- **Target:** [ReconciliationService.cs](../../../backend/FinancialServices.Api/Services/ReconciliationService.cs#L82) `await store.UpsertAsync(dossier, ct)` **then** [`await audit.WriteAsync(...)`](../../../backend/FinancialServices.Api/Services/ReconciliationService.cs#L85) — two independent point writes, no batch.
- **Attack:** if `UpsertAsync` succeeds and `WriteAsync` hits a `429`/timeout/cancel, the **dossier persists with no audit event**. `WriteAsync` throws `UpstreamServiceException` → client gets `502` → client retries → `RunAsync` mints a **new** guid → now **two dossiers, one audit**. The audit trail — a stated §C requirement *and* the financial-domain rule *"every financial data mutation must log"* — is **best-effort and skewed** vs the dossier set.
- **Impact:** (Compliance/Reliability) non-repudiation gap and audit undercount on the exact requirement this package calls out. Note the export path writes audit **before** returning HTML, so an audit hiccup also blocks a **read-only** export (availability coupling).
- **Hardening:** `dossier` and its `AuditEvent` **share the partition key `/issuerId`** — this is the rare case where Cosmos gives you a real transaction. Use a `TransactionalBatch` on that partition to write both atomically. (This same fix composes with ARC-08-01's deterministic id.)
- **Best-practice reference:** WA Operational Excellence; Cosmos `TransactionalBatch` (single-partition ACID).

### [ARC-08-03] §C audit is **incomplete** — "scope-confirmed" is never persisted — Severity: **High**
- **Target:** [PrismStreamingOrchestrator.cs](../../../backend/FinancialServices.Api/Orchestration/PrismStreamingOrchestrator.cs#L46) emits `ScopeConfirm`/`AwaitingApproval` **events** but writes **no** `audit_events` doc; the REST [`Post`](../../../backend/FinancialServices.Api/Controllers/ReconciliationsController.cs#L34) has no scope-confirm audit. Only `dossier_generated` ([ReconciliationService.cs L85](../../../backend/FinancialServices.Api/Services/ReconciliationService.cs#L85)) and `dossier_exported` ([ReconciliationsController.cs L124](../../../backend/FinancialServices.Api/Controllers/ReconciliationsController.cs#L124)) are audited.
- **Attack/impact:** spec §C requires audit on *"scope confirmed, dossier generated, dossier exported"* → **1 of 3 is unaudited**. P5's human approval — the single consequential gate — leaves **no audit record**. You cannot prove *who approved a sweep*.
- **Hardening:** write an `audit_events` doc `Action:"scope_confirmed"` when `confirmed==true` in the orchestrator; for REST, either treat `POST /reconciliations` as an implicit confirm (audit it) or add an explicit confirm step.
- **Best-practice reference:** financial-domain "Human-in-the-Loop Gates" + audit-trail requirement.

### [ARC-08-04] `Actor:"system"` hardcoded — the audit trail cannot answer "who" — Severity: **High** (prod; latent behind missing auth)
- **Target:** [ReconciliationService.cs L89](../../../backend/FinancialServices.Api/Services/ReconciliationService.cs#L89) `Actor: "system"`; [ReconciliationsController.cs L120](../../../backend/FinancialServices.Api/Controllers/ReconciliationsController.cs#L120) `Actor: "system"`.
- **Attack/impact:** the financial-domain `AuditEvent` shape mandates an actor/advisor id; every event is stamped `"system"` in **two** call sites → non-repudiation impossible the moment this leaves localhost. Overlaps pkg-07 SEC-07 but is baked into pkg-08 write paths.
- **Hardening:** source `Actor` from `HttpContext.User` once auth lands (pkg 07/11); thread it into `RunAsync`/the audit write. Keep `"system"` only for genuinely unattended jobs.

### [ARC-08-05] The rating cards are fetched **twice** per reconciliation (redundant Search round-trips + TOCTOU) — Severity: **High** (perf + correctness)
- **Target:** [ReconciliationService.cs L31](../../../backend/FinancialServices.Api/Services/ReconciliationService.cs#L31) `corpus.GetIssuerAsync` issues **2** Search queries (issuer row + all rating cards, to compute coverage — see [SearchCorpus.cs GetIssuerAsync](../../../backend/FinancialServices.Api/Services/SearchCorpus.cs#L39)); then [L36](../../../backend/FinancialServices.Api/Services/ReconciliationService.cs#L36) `corpus.GetProviderRatingsAsync` issues a **third** query re-fetching the **same** cards ([SearchCorpus.cs L63](../../../backend/FinancialServices.Api/Services/SearchCorpus.cs#L63)). The `/stream` path re-queries per provider in `PrismSweepSteps` **and then** runs step-4 `RunAsync`, repeating all of this.
- **Attack/impact:** ≈3 Search calls per REST reconciliation (≥6 on stream), each a network round-trip and RU spend; and a **TOCTOU** window — coverage from the first fetch can disagree with the ratings from the second if the index changes between calls (rehearsal re-runs `tools/SeedData` live). Wasteful and non-deterministic under a concurrent re-seed.
- **Hardening:** fetch the issuer + its cards **once** and pass the cards to both coverage and rating mapping — e.g. `ISearchCorpus.GetIssuerWithCardsAsync(issuerId, asOf)` returning `(entry, ratings)`.
- **Best-practice reference:** WA Performance Efficiency (eliminate N+1 / repeated reads).

### [ARC-08-06] `Providers` scope is **accepted then silently dropped** — Severity: **High** (API contract / P5 governance)
- **Target:** [PrismDtos.cs](../../../backend/FinancialServices.Api/Models/PrismDtos.cs#L74) `ReconciliationRequest.Providers` (and `ReconciliationStreamRequest.Providers`) vs [ReconciliationsController.cs L36](../../../backend/FinancialServices.Api/Controllers/ReconciliationsController.cs#L36) `service.RunAsync(request.IssuerId!, request.AsOf!.Value, ct)` — `Providers` is never passed; `IReconciliationService.RunAsync` has no providers parameter; the sweep always uses full `entry.Coverage`.
- **Attack/impact:** a client that scopes to `{Moodys, MorningstarDbrs}` gets a **full-coverage** sweep including MSCI anyway. For a tool whose P5 gate literally asks the user to *confirm the scope*, approving a narrowed scope the server then **ignores** is a governance lie, not just dead input. (Overlaps pkg-07 ARC-04; here it's a concrete REST-surface bug.)
- **Hardening:** either **honor** `Providers` (filter `Coverage` server-side, re-authorize each, `400` on a provider outside coverage) or **remove** the field until supported — fail-loud/absent beats silent-ignore.

### [ARC-08-07] `UpsertItemAsync` contradicts the stated immutability invariant; audit uses `CreateItemAsync` — inconsistent semantics — Severity: **Medium**
- **Target:** [CosmosDossierStore.cs L33](../../../backend/FinancialServices.Api/Services/CosmosDossierStore.cs#L33) `_container.UpsertItemAsync(...)` vs [AuditService.cs L31](../../../backend/FinancialServices.Api/Services/AuditService.cs#L31) `_container.CreateItemAsync(...)`.
- **Attack/impact:** arch-08 says dossiers are *immutable once written*, but `Upsert` does **not** enforce it — if an id ever collides (or you adopt a deterministic id per ARC-08-01) it **silently overwrites**; `Create` would `409` and preserve history. Two "immutable" stores, two semantics.
- **Hardening:** pick one and make both consistent. If you adopt deterministic ids you *want* idempotent `Upsert` — then drop the "immutable" wording. If you keep guids, use `CreateItemAsync` so a collision is a loud `409`, not a silent replace.

### [ARC-08-08] Silent data-quality fallback on the **exact field that drives the money-moment flag** — Severity: **Medium** (borderline P1 violation)
- **Target:** [SearchCorpusMapper.cs ToIssuerEntry](../../../backend/FinancialServices.Api/Services/SearchCorpusMapper.cs#L140) `DateTimeOffset filingBoundary = issuerRow.LatestFilingDate ?? issuerRow.AsOfDate;` and `filingType = string.IsNullOrWhiteSpace(...) ? "filing" : ...`.
- **Attack:** if the live `prism-ratings` index has `latestFilingDate` unpopulated/misnamed (a **real** risk — pkg 08 *extended* the index and the mapper comment admits *"fall back… if not yet populated"*), the STALE-input boundary silently becomes the issuer doc's `asOfDate` — a **semantically different date** — so the flag that *is the demo* can fire/not-fire on the wrong basis with **no error**. That is a silent fallback in a P3 as-of-correctness path; P1 demands fail-loud.
- **Hardening:** for `docType=='issuer'`, if `latestFilingDate` is null, `throw new UpstreamServiceException("Search", "issuer row missing latestFilingDate")` — never substitute a different date behind the flag.

### [ARC-08-09] An empty / mis-pointed index returns `200 []`, not a fault — silent degradation — Severity: **Medium**
- **Target:** [SearchCorpus.cs GetIssuersAsync](../../../backend/FinancialServices.Api/Services/SearchCorpus.cs#L22) returns whatever came back (empty → empty); [QueryAsync](../../../backend/FinancialServices.Api/Services/SearchCorpus.cs#L104) only throws on `RequestFailedException`.
- **Attack/impact:** point `Azure:SearchIndex` at a wrong-but-existing (or un-seeded) index → queries succeed with zero rows → `GET /api/v1/issuers` returns `200 []`; the UI shows *"no issuers."* Acceptance *"from real Search (no mock)"* is technically met, but a **misconfiguration is indistinguishable from an empty cast** — P1 fail-loud is bypassed.
- **Hardening:** the curated corpus is never legitimately empty — if `GetIssuersAsync` yields zero issuer rows, log + throw a configuration/upstream fault, or surface index doc-count on `/api/health`.

### [ARC-08-10] Cosmos `429` mapped to `502` (no Retry-After); read-your-write breaks across replicas — Severity: **Medium** (prod)
- **Target:** [CosmosDossierStore.cs L37/L57](../../../backend/FinancialServices.Api/Services/CosmosDossierStore.cs#L37) — every `CosmosException` (incl. `429`/`503`/`408`) becomes `UpstreamServiceException` → `502` in the handler.
- **Attack/impact:** once the SDK's built-in `429` retries exhaust, the client sees `502 Bad Gateway` (wrong class; should be `503 + Retry-After`). Separately, with default **Session** consistency and a scaled-out ACA (one singleton `CosmosClient` per replica), `GET /{id}` on replica B immediately after a write on replica A can `404` a real dossier (session token not shared). Latent on a single-replica demo; a live defect on pkg-11 ACA.
- **Hardening:** map `429/503`→`503` with `Retry-After`; for read-your-write, pin/propagate the session token, use bounded staleness, or read from the write region.

### [ARC-08-11] SSE `/stream` returns HTTP `200` for NotFound/validation (in-band error) — Severity: **Low** (pkg-07 overlap)
- **Target:** [ReconciliationsController.cs Stream L52](../../../backend/FinancialServices.Api/Controllers/ReconciliationsController.cs#L52) sets `Response.StatusCode = 200` **before** the run; `NotFound`/`Validation` are emitted as `error` events at `200`. The REST `POST` is correct (throws → proper status via the handler). Monitoring sees only `200`s for the stream. Already filed as pkg-07 ARC-09; the 08 surface inherits it.

### [ARC-08-12] `DossierResponse` drops `Fundamentals` + `GeneratedAt`; `POST` doc omits its real status codes — Severity: **Low**
- **Target:** [PrismDtoMappings.cs ToResponse](../../../backend/FinancialServices.Api/Models/PrismDtoMappings.cs#L15) omits `Fundamentals`/`GeneratedAt`; [ReconciliationsController.cs Post](../../../backend/FinancialServices.Api/Controllers/ReconciliationsController.cs#L29) declares only `[ProducesResponseType]` `200`/`400` though it genuinely returns `404` (unknown issuer) and `502` (Search/Cosmos). Cosmetic contract drift — the persisted doc keeps both fields; the export renderer reads the full doc from Cosmos. Document the omission / add the response types.

### [ARC-08-13] `MaxCorpusRows = 1000` silent truncation + client-side full-scan grouping — Severity: **Low**
- **Target:** [SearchCorpus.cs L18](../../../backend/FinancialServices.Api/Services/SearchCorpus.cs#L18) `MaxCorpusRows`; `GetIssuersAsync` pulls **all** issuer rows + **all** rating cards and groups coverage in memory ([BuildCoverage](../../../backend/FinancialServices.Api/Services/SearchCorpus.cs#L72)). Fine for the curated corpus; a silent cap + full-scan if it ever grows. arch-08 explicitly suggests *"AI Search facet on `issuerId`"* — prefer a facet query over client-side grouping.

---

## Design Strengths (kept honest — brief)
- **Clean controller/service/DTO layering.** Controllers are thin; wire DTOs (`PrismDtos`) are separate from domain records (`PrismModels`); mapping is isolated and pure (`PrismDtoMappings`). **P2 holds on the REST path** — `RunAsync` composes via `DossierAssembler` (the sole composer) and never re-implements notch/gap/flag math (unlike the AG-UI orchestrator — pkg-07 ARC-03).
- **Genuinely consistent error envelope.** `PrismExceptionHandler` maps the whole taxonomy → `{error:{code,message,details}}`, model-validation is overridden to the **same** shape, and the fallback is a generic message with **no stack/internal leak** (P6).
- **Sound persistence hygiene.** Point read/write **always** pass `PartitionKey` (no cross-partition scans); the id encodes the partition so `GET /{id}` is a true point read; Cosmos faults fail loud as `UpstreamServiceException`; a `404` read → `null` → `404` (correct). `CancellationToken` is plumbed through every Cosmos/Search call (P7). OData filter input is single-quote-escaped (P6). Enum-as-string + camelCase wired once (`PrismJson`) so the wire contract matches `frontend/src/types/prism.ts`.
- **Real-services posture.** `GET /issuers` reads the **live** Search corpus (no mock list, no in-memory seed in runtime); `DefaultAzureCredential`-only (no keys). Narration is best-effort and **never** mutates the deterministic dossier (P1/P2).

---

## Acceptance-criteria checklist

| # | Criterion | Status | Notes |
|---|---|---|---|
| 1 | `GET /issuers` from real Cosmos/Search, no mock | **MET** | Live `SearchCorpus`; caveat ARC-08-09 (empty/mis-pointed index silently `200 []`). |
| 2 | `POST /reconciliations` (NordStar) persists a dossier w/ `STALE_INPUT` | **MET** | Live-verified + `ReconciliationServiceTests`; but **non-idempotent** (ARC-08-01). |
| 3 | `GET /{id}` round-trips the persisted dossier | **MET** | id-encoded partition point read (test + live); but **no canonical id** — dupes accrue. |
| 4 | Every run writes ≥1 audit; no PII/financials in logs | **PARTIAL** | `dossier_generated` ✓, `dossier_exported` ✓, **`scope_confirmed` ✗** (ARC-08-03); audit **non-atomic** (ARC-08-02); `Actor:"system"` (ARC-08-04). Log hygiene ✓ (ids+counts). |
| 5 | Error responses match the standard shape | **MET** | `PrismExceptionHandler` + model-state override; minus SSE in-band `200` (ARC-08-11). |

**Test-coverage gap (arch-11):** `ReconciliationServiceTests` asserts `store.Count == 1` after **one** run and never re-runs — so **non-idempotency (ARC-08-01) is completely unpinned**; there is no test for the export audit, `scope_confirmed`, the partial-write path (ARC-08-02), or a `WebApplicationFactory` integration test exercising the real error envelope. Acceptance #5 is verified live only, not by a test.

---

## Top 3 Must-Fix
1. **ARC-08-01** — deterministic `(issuerId, asOf)` id + **Cosmos TTL** on `rating_reconciliations` (+ a rate limiter before this ever runs anonymously): stop the hot-partition dossier flood and give the store a canonical record.
2. **ARC-08-02** — write dossier + audit in a **single `TransactionalBatch`** on the shared `/issuerId` partition: guarantee the audit trail can't be silently dropped.
3. **ARC-08-03 / ARC-08-04** — audit `scope_confirmed` **and** source `Actor` from the authenticated principal: make the §C / financial-domain audit **complete and attributable**.

---

### Overlap note (07 ↔ 08)
Idempotency (ARC-08-01) and anonymous/unthrottled access are the persistence/API face of pkg-07 **ARC-02** and **SEC-01**; `Actor:"system"` and read/export auth are pkg-07 **SEC-07**; the SSE `200`-on-error is pkg-07 **ARC-09**. This review adds the **08-specific** angles those did not: the **`TransactionalBatch` opportunity** the shared partition key makes available (ARC-08-02), the **double Search fetch / TOCTOU** (ARC-08-05), the **`Upsert` vs `Create` immutability inconsistency** (ARC-08-07), the **silent `latestFilingDate` fallback behind the money-moment flag** (ARC-08-08), and the **empty-index silent `200 []`** (ARC-08-09).
