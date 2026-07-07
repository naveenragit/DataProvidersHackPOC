# 🛡️ Fin Adversary Security: Prism Package 08 — API & Persistence

**VERDICT:** Demo **GO** (localhost + synthetic corpus only) / Production **NO-GO** — unauthenticated,
unthrottled read **and write** to Cosmos; audit trail is non-attributable (`Actor:"system"`), the P5
scope gate is never audited, and dossier writes are non-atomic with their audit records.

Severity counts: **1 Critical · 4 High · 3 Medium · 3 Low** (11 findings).

Scope of this review: persistence + data-access security of package 08 (`Controllers/`, `Services/`
Cosmos + Search + Audit + HTML export, `Models/PrismDtos.cs` + `AuditEvent`, `Infrastructure/` error
handler + Azure client wiring, `Program.cs`). The prior 07 review already owns the paid-LLM economic-DoS
angle (SEC-01 there); here it is restated only where persistence multiplies the blast radius.

---

## Trust Boundary Map

| Boundary | What protects it today | Gap |
|---|---|---|
| Browser / any client → API `:8000` (`POST /reconciliations`, `/stream`, `GET /{id}`, `/{id}/export`, `GET /issuers`) | **Nothing.** `[AllowAnonymous]` on both controllers; `Program.cs` has no `AddAuthentication`/`UseAuthorization`/`AddRateLimiter`/`RequireAuthorization` (grep-confirmed: 0 in `FinancialServices.Api`) | **Total** — anonymous read **and** persist |
| API → Cosmos (`rating_reconciliations`, `audit_events`) | `DefaultAzureCredential`; **point** reads/writes with the partition key (no SQL text) | Over-broad data-plane RBAC (can delete audits); no TTL; no idempotency → unbounded anonymous writes |
| API → AI Search (`prism-ratings`) | `DefaultAzureCredential`; OData single-quote escaping on every issuerId filter | No length/charset cap on issuerId; raw issuerId reaches logs (CWE-117) |
| Client → dossier capability URL (`GET /{id}`, `/export`) | A 122-bit CSPRNG guid in the id → **not** enumerable | No auth, no expiry, no per-user binding → leaked id = permanent read |
| API → `{ error }` envelope | Unknown exceptions → generic message, **no stack traces** | 5xx `details` leak service/config/DTO-type names |

---

## Findings

### [SEC-01] Unauthenticated read + write to every persistence endpoint — Severity: **Critical**
- **Boundary / Target:** `Controllers/ReconciliationsController.cs` L20 (`[AllowAnonymous]`),
  `Controllers/IssuersController.cs` L17 (`[AllowAnonymous]`); `Program.cs` L1–L95 — no
  `AddAuthentication`, no `UseAuthorization`, no `AddRateLimiter`, `MapControllers()` has no
  `.RequireAuthorization()` (grep across `FinancialServices.Api` = 0 matches).
- **OWASP / Regulation:** A01 Broken Access Control, A05 Security Misconfiguration; API4:2023
  Unrestricted Resource Consumption. SEC/FINRA books-and-records, GDPR/CCPA access control.
- **Exploit:** Any host that can reach `:8000` calls `POST /api/v1/reconciliations
  {"issuerId":"nordstar","asOf":"2026-07-06"}` → the sweep runs and **persists** a dossier + audit doc
  to Cosmos. `GET /api/v1/reconciliations/{id}` and `/{id}/export` return the full dossier to anyone
  holding an id. `[AllowAnonymous]` is decorative — with no authentication middleware there is no
  identity to deny to.
- **Impact:** On real data every issuer dossier is world-readable and world-writable; on ACA the same
  surface is internet-facing. This is the umbrella that SEC-02..05 sharpen.
- **Fix:** Entra JWT bearer + a deny-by-default `[Authorize]` fallback policy (the accelerator pattern
  the controllers already *gesture* at in their XML docs); `AddRateLimiter` (per-identity + a global
  concurrency cap); keep `[AllowAnonymous]` **only** on `GET /issuers` + `GET /api/health` for the demo.
  Until then: localhost / firewalled / synthetic only.

