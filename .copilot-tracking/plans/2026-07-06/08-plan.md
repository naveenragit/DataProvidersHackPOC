# Package 08 — API & Persistence · Plan

**Package:** `implementationPlan/08-api-and-persistence.md` · **Depends on:** 02, 05 (both ✅) ·
**Blocks:** 10 · **Date:** 2026-07-06

The REST surface (list / detail / **fallback** reconciliation / export) plus **real** Cosmos
persistence + audit trail, reading the pkg-03 Search corpus. **No LLM** — pkg 06 narrators are not
built, so `Narrative` fields stay empty and the deterministic **Rule** text is the money moment.

---

## Acceptance criteria → where satisfied

| # | Acceptance | Satisfied by |
|---|---|---|
| A1 | `GET /api/v1/issuers` returns the cast from real Cosmos/Search (no mock list) | `IssuersController` → `ISearchCorpus.GetIssuersAsync` (Search); live-gated if index un-reseeded |
| A2 | `POST /api/v1/reconciliations` (fallback) for NordStar persists a dossier with STALE_INPUT | `ReconciliationsController.Post` → `ReconciliationService.RunAsync` → `DossierAssembler` (STALE via `asOfDate→InputAsOfDate`) → Cosmos |
| A3 | `GET /api/v1/reconciliations/{id}` round-trips the persisted dossier | id encodes issuerId → `CosmosDossierStore.ReadAsync` point read |
| A4 | Every run writes ≥1 `audit_events` doc; no PII/financials in logs | `AuditService.WriteAsync` in `RunAsync`; `ILogger` scopes log ids+counts only |
| A5 | Error responses match the standard shape `{ error: { code, message, details } }` | `PrismExceptionHandler` (IExceptionHandler) + `InvalidModelStateResponseFactory` |

Cross-package (frozen `frontend/src/types/prism.ts`): `Provider` serializes as string names
(`JsonStringEnumConverter`), camelCase wire; DTO names mirror the TS exactly.

---

## Non-negotiables applied (arch 00)

- **P1** real Search + Cosmos via `DefaultAzureCredential`; no mock/stub in runtime (fakes only in
  `FinancialServices.Tests`). Missing `Azure:*` config → `ConfigurationException` at first use (health
  still boots). Synthetic issuers' real financials are genuinely absent → `null`, never fabricated.
- **P2** notch/gap/flag math stays in `Analysis/`. The service/`DossierAssembler` **only orchestrates
  and maps** — it never re-implements a formula.
- **P3** every verdict carries `MethodologyDocId`; every flag carries `EvidenceRefs` (from pkg 05).
- **P4** no buy/sell/hold/recommend/allocate/trade/alpha/signal anywhere (routes, DTOs, HTML, code,
  comments). Grep-gate in a test.
- **P6** `DefaultAzureCredential`, no secrets in code; audit + logs = ids + counts only.
- **P7** `CancellationToken` plumbed controller → service → SDK on every async call.

---

## Design decisions (locked)

- **D1 — STALE mapping (money moment).** `SearchCorpusMapper` maps ratingCard **`asOfDate`** →
  `ProviderRating.InputAsOfDate` (rating-action date, matching pkg-04 `RatingActionDate→InputAsOfDate`).
  `RedFlagEngine` compares it to `latest.FilingDate` = issuer `latestFilingDate`. NordStar: MSCI
  `2025-09-15 < 2025-11-05` fires; Moody's/DBRS `2025-11-12/14` do not. **Never** map the card's
  `inputAsOfDate` (Q3 period-end `2025-09-30` for all three → would false-flag everyone).
- **D2 — Index extension (additive, live gate).** Add retrievable fields to `prism-ratings`:
  `legalName, ticker, cik, sector, sampleBondIsin` (String) + `latestFilingDate` (DateTimeOffset) +
  `filingType` (String). `latestFilingDate` is required even for A2 (STALE boundary). Project them in
  `IndexDoc.From`. Re-seed (`dotnet run --project tools/SeedData`) is a **live gate** — reported, not
  blocking build/unit tests. Factors are **not** projected (nested); provider ratings are letter-only
  → `Decompose` is honestly residual-dominated (pkg-05 established) — STALE needs no factors.
- **D3 — Dossier id encodes the partition key.** `id = $"{issuerId}:{Guid.NewGuid():N}"` (immutable,
  arch-08). `GET /reconciliations/{id}` parses `issuerId` = substring before `':'` for a Cosmos point
  read (frontend `useDossier` sends id only). Reject an id with no `':'` → 404.
- **D4 — Fundamentals source for the synthetic cast.** `FundamentalSnapshot latest` is built from the
  issuer doc's `latestFilingDate` + `filingType`; ratios = `null` (synthetic → genuinely absent, P1).
  Real EDGAR/FRED enrichment (pkg 04, already wired) is a documented seam for real issuers; it is
  **not** invoked on synthetic CIKs (they 404). This keeps the composition honest without fabrication.
