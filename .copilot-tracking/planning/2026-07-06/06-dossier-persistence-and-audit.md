# Plan 06 — Dossier Assembly, Cosmos Persistence & Audit Trail

**Objective:** Assemble the regulator-ready dossier, persist inquiries + an **immutable audit trail**
to Cosmos DB (free tier), and enforce the scope gate + idempotency **server-side** at the trust
boundary. Audit is the product.

**Depends on:** Plans 01, 05. **Primary day:** 2 (audit hardening continues Day 4).

> Folds ⚠ ARC-02 (server-side HITL gate + auth), ARC-03 (durable immutable audit), ARC-04
> (idempotency + job store), SEC-01/02/03 (authN + OBO + object-level authZ).

---

## 1. Dossier model

- [ ] `backend/FinancialServices.Api/Models/DossierModels.cs`:
  - `ReconstructionInquiry` (record): `id`, `issuer`, `instrument`, `decisionDate`, `submittedBy`,
    `submittedTs`, `scopeConfirmed`, `status` (`PendingScope` | `Sweeping` | `Assembled` | `Exported`)
  - `Dossier` (record): the `Timeline` (Plan 05), ranked `RedFlag[]`, the `MarketSnapshot` (Plan 03),
    evidence citations, `generatedTs`, elapsed reconstruction time (the stopwatch value)
  - Every claim references a `sourceDocId` so the export (Plan 10) can hyperlink it

## 2. Cosmos DB containers (free tier — assume provisioned)

- [ ] Database `rewind`; containers:
  - `inquiries` — partition key `/submittedBy` (or `/orgId`); validate against query patterns
    (⚠ ARC-01: never derive a partition key from the row id; point-read with the correct PK)
  - `auditLog` — **append-only, immutable**; partition key `/inquiryId`
  - `jobs` — idempotency + reconstruction job state; partition key `/inquiryId`, unique idempotency key
- [ ] Repositories under `Services/` using the singleton `CosmosClient` (STJ serializer from Plan 01);
      point reads use the correct partition key value (not the id)

## 3. Immutable audit trail (⚠ ARC-03)

- [ ] `AuditEvent` record: `eventType`, `timestamp`, `actorId`, `orgId`, `sessionId`, `action`,
      `metadata`, `sourceIp` — matching the financial-domain standard
- [ ] Write the audit event as a **precondition** of accepting each consequential action (inquiry
      submitted, scope confirmed, dossier assembled, dossier exported) — **fail the request if the
      audit write fails**
- [ ] Container is append-only: no updates/deletes; never treat `ILogger` as the system of record
- [ ] Do not log PII; mask identifiers (`****1234`) per financial-domain rules

## 4. Server-side scope gate + idempotency (⚠ ARC-02/04, SEC-01/02/03)

- [ ] Model the scope gate as an explicit state-machine transition (`PendingScope → Sweeping`) that
      requires an authenticated caller + a verifiable approval artifact — not a UI-only boolean
- [ ] Accept and enforce an `Idempotency-Key` header persisted in `jobs`; a repeated key returns the
      same job (no duplicate reconstruction)
- [ ] Object-level authorization: resolve caller → allowed org/scope; verify ownership before every
      read/mutation. Treat route/body/LLM ids as untrusted

## 5. API surface (`/api/v1/`)

Author `backend/FinancialServices.Api/Controllers/ReconstructionsController.cs` (`[Authorize]`).

- [ ] `POST /api/v1/reconstructions` — submit inquiry (issuer, instrument, decisionDate); returns
      `202` + `Location: /api/v1/reconstructions/{id}` + status `PendingScope`; writes audit
- [ ] `POST /api/v1/reconstructions/{id}/confirm-scope` — the human gate; transitions to `Sweeping`;
      writes audit; requires approver identity
- [ ] `GET /api/v1/reconstructions/{id}` — status + assembled dossier (object-level authZ)
- [ ] `GET /api/v1/reconstructions/{id}/dossier` — the full dossier for the UI/export
- [ ] All responses use RFC 7807 `ProblemDetails` on error; validate + reject unexpected fields

## 6. Orchestration glue

- [ ] A `ReconstructionService` ties the orchestrator (Plan 04) → timeline/rules (Plan 05) → dossier →
      Cosmos persistence, measuring elapsed time for the stopwatch
- [ ] Register everything in `AddDomainServices()`

## Acceptance criteria

- Inquiry → confirm-scope → dossier flow persists to Cosmos with the correct partition keys
- Every consequential action writes an immutable audit event *before* succeeding; audit write failure
  fails the request
- Idempotency-Key dedupes duplicate submissions; `202` returns a pollable `Location`
- Endpoints are `[Authorize]`d and enforce object-level ownership; anonymous/curl access is rejected
- A compliance query can retrieve approver + timestamp + scope for any reconstruction

## Cut-lines

- If Cosmos slips, persistence can be deferred but audit **cannot** be faked — keep the immutable
  audit write as the last thing to cut; for a pure-localhost demo, an append-only local store is
  acceptable only if clearly labeled dev-only (never in a deployed path)
