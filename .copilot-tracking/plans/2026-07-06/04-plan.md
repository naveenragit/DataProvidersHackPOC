<!-- markdownlint-disable-file -->
# Implementation Plan: Package 04 — Data Connectors (EDGAR / FRED + provider-ratings abstraction)

**Work Package:** `implementationPlan/04-real-data-connectors.md`
**Governing standards:** `architecturalPlan/00-core-principles.md` (P1 fail-loud, P3 provenance/as-of,
P4 language, P6 security, P7 async+ct), `01-naming-conventions.md`, `02-folder-structure.md`
(`Connectors/`), `03-error-handling-and-propagation.md` (taxonomy + standard shape), `05-configuration-and-secrets.md`
(options pattern), `06-security-and-compliance.md` (SSRF allowlist, secrets in options, tool-arg trust),
`07-observability-and-logging.md` (OTel span per external call), `08-data-and-persistence.md` (as-of correctness),
`11-testing-and-quality.md` (stubbed `HttpMessageHandler`)
**Financial Domain:** Capital Markets — corporate bond credit-rating reconciliation (data-quality tool, **not** a trading agent — P4)
**Azure Services (new NuGet):** **NONE.** `IHttpClientFactory`/`AddHttpClient`, `IOptions<T>`/
`ValidateDataAnnotations`, and `System.Diagnostics.ActivitySource` all ship in the ASP.NET Core (`Microsoft.NET.Sdk.Web`)
shared framework. No Cosmos / Search / Polly / OTel-SDK packages this increment (dependency-light so the
`TreatWarningsAsErrors` build stays green). Real OTel export + Cosmos caching arrive in pkg 07 / 08.

---

## Package acceptance (source of truth — every task maps back to one of these)

From `implementationPlan/04-real-data-connectors.md` **Acceptance for this package**:

- **A1** — `GetFactsAsOfAsync(nordstarCik, 2026-06-25)` returns the later ("Q3") figures with the correct `LatestFilingDate`.
- **A2** — the same call with `asOf = 2026-04-01` returns only the earlier ("Q2") figures (proves as-of filtering, no hindsight).
- **A3** — `GetLatestOnOrBeforeAsync("BAMLC0A0CM", asOf)` returns a spread dated ≤ as-of.
- **A4** — connectors emit OTel spans; failures throw actionable errors (no silent fallback / no fabricated values).
- **A5** — xUnit with a stubbed `HttpMessageHandler` for the as-of boundary cases.

Scope-item acceptance added for this increment:

- **A6** — `Provider.Bloomberg` → `Provider.Moodys` (final set `Moodys, MorningstarDbrs, Msci`); build + all NotchLadder tests stay green.
- **A7** — `PrismOptions` binds section `Prism`, `[Required] FredApiKey` + `[Required] SecUserAgent`, `Models` defaults; `ValidateDataAnnotations().ValidateOnStart()` wired in `Program.cs`.
- **A8** — `SyntheticRatingsSource` (Provider=Msci) returns a record for a known dev issuer and `null` (→ MISSING_COVERAGE) for an unknown one.
- **A9** — `MoodysRatingsClient` + `MorningstarDbrsRatingsClient` exist, are registered, and their `GetRatingAsOfAsync` throws `NotImplementedException` with the pending-spec message (no invented endpoint/auth/response).

---

## Decisions (D1–D14) — ★ = needs your confirmation