- **D5 — Error shape over ProblemDetails.** The default `ProblemDetails` shape (`type/title/status`)
  does **not** match `{ error: { code, message, details } }` that the frontend `ApiError` parses.
  Implement a custom `IExceptionHandler` writing the Prism envelope, and set
  `ApiBehaviorOptions.InvalidModelStateResponseFactory` to the same envelope (`VALIDATION_FAILED`).
  Reject unknown request fields via `JsonUnmappedMemberHandling.Disallow`.
- **D6 — Cosmos + Search boundary services (testability).** `ISearchCorpus` owns Search reads,
  `ICosmosDossierStore` owns dossier read/write, `IAuditService` owns audit writes (arch-08). The pure
  `DossierAssembler` composes the deterministic engines. Unit tests fake these interfaces (no live
  Azure). A `WebApplicationFactory` test overrides them to exercise the real HTTP pipeline offline.
- **D7 — Auth.** Read + fallback endpoints are `[AllowAnonymous]` for the demo (spec §A). The Entra
  bearer seam is documented in a comment (no auth middleware added — keeps the demo simple; never ship
  anonymous with real PII).
- **D8 — Serialization.** MVC JSON = camelCase (Web default) + `JsonStringEnumConverter`. Cosmos uses
  `CosmosClientOptions.UseSystemTextJsonSerializerWithOptions(Web + enum converter)` so stored docs are
  camelCase and `id`/`issuerId` land correctly. `PrismModels.cs` equality/decimal serialization is
  **not** changed (STJ serializes decimal as number; value-equality is unused by persistence).

---

## Phase A — Project wiring & options

- **A-1** `FinancialServices.Api.csproj`: add `Microsoft.Azure.Cosmos`, `Azure.Search.Documents`
  `11.6.0`, `Azure.Identity` `1.13.2`, `Microsoft.AspNetCore.OpenApi` (net9). Pin exact; verify restore.
- **A-2** `Infrastructure/AzureOptions.cs` (new): bind section `Azure` — `CosmosEndpoint`,
  `CosmosDatabase = "prism"`, `SearchEndpoint`, `SearchIndex = "prism-ratings"`. No `ValidateOnStart`
  (health boots without Azure); consumers fail loud at first use.
- **A-3** `Infrastructure/ServiceCollectionExtensions.cs` (edit): add `AddAzureOptions(config)` and
  `AddPrismDataServices()` — register singleton `TokenCredential = DefaultAzureCredential`, factory
  `CosmosClient` (STJ web+enum serializer; throws `ConfigurationException("Azure:CosmosEndpoint")` if
  empty **when resolved**), factory `SearchClient` (throws on empty `SearchEndpoint`), and
  `ISearchCorpus`/`ICosmosDossierStore`/`IAuditService`/`IReconciliationService` +
  `DivergenceDecomposer`/`RedFlagEngine` singletons.

## Phase B — Errors & middleware (arch 03)

- **B-1** `Infrastructure/Errors/NotFoundException.cs` (new): `Code = "NOT_FOUND"`, carries `resource` +
  `id`; → 404.
- **B-2** `Infrastructure/Errors/ValidationException.cs` (new): `Code = "VALIDATION_FAILED"`, carries a
  `details` dict (field → message); → 400.
- **B-3** `Infrastructure/Http/PrismExceptionHandler.cs` (new, `IExceptionHandler`): map
  `NotFoundException→404`, `ValidationException→400`, `UpstreamServiceException→502`,
  `DomainInvariantException→500`, `ConfigurationException→500`, `JsonException→400` (bad body),
  fallback→500 `INTERNAL_ERROR` (generic message, no leak). **Rethrow `OperationCanceledException`**
  (P7 — do not convert to 500). Write `{ error: { code, message, details } }` with the shared
  camelCase+enum `JsonSerializerOptions`. Log once at the boundary (ids only).

## Phase C — DTOs & mapping (arch 09, Models/PrismDtos.cs)

- **C-1** `Models/PrismDtos.cs` (new): `IssuerListItem`, `ProviderVerdictDto`, `BucketAttributionDto`,
  `PairDivergenceDto`, `RedFlagDto`, `DossierResponse`, `ReconciliationRequest`
  (`[Required] IssuerId`, `[Required] AsOf`, `Providers?`), `ApiError`/`ApiErrorBody`. Names/shapes
  mirror `frontend/src/types/prism.ts` exactly.
- **C-2** `Models/PrismDtoMappings.cs` (new): pure static mappers `ToDto` — `Issuer(+coverage)→
  IssuerListItem`, `ProviderRating→ProviderVerdictDto`, `BucketAttribution→BucketAttributionDto`,
  `PairDivergence→PairDivergenceDto`, `RedFlag→RedFlagDto`, `ReconciliationDossier→DossierResponse`.
