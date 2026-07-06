<!-- markdownlint-disable-file -->
# Implementation Changes: Package 04 — Data Connectors (EDGAR / FRED + provider-ratings abstraction)

**Plan:** `.copilot-tracking/plans/2026-07-06/04-plan.md`
**Work package:** `implementationPlan/04-real-data-connectors.md`
**Date:** 2026-07-06

## Files Created
| File | Description |
|---|---|
| `backend/FinancialServices.Api/Infrastructure/PrismOptions.cs` | `sealed PrismOptions` bound to section `Prism`: `[Required] required FredApiKey`/`SecUserAgent`, nested `ModelOptions` defaults, and nullable `ProviderApis.{MoodysApi,MorningstarApi}` placeholders (no `[Required]` — D11). |
| `backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs` | `AddPrismOptions` (bind + `ValidateDataAnnotations().ValidateOnStart()`) and `AddConnectors` (EDGAR/FRED typed clients with fixed `BaseAddress` + `TransientRetryHandler`; three `IProviderRatingsSource` multi-registrations). |
| `backend/FinancialServices.Api/Infrastructure/Errors/UpstreamServiceException.cs` | Sealed BCL-only exception (`Service`, `Code = "UPSTREAM_SERVICE_FAILED"`) → 502 when middleware lands (D9). |
| `backend/FinancialServices.Api/Infrastructure/Telemetry/ConnectorTelemetry.cs` | Shared `static ActivitySource("FinancialServices.Connectors")` — zero-package OTel spans (D14). |
| `backend/FinancialServices.Api/Infrastructure/Http/TransientRetryHandler.cs` | `sealed DelegatingHandler`, `maxAttempts=3`, exponential + **full jitter** (`Random.Shared`), ctor-injected base delay (tests pass `TimeSpan.Zero`), honors `ct`, never retries a cancel (D6). |
| `backend/FinancialServices.Api/Connectors/AsOf.cs` | Pure `internal static LatestOnOrBefore<T>` — the single as-of selector (latest timestamp ≤ asOf; D4). |
| `backend/FinancialServices.Api/Connectors/IEdgarClient.cs` | `IEdgarClient` + `sealed record EdgarFacts` (raw Cik-keyed figures) + pure `ToFundamentalSnapshot(issuerId)` mapper (D2/D3). |
| `backend/FinancialServices.Api/Connectors/EdgarClient.cs` | Real `data.sec.gov` companyfacts: CIK validation + zero-pad (D8), host allowlist (D7), `System.Text.Json` XBRL parse of the D3 concept map, `filed ≤ asOf` select with end tie-break, `LatestFilingDate/Type`, span, fail-loud `UpstreamServiceException` (D10). |
| `backend/FinancialServices.Api/Connectors/IFredClient.cs` | `IFredClient.GetLatestOnOrBeforeAsync`. |
| `backend/FinancialServices.Api/Connectors/FredClient.cs` | Real `api.stlouisfed.org`: seriesId validation (D8), host allowlist (D7), `observation_end=asOf`, skip `"."`, latest obs ≤ asOf via `AsOf`, invariant-culture decimal, span, fail-loud. |
| `backend/FinancialServices.Api/Connectors/IProviderRatingsSource.cs` | `IProviderRatingsSource` + `sealed record ProviderRatingRecord` (raw source record: `RatingActionDate` + `SourceRef`), kept separate from `ProviderRating` (D12/D13). |
| `backend/FinancialServices.Api/Connectors/SyntheticRatingsSource.cs` | Labeled-synthetic `Provider.Msci` dev source; in-code `nordstar` fixture (`BBB-`, older action date, `synthetic:` SourceRef); unknown issuer/as-of → `null` (A8; P1 labeled). |
| `backend/FinancialServices.Api/Connectors/MoodysRatingsClient.cs` | Pending-spec `Provider.Moodys` stub — options + logger wired; `GetRatingAsOfAsync` throws `NotImplementedException("Moody's API integration pending …")` (A9; no invented endpoint/auth). |
| `backend/FinancialServices.Api/Connectors/MorningstarDbrsRatingsClient.cs` | Pending-spec `Provider.MorningstarDbrs` stub — same shape; throws the Morningstar/DBRS pending message. |
| `backend/FinancialServices.Tests/TestSupport/StubHttpMessageHandler.cs` | Hand-rolled canned-response `HttpMessageHandler` (no network, no Moq); replays queued responses, records count + last URI. |
| `backend/FinancialServices.Tests/Connectors/EdgarClientTests.cs` | E1–E4: as-of after/before latest filing, 500→`UpstreamServiceException("EDGAR")`, mapper ratios + null-EBITDA. |
| `backend/FinancialServices.Tests/Connectors/FredClientTests.cs` | F1–F3: latest ≤ asOf, `"."`-skip, 500→`UpstreamServiceException("FRED")`. |
| `backend/FinancialServices.Tests/Connectors/ProviderRatingsSourceTests.cs` | S1/S2 synthetic known→record / unknown→null; P1/P2 pending clients throw `NotImplementedException`. |
| `backend/FinancialServices.Tests/Infrastructure/TransientRetryHandlerTests.cs` | Retry 5xx-then-200 invoked 3×; 400 not retried (D6). |
| `backend/FinancialServices.Tests/Infrastructure/PrismOptionsTests.cs` | Blank required secrets fail `Validator`; populated pass; `ProviderApis` stay optional (A7). |

