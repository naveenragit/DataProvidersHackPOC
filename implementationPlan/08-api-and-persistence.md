# 08 — API & Persistence

**Purpose:** the REST surface (for TanStack Query data) and Cosmos persistence + audit trail. The
streaming pipeline is AG-UI (package 07); these endpoints serve list/detail/export and the fallback.

**Depends on:** 02, 05. **Blocks:** 10.

Follow the accelerator's controller/service/DTO separation and `/api/v1/` prefix.

---

## A. Controllers (`Controllers/`)

```
GET  /api/health                                  → HealthController (AllowAnonymous)
GET  /api/v1/issuers                              → list the issuer cast (id, name, sector, ticker)
GET  /api/v1/issuers/{issuerId}                   → issuer detail + which providers cover it
POST /api/v1/reconciliations                      → run pipeline (FALLBACK, sync) → dossier
GET  /api/v1/reconciliations/{id}                 → fetch a persisted dossier
GET  /api/v1/reconciliations/{id}/export          → dossier as printable HTML (PDF fallback, pkg 10)
```

- Request DTOs separate from domain records; validate with DataAnnotations; reject unknown fields.
- Consistent error shape: `{ "error": { "code": "...", "message": "...", "details": {...} } }`
  (accelerator uses `Problem(...)` / ProblemDetails — keep it).
- **Auth for the hackathon:** the accelerator enforces a global auth fallback policy. For the demo,
  put `[AllowAnonymous]` on the read endpoints you demo *or* wire a dev token; keep the Entra pattern
  documented so "path to customer build" (rubric 04) is credible. Never ship anonymous with real PII.

---

## B. Cosmos DB

Reuse `AddAzureClients` (CosmosClient via `DefaultAzureCredential`). Database `prism`:

| Container | Partition key | Contents |
|---|---|---|
| `rating_reconciliations` | `/issuerId` | `ReconciliationDossier` documents |
| `audit_events` | `/issuerId` | one per consequential action |

`ReconciliationService`:
```csharp
public interface IReconciliationService
{
    Task<IReadOnlyList<Issuer>> GetIssuersAsync(CancellationToken ct);
    Task<ReconciliationDossier> RunAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct); // fallback
    Task<ReconciliationDossier?> GetAsync(string id, string issuerId, CancellationToken ct);
    Task SaveAsync(ReconciliationDossier dossier, CancellationToken ct);
}
```
`RunAsync` composes: corpus fetch (03) → connectors (04) → DivergenceDecomposer + RedFlagEngine (05)
→ narrators (06) → persist. The AG-UI orchestrator (07) calls the same building blocks.

Issuer list source: read from AI Search facet on `issuerId`, or a small `issuers` seed container.

---

## C. Audit trail (financial-domain requirement)

`AuditService.Write(AuditEvent)` on: scope confirmed, dossier generated, dossier exported. Use the
domain's `AuditEvent` shape (EventType, Timestamp, actor, `issuerId`, Action, Metadata). **Never log
PII or raw financials**; log ids + counts. Write via `ILogger` scopes + the `audit_events` container.

---

## Acceptance for this package
- [ ] `GET /api/v1/issuers` returns the cast from real Cosmos/Search (no mock list).
- [ ] `POST /api/v1/reconciliations` (fallback) for NordStar persists a dossier with the STALE_INPUT flag.
- [ ] `GET /api/v1/reconciliations/{id}` round-trips the persisted dossier.
- [ ] Every run writes ≥1 `audit_events` doc; no PII/financials in logs.
- [ ] Error responses match the standard shape.