- **C-3** `Models/AuditEvent.cs` (new): `record AuditEvent(string Id, string EventType, DateTimeOffset
  Timestamp, string Actor, string IssuerId, string Action, IReadOnlyDictionary<string, object>
  Metadata)` (spec §C shape; metadata = ids + counts only).

## Phase D — Search corpus reader (arch 08, Services/)

- **D-1** `Services/SearchCorpusMapper.cs` (new, **pure**): index-row record → `ProviderRating`
  (`asOfDate→InputAsOfDate`, `Notch = NotchLadder.ToNotch(Letter)`, empty `Factors`, `MethodologyDocId
  = card id`) and → `IssuerCorpusEntry(Issuer, LatestFilingDate, FilingType, Coverage)`. **Unit-tested
  in isolation** (the STALE mapping proof — D1).
- **D-2** `Services/ISearchCorpus.cs` + `Services/SearchCorpus.cs` (new): `SearchClient`-backed.
  `GetIssuersAsync(ct)` (filter `docType eq 'issuer'` + join card providers for coverage),
  `GetIssuerAsync(issuerId, ct)`, `GetProviderRatingsAsync(issuerId, asOf, ct)` (filter `docType eq
  'ratingCard' and issuerId eq X`, keep `asOfDate <= asOf`, map via D-1). `ct` plumbed. Empty result
  set is data, not an error.

## Phase E — Cosmos persistence & audit (arch 08, Services/)

- **E-1** `Services/ICosmosDossierStore.cs` + `Services/CosmosDossierStore.cs` (new): `Container`
  `rating_reconciliations`. `UpsertAsync(dossier, ct)` with `PartitionKey(dossier.IssuerId)`;
  `ReadAsync(id, issuerId, ct)` point read → `null` on `NotFound`.
- **E-2** `Services/IAuditService.cs` + `Services/AuditService.cs` (new): `Container` `audit_events`.
  `WriteAsync(AuditEvent, ct)`; also `ILogger` scope (issuerId) logging ids + counts (no PII).

## Phase F — Reconciliation composition (Services/)

- **F-1** `Services/DossierAssembler.cs` (new, **pure static**): `Assemble(DivergenceDecomposer,
  RedFlagEngine, Issuer, FundamentalSnapshot latest, IReadOnlyList<ProviderRating>, string id,
  DateTimeOffset asOf, DateTimeOffset generatedAt) → ReconciliationDossier`. Runs `Decompose` over all
  unique provider pairs (ordered by enum value; `aInputs/bInputs = null`), `Evaluate`,
  `ConsensusSummary`, `ConfidenceScore`. No I/O, no LLM (`Narrative`/`Explanation` stay empty).
- **F-2** `Services/DossierHtmlRenderer.cs` (new, **pure static**): `Render(ReconciliationDossier) →
  string` printable HTML; **all dynamic values `HtmlEncoder.Default.Encode`d** (P6/XSS). Shows verdicts,
  divergences, the verbatim red-flag **Rule** text (money moment), consensus, confidence. No P4 vocab.
- **F-3** `Services/IReconciliationService.cs` + `Services/ReconciliationService.cs` (new, spec §B
  signature): `GetIssuersAsync` (→ `searchCorpus` issuers), `RunAsync` (issuer lookup → 404 if absent →
  ratings → build `latest` → `DossierAssembler.Assemble` → `store.UpsertAsync` → `audit.WriteAsync`
  `dossier_generated` → log scope), `GetAsync(id, issuerId, ct)`, `SaveAsync(dossier, ct)`.

## Phase G — Controllers (arch 09, Controllers/)

- **G-1** `Controllers/IssuersController.cs` (new, `[ApiController]`, `[Route("api/v1/issuers")]`,
  `[AllowAnonymous]`, injects `ISearchCorpus`): `GET` list → `IssuerListItem[]`; `GET {issuerId}` →
  detail or `throw NotFoundException`. `[ProducesResponseType]` annotations.
- **G-2** `Controllers/ReconciliationsController.cs` (new, `[Route("api/v1/reconciliations")]`,
  `[AllowAnonymous]`, injects `IReconciliationService` + `IAuditService`): `POST` (validate
  `ReconciliationRequest`) → `RunAsync` → `DossierResponse`; `GET {id}` → parse issuerId → `GetAsync`
  → dossier or 404; `GET {id}/export` → `GetAsync` → `DossierHtmlRenderer` → `Content(html,
  "text/html")` + audit `dossier_exported`.

## Phase H — Program.cs wiring