## Files Modified
| File | Changes |
|---|---|
| `backend/FinancialServices.Api/Analysis/NotchLadder.cs` | Renamed enum member `Provider.Bloomberg` → `Provider.Moodys` (final set `Moodys, MorningstarDbrs, Msci`) + updated the `<summary>` (A6/D1). One-line code change; the 65 NotchLadder tests test string labels, not the enum, so they stay green. |
| `backend/FinancialServices.Api/Program.cs` | Added `using FinancialServices.Api.Infrastructure;` and `builder.Services.AddPrismOptions(builder.Configuration).AddConnectors();` after `AddHealthChecks()` (A7; ValidateOnStart). |
| `.env.example` | Added a commented **pending** `Prism__ProviderApis__{MoodysApi,MorningstarApi}__{BaseUrl,ApiKey}` block with a "leave blank" note (D11). |
| `backend/FinancialServices.Tests/FinancialServices.Tests.csproj` | Added `<FrameworkReference Include="Microsoft.AspNetCore.App" />` so the non-Web test project can reference `Microsoft.Extensions.Options`/`Logging.Abstractions`. **Zero new NuGet packages** (shared-framework reference). |

## Plan Deviations
- **Tests.csproj `FrameworkReference` (not enumerated in the plan's file list).** The plan requires the connector tests to construct `Options.Create<PrismOptions>(…)` and `NullLogger<T>.Instance`, whose types live in the ASP.NET Core shared framework. A `Microsoft.NET.Sdk` (non-Web) test project does not see those transitively through the project reference, so I added `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. This is a reference to the already-installed shared framework — it adds **zero NuGet packages** and honors the "ZERO new packages" constraint. It also pre-enables `WebApplicationFactory<Program>` for pkg 08/11.
- **D3 EBITDA / debt derivation nuance (documented, not a scope change).** `TotalDebt` requires `LongTermDebtNoncurrent` present (else `null`); the current-portion (`LongTermDebtCurrent`/`DebtCurrent`) is additive when present. `Ebitda` requires `OperatingIncomeLoss` present (else `null`); D&A is additive when present. Absent → `null` (never fabricated), exactly per D3.
- No other deviations. D2, D6, D11, D13 applied exactly as the orchestrator approved.

## Verification
- `dotnet build backend/FinancialServices.slnx -warnaserror`: **Build succeeded — 0 Warning(s), 0 Error(s)** (nullable + `TreatWarningsAsErrors` on both projects).
- `dotnet test backend/FinancialServices.slnx`: **Passed! — Failed: 0, Passed: 81, Skipped: 0, Total: 81** (65 existing NotchLadder/scaffold + 16 new connector/infra tests). Duration ~54 ms — **no live network** (all HTTP via `StubHttpMessageHandler`).
- `ValidateDataAnnotations()`, `AddHttpClient`, `ActivitySource` all resolved under `Microsoft.NET.Sdk.Web` with **no** new package (D-3.6 confirmed).

## Completed Tasks
- [x] Phase 1: Provider enum realignment — 3/3
- [x] Phase 2: Options & configuration — 4/4
- [x] Phase 3: Infrastructure (errors, telemetry, retry, DI) — 6/6
- [x] Phase 4: Connectors — as-of + EDGAR + FRED — 9/9
- [x] Phase 5: Provider-ratings abstraction — 5/5
- [x] Phase 6: DI wiring (`Program.cs` + `AddConnectors`) — 5/5
- [x] Phase 7: Tests (stubbed handler, no live network) — 14/14 (incl. both optional suites)
- Workflow visualization: **N/A for package 04** (no runtime workflow node/agent added; connectors become nodes when the engines/agents ship in pkg 05/06, which own the WorkflowPage update).

## Acceptance
| Acceptance | Result |
|---|---|
| A1 — `GetFactsAsOfAsync(cik, 2026-06-25)` → Q3 figures + `LatestFilingDate` | ✅ `EdgarClientTests.GetFactsAsOf_AfterLatestFiling_ReturnsQ3Figures` (Cash 150, 2026-05-20, 10-Q) |
| A2 — same call `asOf=2026-04-01` → only Q2 (no hindsight) | ✅ `…_BeforeLatestFiling_ReturnsOnlyQ2Figures` (Cash 100, 2026-03-01) |
| A3 — `GetLatestOnOrBeforeAsync("BAMLC0A0CM", asOf)` ≤ as-of | ✅ `FredClientTests` (1.23 on/before; 1.45 skipping `"."`) |
| A4 — spans + actionable errors, no silent fallback | ✅ `ConnectorTelemetry` spans; 500 → `UpstreamServiceException` (EDGAR/FRED) |
| A5 — stubbed `HttpMessageHandler` for as-of boundary | ✅ `StubHttpMessageHandler` (no live network) |
| A6 — `Bloomberg`→`Moodys`; build + NotchLadder tests green | ✅ 65 NotchLadder/scaffold tests unchanged and green |
| A7 — `PrismOptions` + `ValidateOnStart` | ✅ `AddPrismOptions`; `PrismOptionsTests` proves `[Required]` wiring |
| A8 — Synthetic known→record / unknown→null | ✅ `ProviderRatingsSourceTests` S1/S2 |
| A9 — pending Moody's/Morningstar stubs throw | ✅ `ProviderRatingsSourceTests` P1/P2 (message contains "pending") |

## Residual risks (deferred — not in scope for pkg 04)
| Item | Rationale | Target package |
|---|---|---|
| Real Moody's / Morningstar DBRS HTTP (endpoint/auth/response) | Product spec TBC (user away); stubs throw + nullable options exist so wiring is honest (P1). | 05/06 once specs land |
| `ProviderRatingRecord` ↔ `ProviderRating` reconciliation (action-date → stale flag) | Decomposer/agent work; kept separate per D13. | 05 |
| EDGAR submissions-endpoint cross-check; Treasury FiscalData connector | Enrichment / cut-candidate; companyfacts alone satisfies A1/A2. | 08 (submissions); Treasury = cut candidate |
| Cosmos/Search caching of fetched records with provenance | Persistence concern. | 07/08 |
| Exception → HTTP 502 middleware mapping | Standard error-shape middleware. | 08/09 |
| Governance-doc `Bloomberg`→`Moodys` sync (01, 00, 02, PRISM-BUILD-PLAN) | Doc housekeeping; out of green-build scope. | doc pass |
| `ValidateOnStart` makes the API refuse to boot without `Prism__FredApiKey` + `Prism__SecUserAgent` | Intended P1 fail-loud; no host-booting test exists so `dotnet test` stays green. | 08/11 integration test supplies settings |

---

## Corrections (post-adversarial-review)

Applied the 10 adversarial fixes (ARC-01…10 / STK-01…08 / A4). **Build 0-warning (`-warnaserror`),
`dotnet test` = Failed: 0, Passed: 95, Skipped: 0 (was 81; +14 new/rebalanced tests), ~47 ms, no live
network.** Per-fix status:

| # | Fix | Status | Notes |
|---|---|---|---|
| 1 | Transport faults → `UpstreamServiceException` (Critical, ARC-01/A4) | **done** | `EdgarClient`/`FredClient` route every send through a `SendAsync` choke point wrapping `HttpRequestException` **and** timeout `OperationCanceledException` (`when (!ct.IsCancellationRequested)`) → `UpstreamServiceException`; the caller's real cancellation still propagates. Tests: `…_TransportFault_ThrowsUpstreamServiceException` (EDGAR + FRED) via new `ThrowingHttpMessageHandler`. |
| 2 | `HttpClient.Timeout = 15s` (Critical, ARC-02) | **done** | Set on both typed clients in `AddConnectors`. |
| 3 | Pure `FundamentalsCalculator` + `EdgarClient` I/O-only (Critical, P2, ARC-05/STK-01) | **done** | New `Analysis/FundamentalsCalculator.cs` (+ raw `XbrlFact`/`EdgarCompanyFacts`). Flow concepts = **annual-only** (frame `CY{fy}` or 365d±20d); OI+D&A from the **same accession**; instant debt/cash as-of; debt fallback chain `LongTermDebtNoncurrent`(+current)→`LongTermDebt`→`LongTermDebtAndCapitalLeaseObligations`→`Liabilities` (matched concept logged; absent → **null**); period selection = max `end`≤asOf **then** max `filed`. `EdgarClient` now returns raw facts only. 6 isolation tests incl. annual+quarter-share-end, restated-old-period-filed-after-newer, missing-debt→null. TTM-from-4-quarters noted as future refinement. |
| 4 | `LatestFilingDate` from submissions API (High, STK-02) | **done** | `EdgarClient` calls `data.sec.gov/submissions/CIK{cik}.json`; `LatestFilingDate`/`Type` = max `filingDate`≤asOf across **all** forms (8-Ks included). The fundamentals' own `FilingDate` (newest filing among used facts) is exposed separately on `FundamentalSnapshot`. Test proves an 8-K moves `LatestFilingDate` without touching the leverage filing. |
| 5 | FRED key log leak (High, P6, ARC-04) | **done** | `appsettings.json` adds `System.Net.Http.HttpClient: Warning` (suppresses framework request logging of the `?api_key=` URL). Confirmed the custom OTel span tags only `seriesId`/`asOf`/`observationDate` — never the URL/api_key. |
| 6 | Pending provider stubs return null, not throw (High, ARC-03) | **done** | `MoodysRatingsClient`/`MorningstarDbrsRatingsClient` return `null` (→ `MISSING_COVERAGE`) + `LogWarning("…integration is pending…")`; interface XML doc updated (null also covers not-yet-configured). Tests assert null + a captured warning via new `CapturingLogger<T>` (replaces the old `NotImplementedException` asserts). |
| 7 | Retry handler GET-only + Retry-After (Medium, STK-06) | **done** | `TransientRetryHandler` retries only `HttpMethod.Get`; `Retry-After` (`response.Headers.RetryAfter`, delta or HTTP-date) is a **delay floor**, else full jitter; injectable base delay kept. Tests: POST-not-retried, 429-with-Retry-After-still-retries. |
| 8 | Point-of-use config validation (Medium, ARC-07) | **done** | `PrismOptions.FredApiKey` → nullable, no `[Required]`; `SecUserAgent` keeps a default; dropped `ValidateOnStart` so `/api/health` + synthetic-only runs boot. New `Infrastructure/Errors/ConfigurationException.cs` (03 taxonomy). `FredClient` throws `ConfigurationException("Prism:FredApiKey")` and `EdgarClient` throws `ConfigurationException("Prism:SecUserAgent")` on first real use. Options test rewritten. |
| 9 | Cheap hardening | **done** | (a) both allowlists assert `Uri.UriSchemeHttps`; (b) `AsOf.EnsureNotFuture` rejects `asOf > UtcNow` (`ArgumentOutOfRangeException`) in EDGAR + FRED (+EDGAR test); (c) `TryGetDecimal` (not `GetDecimal`) in `EdgarClient.ReadFact`; (d) FRED `observation_end={asOf.UtcDateTime:yyyy-MM-dd}`. |
| 10 | `ProviderRatingRecord.ToProviderRating()` mapper (Medium, ARC-08) | **done** | Maps `RatingActionDate`→`InputAsOfDate` (stale-flag driver), `SourceRef`→`MethodologyDocId`, and all 1:1 fields. `IssuerId` intentionally has no target (it lives on the enclosing dossier). Full-field equivalence test added. |

### Files created (corrections)
| File | Description |
|---|---|
| `backend/FinancialServices.Api/Analysis/FundamentalsCalculator.cs` | Pure fundamentals engine + raw `XbrlFact`/`EdgarCompanyFacts` records + `RelevantConcepts` vocabulary (fix 3/4). |
| `backend/FinancialServices.Api/Infrastructure/Errors/ConfigurationException.cs` | `CONFIGURATION_INVALID` exception (03 taxonomy) for point-of-use fail-loud (fix 8). |
| `backend/FinancialServices.Tests/TestSupport/ThrowingHttpMessageHandler.cs` | Faults a send with a supplied exception (transport-fault tests). |
| `backend/FinancialServices.Tests/TestSupport/CapturingLogger.cs` | Records log entries so the pending-provider warning path is asserted. |
| `backend/FinancialServices.Tests/Analysis/FundamentalsCalculatorTests.cs` | 6 isolation tests for the coherent-period derivation. |

### Files changed (corrections)
`EdgarClient.cs` (I/O-only rewrite), `IEdgarClient.cs` (returns `EdgarCompanyFacts`; **removed** `EdgarFacts` + `ToFundamentalSnapshot`), `FredClient.cs`, `AsOf.cs` (+`EnsureNotFuture`), `MoodysRatingsClient.cs`, `MorningstarDbrsRatingsClient.cs`, `IProviderRatingsSource.cs` (+mapper), `PrismOptions.cs`, `ServiceCollectionExtensions.cs`, `Http/TransientRetryHandler.cs`, `appsettings.json`, and the five test files (`EdgarClientTests`, `FredClientTests`, `ProviderRatingsSourceTests`, `TransientRetryHandlerTests`, `PrismOptionsTests`) + `StubHttpMessageHandler` (+`JsonResponse`).

### Deviation (corrections)
- **`IEdgarClient` contract changed vs the pkg-04 stated interface.** It now returns raw
  `EdgarCompanyFacts` (concept facts + submissions-derived latest filing) instead of a derived
  `EdgarFacts`; the derivation moved to `FundamentalsCalculator.Derive`. This is the P2 correction
  (ARC-05/STK-01) — business logic out of the I/O layer. Downstream (pkg 05/08) now composes
  `EdgarClient` + `FundamentalsCalculator` instead of calling a mapper on the connector's result.

### Round 2 (final) — residual-Major corrections (STK-R1/R2/R4/R5)

The stack re-check flagged edge-triggered Majors on the pure engine. Applied 4 targeted fixes, all in
`Analysis/FundamentalsCalculator.cs` + `Tests/Analysis/FundamentalsCalculatorTests.cs`. **Build
0-warning (`-warnaserror`), `dotnet test` = Failed: 0, Passed: 99, Skipped: 0 (was 95; +4 tests),
~53 ms, no live network.** Per-fix status:

| # | Fix | Status | Notes |
|---|---|---|---|
| STK-R1 | Debt period coherence — current portion must share the noncurrent debt's balance-sheet date | **done** | `SelectDebt` now folds in the current portion only when `current.Filing.End == noncurrent.Filing.End` (same balance-sheet date), else noncurrent alone — the two legs come from independent `SelectInstant` calls and could otherwise straddle two reporting dates. Tests: `Derive_CurrentPortionDifferentBalanceSheetDate_ExcludesStaleCurrentPortion` (Q3 noncurrent 500 + Q2 current 50 → **500 only**) + companion `…SameBalanceSheetDate_IncludesCurrentPortion` (same End → **550**) prove the guard is a coherence check, not a blanket exclusion. |
| STK-R2 | D&A must be annual | **done** | `FindSameAccnAnnual` predicate gains `&& IsAnnual(f)`, so a 3-month D&A co-filed inside a 10-K accession at the FY end is skipped in favour of the 12-month fact. Test: `Derive_DepreciationAnnualAndQuarterSameAccn_UsesAnnualDepreciation` (3-mo D&A 5 + 12-mo D&A 20 under same accn+end → EBITDA uses **20**). |
| STK-R4 | Lock STK-03 | **done** | `Facts()` fixture's issuer `LatestFilingDate/Type` moved to a **later 8-K (2026-05-25)**, distinct from the used 10-Q (2026-05-20). New `Derive_SnapshotFilingProvenance_UsesUsedFactsFilingNotIssuerLatest` asserts `snapshot.FilingDate==2026-05-20` / `FilingType=="10-Q"` while `facts.LatestFilingDate==2026-05-25` / `"8-K"` — a regression to the issuer date now fails. |
| STK-R5 | Narrow current-portion concept | **done** | `DebtCurrentConcepts` narrowed from `{ LongTermDebtCurrent, DebtCurrent }` to `{ LongTermDebtCurrent, LongTermDebtAndCapitalLeaseObligationsCurrent }` — the broad `DebtCurrent` (short-term operating borrowings: revolver / commercial paper) is dropped so it is not over-scoped into interest-bearing TotalDebt. `RelevantConcepts` (drives EDGAR extraction) updates automatically. |

**Files changed (round 2):** `Analysis/FundamentalsCalculator.cs` (`SelectDebt` same-date guard, `FindSameAccnAnnual` annual predicate, `DebtCurrentConcepts` narrowing) and `Tests/Analysis/FundamentalsCalculatorTests.cs` (`Facts()` fixture date + 4 new tests). No new files; no other files touched.

**Deferred (round 2, NOT changed):** STK-R3 (EBITDA-equals-EBIT-when-D&A-absent basis marker) and TTM-from-4-quarters — tracked in the residual-risks table below.

## Residual risks (deferred — post-corrections)
| Item | Rationale | Target package |
|---|---|---|
| Keyed-DI provider resolution (vs `IEnumerable<IProviderRatingsSource>` + `.Provider` scan) | Sweep-fan-out concern; today's set is small and null-degrades safely. | 07 |
| Response caching / N+1 on repeated companyfacts + submissions fetches | Big payloads + SEC 10 rps; persistence concern. | 08 |
| Per-attempt request cloning for future POST bodies | Only idempotent GETs are retried today; a POST body would need per-attempt cloning. | when a POST upstream lands (07) |
| STK-R3 — EBITDA silently equals EBIT when D&A is absent (no basis marker); TTM-from-4-quarters EBITDA | Calculator uses the latest **annual** window and adds D&A when present (so EBITDA collapses to EBIT with no signal when D&A is missing). Add an `EbitdaBasis` marker (`Ebitda` vs `Ebit`) to `FundamentalSnapshot`/`FundamentalsCalculator` when the leverage model is revisited, so the narration can cite the basis; TTM-from-four-quarters is the same revisit. | 05/06 |
| Real Moody's / Morningstar DBRS HTTP (endpoint/auth/response) | Product spec TBC; clients null-degrade + warn, never invent HTTP. | 05/06 once specs land |
| Governance-doc sync (raw-facts contract; `Bloomberg`→`Moodys`) | Doc housekeeping; out of green-build scope. | doc pass |