| # | Decision | Choice | Rationale |
|---|---|---|---|
| D1 | `Provider` enum member rename location | Rename the member **in place** in `Analysis/NotchLadder.cs` (`Bloomberg`→`Moodys`). Enum stays in that file (pkg02 D1). | Only *code* reference to `Bloomberg` is the enum declaration (grep-verified: no test, model, or Program reference). One-line change ⇒ green. NotchLadder's `Map` already carries Moody's `Aaa…C` aliases, so `MoodysRatingsClient` will later map via `NotchLadder.ToNotch`. |
| D2 ★ | `IEdgarClient.GetFactsAsOfAsync` return type | Return the **raw `EdgarFacts`** record (Cik-keyed) exactly as the package-doc interface declares; add a pure, tested mapper `EdgarFacts.ToFundamentalSnapshot(string issuerId)` that computes the ratios. | Scope says "builds `FundamentalSnapshot`", but `FundamentalSnapshot` needs `IssuerId`, which a CIK-only call cannot honestly supply (P1 — don't fabricate an id). The acceptance checks (A1/A2) probe raw figures + `LatestFilingDate` = `EdgarFacts`. The mapper delivers the "build snapshot" logic for the caller (pkg 08 service) that owns the issuer join. **Alt:** change signature to `GetFactsAsOfAsync(issuerId, cik, asOf, ct)` and return `FundamentalSnapshot` directly — confirm if you prefer this. |
| D3 ★ | XBRL concept selection (`us-gaap`) | TotalDebt ← `LongTermDebtNoncurrent` (+ `LongTermDebtCurrent`/`DebtCurrent` when present); EBITDA ← derive `OperatingIncomeLoss` + `DepreciationDepletionAndAmortization` (fallback `DepreciationAndAmortization`); InterestExpense ← `InterestExpense` (fallback `InterestExpenseNonoperating`); Cash ← `CashAndCashEquivalentsAtCarryingValue`. Any absent concept ⇒ that field `null` (data, not error — 03). | Debt-to-EBITDA uses interest-bearing debt, not total `Liabilities`; EBITDA has no single us-gaap tag so it is derived. All choices are documented + isolated in one concept-map constant so they are easy to tune when we see NordStar's real filing. |
| D4 | As-of filter key | Per concept: keep `units.USD[]` entries with **`filed` ≤ asOf**; select the entry with max `filed` (tie-break max `end`). `LatestFilingDate` = max `filed` across selected concepts; `LatestFilingType` = that entry's `form`. | "As-of" = information *available* by the decision date ⇒ compare the **filing** date, not the period end (a Q3 filed after asOf must not appear). Companyfacts entries carry `filed`+`form`, so one endpoint suffices. |
| D5 | EDGAR endpoint set | **`companyfacts` only** this increment (`/api/xbrl/companyfacts/CIK{cik:0000000000}.json`). Submissions-endpoint join deferred. | Single endpoint yields values + `filed` + `form` — enough for A1/A2 and a self-contained stub. Submissions cross-check is a pkg-08 enrichment. |
| D6 ★ | Retry-with-jitter shape (no Polly) | `Infrastructure/Http/TransientRetryHandler : DelegatingHandler`: retry on `HttpRequestException` + 5xx/408/429, **bounded** `maxAttempts` (default 3), exponential base delay × full jitter (`Random.Shared`), honors `CancellationToken`. **Base delay injected via ctor** (tests pass `TimeSpan.Zero`). Registered per typed client via `.AddHttpMessageHandler(...)`. | Idiomatic, composes with typed clients, transparent to connector code. Connectors under unit test are built with a bare `HttpClient(stub)` (no handler) ⇒ fast/deterministic. **Confirm** attempts/base-delay values. |
| D7 | SSRF allowlist enforcement (P6) | Typed clients get a **fixed `BaseAddress`** (`https://data.sec.gov/`, `https://api.stlouisfed.org/`) set in DI; connector methods build **relative** URIs only — never accept a URL argument. Belt-and-suspenders: assert `request.RequestUri!.Host` ∈ the one allowed host before send. | The model never supplies a URL; host can't be redirected. Matches 06 "connectors only call the fixed hosts; validate/allowlist." |
| D8 | Tool-arg trust (P6) — input validation | `cik`: strip non-digits, reject empty, zero-pad to 10 (`^\d{1,10}$` after strip). `seriesId`: `^[A-Za-z0-9_]+$`. Invalid ⇒ `ArgumentException` (fail-loud, before any I/O). | LLM/tool args are hostile (06); validating prevents path/query injection into the allowlisted URL. |
| D9 | New exception(s) in `Infrastructure/Errors/` | Add **only** `UpstreamServiceException` (BCL-only, sealed): ctor `(string service, string message, Exception? inner = null)`, `Service` prop, `Code = "UPSTREAM_SERVICE_FAILED"`. Maps to 502 when middleware lands (pkg 08/09). | Scope: "minimal … e.g. `UpstreamServiceException` … zero-dependency, no Azure." `ConfigurationException` is unnecessary — `ValidateOnStart` already fails loudly at boot. |
| D10 | EDGAR failure semantics | Non-2xx **or** unparseable JSON ⇒ `UpstreamServiceException("EDGAR", …, inner)`. Individual missing concept ⇒ field `null` (not an error). Zero filings with `filed ≤ asOf` ⇒ `UpstreamServiceException("EDGAR","No company facts on or before {asOf} for CIK {cik}")` **(temporary)**. | 03: "throw `UpstreamServiceException` naming the source; never fabricate; absent field = `null`/`unavailable`." Not hit by A1/A2 (a filing always precedes both asOf dates); refine the empty case to `NotFoundException` when the taxonomy/middleware ships. |
| D11 ★ | Pending Moody's/DBRS "options exist" | Add **nullable** placeholders `PrismOptions.ProviderApis { MoodysApi?, MorningstarApi? }`, each `{ string? BaseUrl; string? ApiKey; }` — **no `[Required]`**, all default `null` (so `ValidateOnStart` is unaffected). Pending clients inject `IOptions<PrismOptions>` + `ILogger<T>` and still `throw NotImplementedException(...)`. | Satisfies scope "class + **options** + registration exist" while inventing **zero** concrete endpoint/auth values (honest not-yet-implemented boundary). **Confirm** you want the placeholder now vs. deferring options entirely until specs land. |
| D12 | `ProviderRatingRecord` / `IProviderRatingsSource` placement | Both in one file `Connectors/IProviderRatingsSource.cs` (boundary contract; mirrors pkg02's enum-in-`NotchLadder` precedent). | The record is the connector-boundary DTO, tightly bound to the interface; keeps `Models/PrismModels.cs` free of source-layer concerns (08 "DTOs never leak connector concerns"). |
| D13 ★ | `ProviderRatingRecord` vs existing `ProviderRating` (near-duplicate) | Keep them **separate**: `ProviderRatingRecord` = raw source record (`RatingActionDate` + `SourceRef`); `ProviderRating` (pkg02 model) = internal reconciliation model (`InputAsOfDate` + `MethodologyDocId`). Reconcile/merge in **pkg 05**. | Flagging the smell now. Do **not** collapse them this increment — the mapping (action-date → stale flag) is decomposer/agent work (05/06). **Confirm** the deferral. |
| D14 | OTel spans without the OTel SDK | Shared `Infrastructure/Telemetry/ConnectorTelemetry.cs` → `static readonly ActivitySource Source = new("FinancialServices.Connectors")`. Each connector wraps its as-of logic in `using var activity = ConnectorTelemetry.Source.StartActivity("Edgar.GetFactsAsOf")` (resp. `"Fred.GetLatestOnOrBefore"`), tagging **safe ids only** (`cik`, `asOf`, `seriesId`, `latestFilingDate`) — never financials (07). | `ActivitySource`/`Activity` are BCL (shared framework) ⇒ zero package. Pkg 07 adds `AddSource("FinancialServices.Connectors")` + exporter and these light up (A4). |

**Reference — canonical XBRL concept map (for D3):**
`us-gaap` → `TotalDebt`: `LongTermDebtNoncurrent` (+`LongTermDebtCurrent`|`DebtCurrent`) ·
`Ebitda`: `OperatingIncomeLoss` + (`DepreciationDepletionAndAmortization`|`DepreciationAndAmortization`) ·
`InterestExpense`: `InterestExpense`|`InterestExpenseNonoperating` ·
`Cash`: `CashAndCashEquivalentsAtCarryingValue`. Unit array = `units.USD`.

---

## Phase 1 — Provider enum realignment (`Analysis/NotchLadder.cs`)

- [ ] **1.1** In `backend/FinancialServices.Api/Analysis/NotchLadder.cs` rename enum member
  `Bloomberg` → `Moodys` (final: `public enum Provider { Moodys, MorningstarDbrs, Msci }`); update the
  `<summary>` if it names providers. *(→ A6; D1; naming 01)*
- [ ] **1.2** Run `dotnet build backend -warnaserror` — confirm **zero** compile references to the old
  member remain (grep-verified none exist outside this file). *(→ A6; P1 gate)*
- [ ] **1.3** Run the existing `FinancialServices.Tests/Analysis/NotchLadderTests.cs` unchanged — all
  65 assertions must still pass (they test string labels, not the enum). *(→ A6; 11 "keep NotchLadder green")*

> Doc drift (OPTIONAL, out of green-build scope — flagged, not required): `architecturalPlan/01`,
> `implementationPlan/00` & `02`, and `PRISM-BUILD-PLAN.md` still print `Bloomberg`. Governance-doc
> sync is a separate housekeeping task; **do not** block package acceptance on it.

## Phase 2 — Options & configuration (`Infrastructure/PrismOptions.cs`, `.env.example`)

- [ ] **2.1** Create `backend/FinancialServices.Api/Infrastructure/PrismOptions.cs`,
  `namespace FinancialServices.Api.Infrastructure;`, `using System.ComponentModel.DataAnnotations;`.
  `public sealed class PrismOptions` with `[Required] public required string FredApiKey { get; init; }`,
  `[Required] public required string SecUserAgent { get; init; }`, `public ModelOptions Models { get; init; } = new();`.
  *(→ A7; 05 verbatim)*
- [ ] **2.2** Nested `public sealed class ModelOptions` with defaults `Orchestrator="gpt-5"`,
  `Provider="gpt-4.1-mini"`, `Fundamentals="gpt-4.1-mini"`, `RedFlag="gpt-5-mini"` (match `.env.example` + 05). *(→ A7)*
- [ ] **2.3** Nested pending-API placeholders (D11): `public ProviderApiOptions ProviderApis { get; init; } = new();`
  → `public sealed class ProviderApiOptions { public ProviderApiEndpoint? MoodysApi { get; init; } public ProviderApiEndpoint? MorningstarApi { get; init; } }`
  and `public sealed class ProviderApiEndpoint { public string? BaseUrl { get; init; } public string? ApiKey { get; init; } }`
  — **no `[Required]`**, all nullable. *(→ A9 "options exist"; D11)*
- [ ] **2.4** Update `.env.example`: under the existing "Prism: real data connectors" block add commented
  **pending** placeholders `# Prism__ProviderApis__MoodysApi__BaseUrl=` / `__ApiKey=` and the Morningstar pair,
  with a `# (pending API spec — leave blank)` note. *(05 "keep .env.example current"; D11)*

## Phase 3 — Infrastructure (errors, telemetry, retry, DI extensions)

- [ ] **3.1** Create `backend/FinancialServices.Api/Infrastructure/Errors/UpstreamServiceException.cs`:
  `sealed class UpstreamServiceException : Exception` with `string Service`, `string Code = "UPSTREAM_SERVICE_FAILED"`,
  ctor `(string service, string message, Exception? inner = null)`. BCL-only. *(→ A4; 03 taxonomy; D9)*
- [ ] **3.2** Create `backend/FinancialServices.Api/Infrastructure/Telemetry/ConnectorTelemetry.cs`:
  `static class ConnectorTelemetry { public static readonly ActivitySource Source = new("FinancialServices.Connectors"); }`
  (`using System.Diagnostics;`). *(→ A4; 07; D14)*
- [ ] **3.3** Create `backend/FinancialServices.Api/Infrastructure/Http/TransientRetryHandler.cs`:
  `sealed class TransientRetryHandler : DelegatingHandler` — ctor `(int maxAttempts = 3, TimeSpan? baseDelay = null)`;
  retry on `HttpRequestException`/5xx/408/429; exponential × full jitter via `Random.Shared`; honor `ct`;
  never retry a cancellation. *(→ A4; 03 "backoff with jitter, bounded"; D6)*
- [ ] **3.4** Create `backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs`,
  `namespace FinancialServices.Api.Infrastructure;`. Add
  `public static IServiceCollection AddPrismOptions(this IServiceCollection services, IConfiguration cfg)`:
  `services.AddOptions<PrismOptions>().Bind(cfg.GetSection("Prism")).ValidateDataAnnotations().ValidateOnStart(); return services;`.
  *(→ A7; 05)*
- [ ] **3.5** In the same file add
  `public static IServiceCollection AddConnectors(this IServiceCollection services)`. Register the two typed
  clients with fixed `BaseAddress` + `TransientRetryHandler` (see Phase 4/5 for headers), and the three
  `IProviderRatingsSource` implementations (multi-registration). *(→ A4/A8/A9; 02 "Connectors/"; D7)*
- [ ] **3.6** Verify `ValidateDataAnnotations()` resolves under `Microsoft.NET.Sdk.Web` (it is in the
  ASP.NET Core shared framework). **Only if the build errors**, add package
  `Microsoft.Extensions.Options.DataAnnotations` (BCL, no Azure) — otherwise add **no** package. *(dependency-light gate)*

## Phase 4 — Connectors: as-of helper + EDGAR + FRED (real, fully specified)

- [ ] **4.1** Create `backend/FinancialServices.Api/Connectors/AsOf.cs`:
  `static class AsOf { public static T? LatestOnOrBefore<T>(IEnumerable<T> items, Func<T, DateTimeOffset> timestamp, DateTimeOffset asOf) }`
  — pure; returns the max-timestamp item with `timestamp ≤ asOf`, else `default`. *(08 "single as-of utility"; D4)*
- [ ] **4.2** Create `backend/FinancialServices.Api/Connectors/IEdgarClient.cs`:
  `interface IEdgarClient { Task<EdgarFacts> GetFactsAsOfAsync(string cik, DateTimeOffset asOf, CancellationToken ct); }`
  **plus** `sealed record EdgarFacts(string Cik, decimal? TotalDebt, decimal? Ebitda, decimal? InterestExpense, decimal? Cash, DateTimeOffset LatestFilingDate, string LatestFilingType)`. *(→ A1/A2; D2; P7)*
- [ ] **4.3** Add the pure mapper `public FundamentalSnapshot ToFundamentalSnapshot(string issuerId)` to
  `EdgarFacts` (same file): `DebtToEbitda = TotalDebt/Ebitda` (null if either null or `Ebitda==0`),
  `InterestCoverage = Ebitda/InterestExpense` (null if either null or `InterestExpense==0`),
  `CashAndEquivalents = Cash`, `FilingDate=LatestFilingDate`, `FilingType=LatestFilingType`. *(→ A1; D2; "builds FundamentalSnapshot")*
- [ ] **4.4** Create `backend/FinancialServices.Api/Connectors/EdgarClient.cs`:
  `sealed class EdgarClient : IEdgarClient` — ctor `(HttpClient http, ILogger<EdgarClient> logger)` (typed-client;
  `BaseAddress`+`User-Agent` set in DI). Validate `cik` (D8), GET relative
  `api/xbrl/companyfacts/CIK{cik:0000000000}.json`, assert host (D7), wrap in `ConnectorTelemetry` span (D14). *(→ A4; D5/D7/D8/D14)*
- [ ] **4.5** In `EdgarClient`, parse the companyfacts JSON with `System.Text.Json`; for each concept in the
  D3 map read `units.USD[]`, apply `filed ≤ asOf`, select via `AsOf.LatestOnOrBefore` (key `filed`); derive
  `LatestFilingDate`/`LatestFilingType` from the max selected entry; absent concept ⇒ `null`. *(→ A1/A2; D3/D4)*
- [ ] **4.6** In `EdgarClient`, on non-2xx or JSON parse failure throw `UpstreamServiceException("EDGAR", …, inner)`;
  on zero `filed ≤ asOf` throw the D10 "no facts on/before" message. **Never** fabricate a value. *(→ A4; 03; D10)*
- [ ] **4.7** Create `backend/FinancialServices.Api/Connectors/IFredClient.cs`:
  `interface IFredClient { Task<decimal> GetLatestOnOrBeforeAsync(string seriesId, DateTimeOffset asOf, CancellationToken ct); }`. *(→ A3; P7)*
- [ ] **4.8** Create `backend/FinancialServices.Api/Connectors/FredClient.cs`:
  `sealed class FredClient : IFredClient` — ctor `(HttpClient http, IOptions<PrismOptions> options, ILogger<FredClient> logger)`.
  Validate `seriesId` (D8); GET relative `fred/series/observations?series_id={seriesId}&api_key={FredApiKey}&file_type=json&observation_end={asOf:yyyy-MM-dd}&sort_order=desc`;
  assert host (D7); span (D14). *(→ A3/A4; D7/D8/D14)*
- [ ] **4.9** In `FredClient`, parse `observations[]`, drop `value == "."`, apply `date ≤ asOf`, pick max `date`,
  parse `decimal` with `CultureInfo.InvariantCulture`; none ⇒ `UpstreamServiceException("FRED", …)`. *(→ A3/A4; 03)*

## Phase 5 — Connectors: provider-ratings abstraction

- [ ] **5.1** Create `backend/FinancialServices.Api/Connectors/IProviderRatingsSource.cs`
  (`using FinancialServices.Api.Analysis;` + `using FinancialServices.Api.Models;`):
  `interface IProviderRatingsSource { Provider Provider { get; } Task<ProviderRatingRecord?> GetRatingAsOfAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct); }`
  **plus** `sealed record ProviderRatingRecord(Provider Provider, string IssuerId, string Letter, int Notch, DateTimeOffset AsOfDate, DateTimeOffset RatingActionDate, IReadOnlyList<RatingFactor> Factors, string SourceRef)`. *(package doc; D12/D13; P3)*
- [ ] **5.2** Create `backend/FinancialServices.Api/Connectors/SyntheticRatingsSource.cs`:
  `sealed class SyntheticRatingsSource : IProviderRatingsSource` with `Provider => Provider.Msci` and a **clearly
  labeled** class comment ("SYNTHETIC / DEV — labeled-synthetic MSCI slot (P1: labeled, not a runtime fake of a
  real provider)"). Back it with a small in-code `static readonly Dictionary<string, ProviderRatingRecord>` keyed
  by lowercase `issuerId`; seed at least `"nordstar"` (Letter `"BBB-"`, `Notch = NotchLadder.ToNotch("BBB-")`, a
  deliberately older `RatingActionDate`, `SourceRef = "synthetic:msci:nordstar"`). *(→ A8; P1 labeled synthetic; memory "MSCI = synthetic 3rd slot")*
- [ ] **5.3** In `SyntheticRatingsSource.GetRatingAsOfAsync`: unknown `issuerId` ⇒ `null` (→ MISSING_COVERAGE);
  known but `RatingActionDate > asOf` ⇒ `null` (as-of; P3); else the record. Wrap in a `ConnectorTelemetry` span
  for parity. `Task.FromResult` (no I/O) but keep the `async`-signature + `ct`. *(→ A8; P3/P7; D14)*
- [ ] **5.4** Create `backend/FinancialServices.Api/Connectors/MoodysRatingsClient.cs`:
  `sealed class MoodysRatingsClient : IProviderRatingsSource` with `Provider => Provider.Moodys`, ctor
  `(IOptions<PrismOptions> options, ILogger<MoodysRatingsClient> logger)`; `GetRatingAsOfAsync` ⇒
  `throw new NotImplementedException("Moody's API integration pending product/endpoint/auth confirmation")`.
  No `HttpClient`, no invented URL. *(→ A9; scope 5; P1 honest boundary)*
- [ ] **5.5** Create `backend/FinancialServices.Api/Connectors/MorningstarDbrsRatingsClient.cs`:
  same shape, `Provider => Provider.MorningstarDbrs`, message `"Morningstar/DBRS API integration pending product/endpoint/auth confirmation"`. *(→ A9; scope 5; P1)*

## Phase 6 — DI wiring (`Program.cs` + `AddConnectors` bodies)

- [ ] **6.1** Flesh out `AddConnectors` (Phase 3.5):
  `services.AddHttpClient<IEdgarClient, EdgarClient>().ConfigureHttpClient((sp, c) => { c.BaseAddress = new Uri("https://data.sec.gov/"); c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", sp.GetRequiredService<IOptions<PrismOptions>>().Value.SecUserAgent); }).AddHttpMessageHandler(() => new TransientRetryHandler());`. *(→ A4; D6/D7; 06 SEC UA)*
- [ ] **6.2** `services.AddHttpClient<IFredClient, FredClient>(c => c.BaseAddress = new Uri("https://api.stlouisfed.org/")).AddHttpMessageHandler(() => new TransientRetryHandler());`. *(→ A3/A4; D6/D7)*
- [ ] **6.3** Register the ratings sources (multi-registration by interface):
  `services.AddSingleton<IProviderRatingsSource, SyntheticRatingsSource>();` + `MoodysRatingsClient` + `MorningstarDbrsRatingsClient`.
  Add a comment: consumers (pkg 05/06) inject `IEnumerable<IProviderRatingsSource>` and select by `.Provider`. *(→ A8/A9)*
- [ ] **6.4** In `backend/FinancialServices.Api/Program.cs`, after `AddHealthChecks()` add
  `builder.Services.AddPrismOptions(builder.Configuration).AddConnectors();` (+ `using FinancialServices.Api.Infrastructure;`).
  Keep the health + `/` endpoints. *(→ A7; 05 ValidateOnStart)*
- [ ] **6.5** Note (no code): `ValidateOnStart` now makes the app **refuse to boot** without
  `Prism__FredApiKey` + `Prism__SecUserAgent` — intended P1 fail-loud. No host-booting test exists
  (`ScaffoldTests`/`NotchLadderTests` don't use `WebApplicationFactory`), so `dotnet test` stays green.
  Flag for pkg 08/11: a future integration test must supply these settings. *(P1; 11)*

## Phase 7 — Tests (`FinancialServices.Tests`, stubbed handler — no live network)

- [ ] **7.1** Create `backend/FinancialServices.Tests/TestSupport/StubHttpMessageHandler.cs`:
  `sealed class StubHttpMessageHandler : HttpMessageHandler` returning a queued/canned `HttpResponseMessage`
  (status + JSON body), recording request count + last URI. Hand-rolled (no Moq) — dependency-light. *(→ A5; 11)*
- [ ] **7.2** Create `backend/FinancialServices.Tests/Connectors/EdgarClientTests.cs` with an inline
  NordStar companyfacts fixture: `CashAndCashEquivalentsAtCarryingValue` has two `USD` entries —
  X `{end:2025-12-31, filed:2026-03-01, val:100, form:"10-Q"}` and Y `{end:2026-03-31, filed:2026-05-20, val:150, form:"10-Q"}`
  (plus at least one debt + EBITDA-input + interest concept). Build `EdgarClient` with `new HttpClient(stub){BaseAddress=…}`. *(→ A5)*
- [ ] **7.3** **E1 (A1):** `GetFactsAsOfAsync(cik, 2026-06-25)` ⇒ `Cash==150`, `LatestFilingDate==2026-05-20`, `LatestFilingType=="10-Q"`. *(→ A1)*
- [ ] **7.4** **E2 (A2):** `GetFactsAsOfAsync(cik, 2026-04-01)` ⇒ `Cash==100`, `LatestFilingDate==2026-03-01` (Y excluded — proves as-of, no hindsight). *(→ A2)*
- [ ] **7.5** **E3 (A4):** stub returns 500 ⇒ `GetFactsAsOfAsync` throws `UpstreamServiceException` with `Service=="EDGAR"`. *(→ A4; D10)*
- [ ] **7.6** **E4 (D2):** `EdgarFacts.ToFundamentalSnapshot("nordstar")` computes `DebtToEbitda`/`InterestCoverage` and passes `IssuerId` through; a `null` EBITDA ⇒ `DebtToEbitda==null` (no fabrication). *(→ A1; D2/D3)*
- [ ] **7.7** Create `backend/FinancialServices.Tests/Connectors/FredClientTests.cs` with fixture
  `observations:[{date:2026-03-31,value:"1.23"},{date:2026-06-30,value:"1.45"},{date:2026-07-15,value:"."}]`;
  build `FredClient` with `new HttpClient(stub){BaseAddress=…}` + an `IOptions<PrismOptions>` carrying a dummy key. *(→ A5)*
- [ ] **7.8** **F1 (A3):** `GetLatestOnOrBeforeAsync("BAMLC0A0CM", 2026-05-01)` ⇒ `1.23` (dated ≤ as-of);
  `…("BAMLC0A0CM", 2026-07-20)` ⇒ `1.45` (skips the `"."`). *(→ A3)*
- [ ] **7.9** **F2 (A4):** stub returns 500 (or empty observations) ⇒ `UpstreamServiceException` with `Service=="FRED"`. *(→ A4)*
- [ ] **7.10** Create `backend/FinancialServices.Tests/Connectors/ProviderRatingsSourceTests.cs`:
  **S1 (A8)** `SyntheticRatingsSource.GetRatingAsOfAsync("nordstar", asOf ≥ actionDate)` ⇒ non-null record
  (`Provider==Msci`, `Notch==NotchLadder.ToNotch("BBB-")`); **S2 (A8)** `…("unknown-issuer", asOf)` ⇒ `null`. *(→ A8)*
- [ ] **7.11** **P1/P2 (A9):** `MoodysRatingsClient` and `MorningstarDbrsRatingsClient` `.GetRatingAsOfAsync(…)`
  each throw `NotImplementedException` (assert the pending message substring). *(→ A9)*
- [ ] **7.12** *(optional, fast)* `TransientRetryHandlerTests`: with `baseDelay: TimeSpan.Zero`, a handler that
  returns 500 twice then 200 succeeds and is invoked exactly 3×; a 400 is **not** retried. *(D6 confidence)*
- [ ] **7.13** *(optional)* `PrismOptionsTests`: `Validator.TryValidateObject` fails when `FredApiKey`/`SecUserAgent`
  are blank (proves the `[Required]` wiring `ValidateOnStart` relies on). *(→ A7)*
- [ ] **7.14** Run `dotnet build backend -warnaserror` + `dotnet test backend` — all green (old 65 + new). *(11 quality gate; A6)*

---

## File change summary

| File | Action | Description |
|---|---|---|
| `backend/FinancialServices.Api/Analysis/NotchLadder.cs` | Modify | Rename enum member `Bloomberg`→`Moodys` (1 line + summary) |
| `backend/FinancialServices.Api/Infrastructure/PrismOptions.cs` | Create | Options: `[Required]` FredApiKey/SecUserAgent, `Models`, nullable `ProviderApis` |
| `backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs` | Create | `AddPrismOptions` + `AddConnectors` |
| `backend/FinancialServices.Api/Infrastructure/Errors/UpstreamServiceException.cs` | Create | 502-mapping upstream failure (BCL-only) |
| `backend/FinancialServices.Api/Infrastructure/Telemetry/ConnectorTelemetry.cs` | Create | Shared `ActivitySource` (zero-package OTel spans) |
| `backend/FinancialServices.Api/Infrastructure/Http/TransientRetryHandler.cs` | Create | Bounded retry + full jitter `DelegatingHandler` |
| `backend/FinancialServices.Api/Connectors/AsOf.cs` | Create | Pure "latest ≤ asOf" selector |
| `backend/FinancialServices.Api/Connectors/IEdgarClient.cs` | Create | Interface + `EdgarFacts` record + `ToFundamentalSnapshot` mapper |
| `backend/FinancialServices.Api/Connectors/EdgarClient.cs` | Create | Real `data.sec.gov` companyfacts, as-of filter, span, fail-loud |
| `backend/FinancialServices.Api/Connectors/IFredClient.cs` | Create | Interface |
| `backend/FinancialServices.Api/Connectors/FredClient.cs` | Create | Real `api.stlouisfed.org` latest-on-or-before, span, fail-loud |
| `backend/FinancialServices.Api/Connectors/IProviderRatingsSource.cs` | Create | Interface + `ProviderRatingRecord` |
| `backend/FinancialServices.Api/Connectors/SyntheticRatingsSource.cs` | Create | Labeled-synthetic MSCI dev source (in-code fixture) |
| `backend/FinancialServices.Api/Connectors/MoodysRatingsClient.cs` | Create | Pending-spec stub → `NotImplementedException` |
| `backend/FinancialServices.Api/Connectors/MorningstarDbrsRatingsClient.cs` | Create | Pending-spec stub → `NotImplementedException` |
| `backend/FinancialServices.Api/Program.cs` | Modify | `+ AddPrismOptions(cfg).AddConnectors()` |
| `.env.example` | Modify | Commented pending `Prism__ProviderApis__*` placeholders |
| `backend/FinancialServices.Tests/TestSupport/StubHttpMessageHandler.cs` | Create | Canned-response handler (no network, no Moq) |
| `backend/FinancialServices.Tests/Connectors/EdgarClientTests.cs` | Create | E1–E4 (as-of boundary, failure, mapper) |
| `backend/FinancialServices.Tests/Connectors/FredClientTests.cs` | Create | F1–F2 (latest ≤ asOf, `"."` skip, failure) |
| `backend/FinancialServices.Tests/Connectors/ProviderRatingsSourceTests.cs` | Create | S1/S2 + P1/P2 |
| `backend/FinancialServices.Tests/Infrastructure/*` (optional) | Create | `TransientRetryHandlerTests`, `PrismOptionsTests` |

**Totals:** ~18 create + 3 modify (excludes 2 optional test files). New NuGet packages: **0** (verify D-3.6).

## Acceptance mapping

| Acceptance | Satisfied by |
|---|---|
| A1 (Q3 + LatestFilingDate) | 4.2–4.5, **7.3 (E1)** |
| A2 (as-of filter → Q2 only) | 4.5, **7.4 (E2)** |
| A3 (FRED ≤ as-of) | 4.7–4.9, **7.8 (F1)** |
| A4 (spans + actionable errors) | 3.1–3.3, 4.4/4.6/4.8/4.9, **7.5 (E3)**, **7.9 (F2)** |
| A5 (stubbed `HttpMessageHandler`) | 7.1–7.9 |
| A6 (enum rename green) | 1.1–1.3, 7.14 |
| A7 (`PrismOptions` + ValidateOnStart) | 2.1–2.2, 3.4, 6.4, 7.13 |
| A8 (Synthetic known/unknown) | 5.2–5.3, **7.10 (S1/S2)** |
| A9 (pending stubs throw) | 5.4–5.5, 6.3, **7.11 (P1/P2)** |

---

## Open decisions / flags (for the implementor + adversary)

1. **D2 ★ — EDGAR return type.** Recommending raw `EdgarFacts` + `ToFundamentalSnapshot(issuerId)` mapper
   (can't fabricate `IssuerId` from a CIK; matches the package-doc interface + acceptance). Alternative:
   `GetFactsAsOfAsync(issuerId, cik, asOf, ct)` returning `FundamentalSnapshot` directly. **Confirm.**
2. **D3 ★ — XBRL concept selection.** Debt/EBITDA/Interest/Cash tag choices are best-effort agency-standard
   and isolated in one constant; will need a tweak once we inspect NordStar's actual filing. **Confirm the tags.**
3. **D6 ★ — retry policy shape.** `maxAttempts=3`, exponential + full jitter, injectable base delay.
   **Confirm** attempts/delay (kept modest to be SEC/FRED rate-limit-polite per the package doc).
4. **D11 ★ — pending-provider options.** Adding nullable `PrismOptions.ProviderApis.{MoodysApi,MorningstarApi}`
   (BaseUrl/ApiKey, no `[Required]`) so "options exist" without inventing values. **Confirm** vs. deferring options.
5. **D13 ★ — `ProviderRatingRecord` vs `ProviderRating` duplication.** Kept separate; reconcile in pkg 05.
   **Confirm the deferral.**
6. **P1 nuance (pre-empt the adversary):** `SyntheticRatingsSource` is a **clearly labeled** synthetic MSCI
   slot (MSCI has no hackathon API), **not** a silent fallback masking a real provider — consistent with P1's
   intent and the package doc, which lists it as a first-class implementation.
7. **Behavior change:** `ValidateOnStart` makes the API refuse to boot without `Prism__FredApiKey` +
   `Prism__SecUserAgent` (intended P1). Set them in `.env`/user-secrets to `dotnet run`. Unit tests unaffected.
8. **Dependency-light gate:** confirm `ValidateDataAnnotations()`, `AddHttpClient`, and `ActivitySource` all
   resolve under `Microsoft.NET.Sdk.Web` with **no** new package (expected). Only if not, add the single BCL
   package `Microsoft.Extensions.Options.DataAnnotations` — never an Azure/Polly package this increment.
9. **Explicitly deferred (not this increment):** Treasury FiscalData connector (package doc §C, cut candidate);
   EDGAR submissions-endpoint cross-check (D5); Cosmos/Search caching of fetched records; exception→HTTP
   middleware mapping (pkg 08/09); real Moody's/DBRS HTTP; governance-doc `Bloomberg`→`Moodys` sync.

---

## Verification (run after implementation — 11 quality gate)

```pwsh
dotnet build C:\GitHub\DataProvidersHackPOC\backend\FinancialServices.slnx -warnaserror
dotnet test  C:\GitHub\DataProvidersHackPOC\backend\FinancialServices.slnx
```

Both must be green (existing 65 NotchLadder/scaffold assertions + the new connector suite), zero warnings.
