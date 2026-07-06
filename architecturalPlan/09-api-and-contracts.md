# 09 — API & Contracts

The REST surface (TanStack Query data) and the AG-UI streaming endpoint. Stable, versioned, typed.

---

## Conventions

- All REST routes under `/api/v1/`; RESTful, plural resource nouns.
- `[ApiController]` controllers, one per resource, thin — delegate to services.
- OpenAPI document via `AddOpenApi()`/`MapOpenApi()` (dev). Annotate with `[ProducesResponseType<T>]`.
- Health: `GET /api/health` (anonymous). AG-UI agent: `POST /prism` (streams; see [04](04-agent-architecture.md)).

## Endpoints (v1)

| Method / route | Purpose | Success | Notes |
|---|---|---|---|
| `GET /api/v1/issuers` | list issuer cast | 200 `IssuerListItem[]` | from Search facet / seed |
| `GET /api/v1/issuers/{issuerId}` | issuer + coverage | 200 / 404 | |
| `POST /api/v1/reconciliations` | run sweep (fallback path) | 200 `DossierResponse` | idempotent per (issuer, asOf) is fine |
| `GET /api/v1/reconciliations/{id}` | fetch dossier | 200 / 404 | point read (needs issuerId) |
| `GET /api/v1/reconciliations/{id}/export` | printable HTML | 200 text/html | PDF fallback on client |

## DTO rules

- **DTOs are separate records** from domain models (`Models/PrismDtos.cs` vs `Models/PrismModels.cs`).
- Requests validate with DataAnnotations; **reject unknown fields**; never bind domain records directly
  from the wire.
- Responses expose only what the UI needs; no Cosmos/Search internals leak.
- `System.Text.Json`, camelCase on the wire (matches TS + Cosmos serialization).

### Example
```csharp
public sealed record ReconciliationRequest(
    [property: Required] string IssuerId,
    [property: Required] DateTimeOffset AsOf,
    IReadOnlyList<Provider>? Providers);   // null = all covered

public sealed record DossierResponse(
    string Id, string IssuerId, DateTimeOffset AsOf,
    IReadOnlyList<ProviderVerdictDto> Verdicts,
    IReadOnlyList<PairDivergenceDto> Divergences,
    IReadOnlyList<RedFlagDto> Flags,
    string ConsensusSummary, double Confidence);
```

## Errors

Every non-2xx uses the standard shape from [03](03-error-handling-and-propagation.md):
`{ "error": { "code", "message", "details" } }`. Use `Problem(...)` for expected 404s.

## Contract sync

`frontend/src/types/prism.ts` mirrors the response DTOs **by name and shape**. When a DTO changes,
update the TS types in the same change. Treat the DTOs as the contract — the AG-UI card payloads and
the REST responses should describe the same `ReconciliationDossier` concept.

## Versioning

`/api/v1` is frozen for the hackathon. Additive changes only; breaking changes would go to `/api/v2`.