### [SEC-02] Audit actor is hardcoded `"system"` → non-attributable, non-repudiable trail — Severity: **High**
- **Boundary / Target:** `Services/ReconciliationService.cs` `RunAsync` L81 (`Actor: "system"`,
  `dossier_generated`); `Controllers/ReconciliationsController.cs` `Export` L127 (`Actor: "system"`,
  `dossier_exported`); `Models/AuditEvent.cs` L14 (comment: *"'system' for the demo; the authenticated
  user in prod"*).
- **OWASP / Regulation:** A09 Security Logging & Monitoring Failures; P6 §C audit requirement,
  SEC 17a-4 / FINRA non-repudiation.
- **Exploit:** Because there is no authenticated principal (SEC-01), **every** consequential action —
  generate and export, by anyone — is stamped `Actor:"system"`. The `audit_events` container cannot
  answer "who reconciled or exported issuer X's dossier?" — the one question a financial audit exists
  to answer.
- **Impact:** The audit trail is present but evidentially worthless; a malicious actor is
  indistinguishable from the platform itself.
- **Fix:** Derive `Actor` from the authenticated principal (`oid`/`sub` claim) once SEC-01 lands; do not
  ship a real-data build with a constant actor. Document the demo relaxation explicitly.

### [SEC-03] Scope-confirmed is never audited + dossier is persisted **before** its audit write — Severity: **High**
- **Boundary / Target:**
  - (a) `Orchestration/PrismStreamingOrchestrator.cs` L44–L59 emits the `scope-confirm` event and
    proceeds when `confirmed==true`, but writes **no** `audit_events` record (grep of `Orchestration/`
    for `WriteAsync` = 0). Spec §C explicitly requires an audit event on **scope confirmed**.
  - (b) `Services/ReconciliationService.cs` L74 `await store.UpsertAsync(dossier, ct)` runs **before**
    L76–L92 `await audit.WriteAsync(...)`, and `Services/AuditService.cs` L28–L38 **throws**
    (`UpstreamServiceException`) on a Cosmos fault.
- **OWASP / Regulation:** A09; P5 human-in-the-loop, P6 §C, financial audit atomicity.
- **Exploit:** (a) An anonymous caller sets `confirmed:true` on `POST /reconciliations/stream`; the P5
  gate that *authorizes* the consequential sweep leaves zero trace. (b) If the audit write fails (Cosmos
  throttled/partitioned) after the dossier upsert, the caller gets a 500 but the dossier is **already
  persisted** — a consequential mutation exists with **no** audit record. A well-timed client abort
  (OCE after L74, before L76 completes) produces the same orphaned-dossier state.
- **Impact:** Actions occur without a durable audit record; the approval that gates them is unrecorded.
- **Fix:** Write a `scope_confirmed` audit event in the orchestrator before proceeding; make the
  dossier+audit pair atomic (Cosmos transactional batch — same `/issuerId` partition) or write audit
  first and reconcile on dossier failure. Treat audit as integrity-critical, not fire-and-forget.

### [SEC-04] Dossier capability URLs are unauthenticated and never expire — Severity: **High**
- **Boundary / Target:** `Controllers/ReconciliationsController.cs` `GetById` L104–L113 and `Export`
  L116–L138; id minted at `Services/ReconciliationService.cs` L123 (`{issuerId}:{guid:N}`).
- **OWASP / Regulation:** A01 Broken Access Control (capability-URL variant); GDPR/CCPA data-access.
- **Exploit:** The **only** access control on a persisted dossier is possession of its id. There is no
  auth, no expiry, and no Cosmos TTL. An id leaked via `Referer`, a proxy/access log, browser history,
  a shared link, or the SSE `dossier-ready` payload grants **permanent** unauthenticated read of the
  full dossier (ratings, notches, red-flag rule text, divergence math).
- **Impact:** Persistent, silent exposure of every dossier whose id escapes — on real issuers, PII-grade.
- **Credit (genuinely solid):** the id's guid is `Guid.NewGuid()` (122-bit CSPRNG) and **no** endpoint
  lists dossier ids, so IDOR-by-enumeration is infeasible; `Export` renders **less** than `GET /{id}`
  (it omits the LLM narrative/explanation), so export leaks ≤ read. The weakness is the **authorization
  model**, not guessability.
- **Fix:** Require auth + object-level authorization on both endpoints; add a Cosmos TTL on
  `rating_reconciliations`; if link-sharing is a real feature, mint short-lived signed tokens, not bare
  capability URLs.

### [SEC-05] Unauthenticated persistence write-amplification (no idempotency / rate limit / TTL) — Severity: **High**
- **Boundary / Target:** `Controllers/ReconciliationsController.cs` `Post` L34 and confirmed `Stream`
  L67; `Services/ReconciliationService.cs` L48/L78 `NewPartitionedId` (fresh guid **every** call),
  L74 `UpsertItemAsync` + L76 `CreateItemAsync` (2 Cosmos writes per call); no `AddRateLimiter`, no
  `SemaphoreSlim`, no dedup, no TTL.
- **OWASP / Regulation:** API4:2023 Unrestricted Resource Consumption; A05.
- **Exploit:** Every `POST` mints a **new** dossier id + audit id and unconditionally writes both — with
  `asOf` unbounded (SEC-06) an attacker trivially varies the request. An anonymous loop against one
  `issuerId` creates a hot partition on `/issuerId` and unbounded item growth; on serverless Cosmos
  (this account) that is direct RU + storage cost, and each call also fires the paid gpt-5.4 narration
  fan-out.
- **Impact:** Cost/DoS amplification through the persistence layer; unbounded junk accumulation with no
  reclamation.
- **Fix:** Rate limit (SEC-01); derive an idempotency key from `(issuerId, asOf)` so re-runs upsert one
  document instead of minting duplicates; Cosmos TTL on dossiers.

### [SEC-06] `asOf` accepts future / arbitrary dates — Severity: **Medium**
- **Boundary / Target:** `Models/PrismDtos.cs` — `ReconciliationRequest` (`[Required] DateTimeOffset?
  AsOf`, no range) and `ReconciliationStreamRequest` (same); consumed unbounded in
  `Services/SearchCorpusMapper.cs` `MapCards` (`card.AsOfDate <= asOf`).
- **OWASP / Regulation:** A04 Insecure Design; P6 (*"asOf must be a real date not in the future beyond
  today"*), P3 as-of correctness.
- **Exploit:** `asOf:"9999-01-01"` is accepted → on real data a future as-of admits rating actions that
  had not occurred at any real decision date (hindsight, P3 breach); `asOf:"1000-01-01"` yields an
  empty-rating dossier that still persists (feeds SEC-05).
- **Fix:** Add an `IValidatableObject` / range check: `asOf ∈ [reasonable-floor, today]`, rejected with
  the standard `VALIDATION_FAILED` envelope.

### [SEC-07] Financial-dossier **reads** are unaudited — Severity: **Medium**
- **Boundary / Target:** `Controllers/ReconciliationsController.cs` `GetById` L104–L113 returns the full
  dossier with **no** `audit_events` write; only `Export` audits.
- **OWASP / Regulation:** A09; P6 (*audit trail for financial-data access*). Note: spec §C lists only
  generate/export/confirm, so this is P6-stricter-than-§C — a customer-build gap, not a §C violation.
- **Exploit:** An actor reads any issuer's dossier via `GET /{id}` and leaves no access record — invisible
  to monitoring.
- **Fix:** Emit a `dossier_read` audit event (actor + issuerId + dossierId) for read/export once identity
  exists.

### [SEC-08] 5xx error envelope leaks internal service/config/type names — Severity: **Medium**
- **Boundary / Target:** `Infrastructure/Http/PrismExceptionHandler.cs` L54–L61 — `UpstreamServiceException`
  → `details { service: "Cosmos"|"Search" }`; `ConfigurationException` → `details { setting:
  "Azure:CosmosEndpoint" }`; `DomainInvariantException` → `details { invariant: ... }`. Plus `Program.cs`
  L44–L52 `InvalidModelStateResponseFactory` surfaces STJ unmapped-member messages that embed the
  fully-qualified DTO type name.
- **OWASP / Regulation:** A05 Security Misconfiguration (information disclosure).
- **Exploit:** An anonymous caller maps the backend topology (which services exist, which is failing),
  the exact config keys, and internal namespaces/type names from error responses.
- **Credit:** **no stack traces leak** — unknown exceptions fall to a generic `"An unexpected error
  occurred."` (L62–L63); `JsonException` → clean 400. This is correct and worth keeping.
- **Fix:** Keep the stable `code`; drop `service`/`setting`/`invariant`/type specifics from the
  client-facing `details` (log them server-side, correlate by request id).

### [SEC-09] Audit trail is not tamper-evident — Severity: **Low**
- **Boundary / Target:** `Services/AuditService.cs` L30 `CreateItemAsync` into `audit_events`; the same
  managed identity (Cosmos DB Data Contributor) can delete/replace audit items — no append-only/WORM,
  no hash-chain.
- **OWASP / Regulation:** A09; SEC 17a-4(f) WORM expectation.
- **Fix (prod):** Dedicated write-only identity for the audit container, Cosmos continuous-backup /
  immutability, or an append-only hash chain.

### [SEC-10] Log injection (CWE-117) via unvalidated `issuerId` — Severity: **Low**
- **Boundary / Target:** `Services/SearchCorpus.cs` L118–L121 logs the OData `filter` (raw issuerId,
  only single-quote-escaped); `issuerId` has no `[StringLength]`/charset cap (`Models/PrismDtos.cs`), so
  interior CR/LF survives `.Trim()` and the OData escape.
- **OWASP / Regulation:** A09; CWE-117.
- **Exploit:** In a plain-text log sink, a CRLF-laden issuerId forges log lines. Structured sinks capture
  it as a property (safe) — impact is sink-dependent.
- **Fix:** Cap length + reject control characters on `IssuerId`; rely on structured logging.

### [SEC-11] CORS origin hardcoded, not from config — Severity: **Low**
- **Boundary / Target:** `Program.cs` L64–L66 `WithOrigins("http://localhost:5173")` — hardcoded, not
  bound to `Cors:Origins` per arch-06.
- **OWASP / Regulation:** A05.
- **Credit:** specific origin, **no** wildcard, **no** `AllowCredentials()` → not exploitable today.
- **Fix:** Bind `Cors:Origins` from config so the prod origin is deployable without a code change.

---

## What's Solid (do not regress)

- **No Cosmos SQL injection.** `Services/CosmosDossierStore.cs` L47–L52 (`ReadItemAsync(id,
  PartitionKey)`) and L34–L35 (`UpsertItemAsync(dossier, PartitionKey)`) are exclusively point
  operations — no `QueryDefinition`, no string-built SQL anywhere. The id→partition parse
  (`ReconciliationsController.PartitionOf` L149–L159) validates the `:` shape before use.
- **No AI Search OData injection.** `Services/SearchCorpus.cs` L124 escapes the single quote
  (`Replace("'","''")`) and applies it on **every** issuerId-bearing filter (L42, L52, L69) — the only
  metacharacter that can break an OData string literal.
- **No export XSS.** `Services/DossierHtmlRenderer.cs` routes **every** dynamic value through
  `HtmlEncoder.Default` (`E(...)`); the severity→CSS class is a fixed `switch` (no attribute injection);
  and the LLM `Narrative`/`Explanation` fields are **not rendered at all** — only deterministic rule
  text (P2). Reflected/stored XSS via issuer name or rule text is closed.
- **Credential hygiene.** `Infrastructure/ServiceCollectionExtensions.cs` L88 registers
  `DefaultAzureCredential` only; `Infrastructure/AzureOptions.cs` exposes **endpoints only** — zero key
  / connection-string fields; `CosmosClient` + `SearchClient` are built with the `TokenCredential`
  (Cosmos `disableLocalAuth` per infra). No secret in code, config, or logs.
- **Unknown-field rejection is real.** `Program.cs` L30–L35 sets `UnmappedMemberHandling.Disallow` on
  the actual MVC input path (`AddControllers().AddJsonOptions`), plus `[Required]` on `IssuerId`/`AsOf`;
  malformed bodies → clean `VALIDATION_FAILED` 400. No mass-assignment: the response DTOs are distinct
  from domain/persistence records.
- **No stack-trace leakage** and cancellation is not converted to 500 (`PrismExceptionHandler` L20–L22,
  L62–L63).
- **Log hygiene** across the service layer is ids + counts only (`ReconciliationService` L82–L91,
  `AuditService` L40–L43) — no dossier payload / financials.

---

## Demo-Acceptable Residuals vs Production Blockers

**Acceptable for the localhost / synthetic-corpus demo (do NOT ship to real data):**
- SEC-01 no auth / no rate limit — the whole gate is off, but the corpus is labeled-synthetic and the
  host is localhost/firewalled.
- SEC-04 capability URLs — ids are unguessable; synthetic dossiers carry no PII.
- SEC-06 `asOf` range — synthetic cards make a bad `asOf` degenerate, not dangerous.
- SEC-08 error-detail disclosure, SEC-10 log injection, SEC-11 CORS hardcoding — low-impact on localhost.

**Production blockers (must fix before ANY real issuer/PII or public/ACA exposure):**
1. **SEC-01** — authenticate + authorize + rate-limit every endpoint; anonymous is off the table.
2. **SEC-02 + SEC-03** — real principal in `Actor`; audit the scope-confirm; make dossier+audit atomic.
3. **SEC-04 + SEC-05** — object-level authz + Cosmos TTL on capability URLs; idempotency + rate limit on
   writes.
4. **SEC-07** — audit financial-data reads.
5. **SEC-08 / SEC-09** — strip internal names from client errors; make the audit container tamper-evident.

---

## Top 3 Must-Fix Before Any Real Data
1. **SEC-01** — Entra JWT + deny-by-default `[Authorize]` + `AddRateLimiter`; `[AllowAnonymous]` only on
   `GET /issuers` + health.
2. **SEC-02 / SEC-03** — set `Actor` from the authenticated principal, write a `scope_confirmed` audit
   event, and make the dossier upsert + audit write atomic (transactional batch on `/issuerId`).
3. **SEC-04 / SEC-05** — object-level authorization + Cosmos TTL on `GET /{id}` + `/export`, and an
   `(issuerId, asOf)` idempotency key so writes can't be amplified.