- **H-1** `Program.cs` (edit): keep health + `AddPrismOptions().AddConnectors()`. Add
  `AddAzureOptions()` + `AddPrismDataServices()`; `AddControllers().AddJsonOptions(enum converter +
  `UnmappedMemberHandling.Disallow`)` + `ConfigureApiBehaviorOptions(InvalidModelStateResponseFactory)`;
  `AddExceptionHandler<PrismExceptionHandler>()` + `AddProblemDetails()`; `AddOpenApi()`; a named dev
  CORS policy for `http://localhost:5173`. Middleware: `UseExceptionHandler()`, `UseCors(dev)`,
  `MapControllers()`, `MapOpenApi()` (dev). Keep `public partial class Program`.

## Phase I — Search index extension (pkg-03 tool, additive)

- **I-1** `tools/SeedData/Search/PrismSearchIndex.cs` (edit): add `legalName, ticker, cik, sector,
  sampleBondIsin` (String, retrievable), `latestFilingDate` (DateTimeOffset, filterable/sortable),
  `filingType` (String). Additive.
- **I-2** `tools/SeedData/Search/IndexDoc.cs` (edit): add matching `[JsonPropertyName]` properties +
  copy them in `From(CorpusDoc, vector)` (issuer docs carry them; cards leave them empty).
- **I-3** If `SeedData.Tests` asserts an exact field set, update that assertion. **Live gate:** re-seed
  required for the live index to carry the new fields (reported).

## Phase J — Tests (arch 11, `FinancialServices.Tests`, offline)

- **J-1** `FinancialServices.Tests.csproj` (edit): add `Microsoft.AspNetCore.Mvc.Testing` (net9).
- **J-2** `Tests/Services/SearchCorpusMapperTests.cs` — **D1 proof**: card `asOfDate→InputAsOfDate`;
  notch via `NotchLadder`; as-of filter drops future-action cards; issuer entry maps metadata +
  boundary + coverage.
- **J-3** `Tests/Services/DossierAssemblerTests.cs` — NordStar → 1 `STALE_INPUT`, 3 divergences,
  consensus "consensus within 1 notch", confidence `0.70`; Cedar Grove → 0 flags, "full consensus",
  `1.0`; Aster Bio → `MISSING_COVERAGE`; Onyx → `OUTLIER_PROVIDER`. Uses `PrismFixtures`.
- **J-4** `Tests/Services/ReconciliationServiceTests.cs` — fake `ISearchCorpus`/`ICosmosDossierStore`
  (in-memory)/`IAuditService` → `RunAsync("nordstar")` persists a dossier with `STALE_INPUT`, writes
  ≥1 audit doc, `GetAsync` round-trips (A2/A3/A4 offline); unknown issuer → `NotFoundException`.
- **J-5** `Tests/Models/PrismDtoTests.cs` — `DossierResponse` serializes with `Provider` as `"Msci"`
  (enum-as-string), camelCase keys, `notches`/`confidence` as numbers.
- **J-6** `Tests/Api/PrismApiTests.cs` — `WebApplicationFactory<Program>` + `ConfigureTestServices`
  fakes: `GET /api/v1/issuers` 200 + NordStar; `POST /reconciliations` 200 + `STALE_INPUT` + Provider
  string on the wire; `GET /reconciliations/{id}` round-trip; `GET /reconciliations/unknown` 404
  `{ error: { code, message, details } }` (A5); `GET .../export` 200 `text/html` with the STALE rule.
- **J-7** `Tests/Api/PrismExceptionHandlerTests.cs` — handler maps each exception → correct status +
  envelope; `OperationCanceledException` is rethrown, not swallowed.
- **J-8** `Tests/Quality/DomainLanguageTests.cs` — grep new `Controllers/`, `Services/`, `Models/
  PrismDtos.cs`, `DossierHtmlRenderer` for P4 forbidden words → none.

## Phase K — Build / test / live gate

- **K-1** `dotnet build backend/FinancialServices.slnx` — 0 warnings (warnaserror).
- **K-2** `dotnet test backend/FinancialServices.slnx` — all green (144 existing + new).
- **K-3** Live gate: load `.env` + `AZURE_TENANT_ID=ce48da85-…`; attempt a live Cosmos/Search smoke
  (`GET /issuers`, `POST /reconciliations` NordStar). If Search index is un-reseeded or unreachable →
  **report as a live gate**, do not fail the package (build + unit tests are the automatable gate).

---

## Out of scope (deferred, tracked in changes log)

- LLM narrators (pkg 06) — `Narrative`/`Explanation` stay empty by design.
- AG-UI streaming + confirm-scope gate (pkg 07).
- Real MSAL/Entra auth middleware (seam documented; pkg 07/11).
- Real EDGAR/FRED enrichment of `latest` for real issuers (seam; synthetic cast uses corpus boundary).
- PDF export (client fallback, pkg 10); factors projected into Search for a rich waterfall (pkg 10).
