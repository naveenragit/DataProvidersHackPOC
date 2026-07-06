# Adversarial Architecture Review: Prism Package 04 — Data Connectors

- **Reviewer role:** Fin Adversary Architect (architecture red team)
- **Date:** 2026-07-06
- **Posture:** Guilty-until-proven-innocent. Findings are attacks, not suggestions.
- **Target:** `backend/FinancialServices.Api/Connectors/*`, `Infrastructure/{ServiceCollectionExtensions,PrismOptions}.cs`, `Infrastructure/Http/TransientRetryHandler.cs`, `Program.cs`
- **Consumers under contract:** deterministic engines (pkg 05), provider/fundamentals/red-flag agents (pkg 06), orchestrator fan-out (pkg 07).
- **Grading lenses:** Azure Well-Architected — Reliability (REL), Cost (COST), Operational Excellence (OPS), Performance (PERF).

---

## Threat Model Summary

- **Topology attacked:** agent (pkg 06/07) → typed `HttpClient` + `TransientRetryHandler` → SEC EDGAR / FRED over the public internet; orchestrator → `IEnumerable<IProviderRatingsSource>` fan-out over 3 sources (1 synthetic, 2 throwing).
- **Highest-risk assumption:** *"Connectors fail loud as `UpstreamServiceException`, so pkg 05–07 can rely on a clean 502-or-null contract."* This holds only for HTTP-status and JSON-parse failures — **transport faults, TLS errors, and timeouts escape raw**, and **two of three ratings sources throw `NotImplementedException`**, so the fan-out contract is already broken at the foundation.

---

## Findings

### [ARC-01] "Fail loud as `UpstreamServiceException`" is a partial contract — transport/timeout faults escape raw — Severity: Critical
- **Target:** [EdgarClient.cs](../../../backend/FinancialServices.Api/Connectors/EdgarClient.cs#L42) L42 `http.GetAsync`; [FredClient.cs](../../../backend/FinancialServices.Api/Connectors/FredClient.cs#L38) L38; contract stated in [IEdgarClient.cs](../../../backend/FinancialServices.Api/Connectors/IEdgarClient.cs#L50) L50-53 and [03-error-handling-and-propagation.md](../../../architecturalPlan/03-error-handling-and-propagation.md).
- **Attack:** The connectors only convert `!IsSuccessStatusCode` (EdgarClient L43-47 / FredClient L40-44) and `JsonException` (EdgarClient L86-90 / FredClient L52-57) into `UpstreamServiceException`. A DNS failure, socket reset, TLS handshake error, or `HttpClient` timeout throws `HttpRequestException` / `TaskCanceledException` **out of the raw `GetAsync`**, outside every catch. `TransientRetryHandler` (L44 `catch (HttpRequestException) when (attempt < _maxAttempts)`) deliberately rethrows the final-attempt fault, so after 3 attempts the raw exception propagates unwrapped.
- **Impact (REL, OPS):** The exact transient blip retries exist to survive — if it outlasts 3 attempts — surfaces as an **un-mapped `HttpRequestException`/`TaskCanceledException`**, not the promised 502. Pkg 08/09 middleware keys HTTP-code mapping off `UpstreamServiceException`; these escape → generic 500 (risk of leaked stack). Worse: pkg 07's documented degradation path (03: "a failed non-critical agent … record a `MISSING_COVERAGE`-style note, continue") keys off `UpstreamServiceException`/null — a raw transport fault is in neither set, so **one flaky provider socket faults the entire reconciliation sweep** instead of degrading one lane.
- **Concrete failure scenario:** Demo day, SEC has a 30-second edge blip. All 3 EDGAR attempts throw `HttpRequestException`. The Fundamentals agent call bubbles a raw exception; the orchestrator's `Task.WhenAll` faults; the dossier for *every* issuer in the sweep dies, not just the one EDGAR lane.
- **Hardening:** Wrap the awaited `GetAsync`/`ReadAsStringAsync` in each connector in `catch (HttpRequestException ex)` and `catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)` → rethrow as `UpstreamServiceException(source, …, ex)`. Let *caller-driven* cancellation (`ct.IsCancellationRequested`) still propagate as `OperationCanceledException` (03 cancellation rule). Add a unit test that queues a `throw new HttpRequestException()` handler and asserts `UpstreamServiceException`.
- **Best-practice reference:** WAF Reliability — *handle transient faults*; 03 exception taxonomy (`UpstreamServiceException` = the single upstream-failure type).

### [ARC-02] No timeout budget anywhere — a hung upstream stalls the whole sweep — Severity: Critical
- **Target:** [ServiceCollectionExtensions.cs](../../../backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L32) L32-44 (no `client.Timeout`); [TransientRetryHandler.cs](../../../backend/FinancialServices.Api/Infrastructure/Http/TransientRetryHandler.cs#L31) L31 (unbounded retry loop). Grep of `backend/**/*.cs` for `Timeout|StandardResilience|AddResilience` returns only the `RequestTimeout` (408) constant — **zero timeout configuration**.
- **Attack:** No `HttpClient.Timeout` override ⇒ the .NET default **100 s** applies, and it bounds the *whole* handler pipeline (all 3 retry attempts + jitter delays collectively), not per attempt. The only finer deadline is the caller's `CancellationToken` — but the tests (and, by pattern, the pending agents) pass `CancellationToken.None` ([ProviderRatingsSourceTests.cs](../../../backend/FinancialServices.Tests/Connectors/ProviderRatingsSourceTests.cs#L24) L24+). Nothing enforces a shorter deadline.
- **Impact (REL, PERF):** A single hung SEC/FRED socket blocks a reconciliation agent for up to 100 s with no external cancel. An orchestrator fan-out (`Task.WhenAll`) is only as fast as its slowest lane, so **one stalled EDGAR call = up to 100 s of dead air on stage** while the whole dossier waits. And a 100 s timeout ultimately throws `TaskCanceledException`, which (see ARC-01) escapes unwrapped.
- **Concrete failure scenario:** FRED is slow (not down) during the live demo. `GetLatestOnOrBeforeAsync` hangs ~100 s, then throws `TaskCanceledException`; the sweep neither completes nor cleanly errors for a minute-and-a-half.
- **Hardening:** Set an explicit per-call budget (`client.Timeout = TimeSpan.FromSeconds(10)` on both typed clients) **and** have pkg 06/07 pass a `CancellationTokenSource(TimeSpan)` deadline down `ct` rather than `CancellationToken.None`. Optionally move to `AddStandardResilienceHandler()` (Microsoft.Extensions.Http.Resilience) which bundles timeout + bounded retry + jitter and would *replace* the hand-rolled handler — but for hackathon, an explicit `Timeout` is the one-line must-fix.
- **Best-practice reference:** WAF Reliability — *set timeouts on all remote calls*; 03 cancellation rule.

### [ARC-03] Pending provider clients throw `NotImplementedException`, breaking the interface's `null → MISSING_COVERAGE` contract — Severity: High
- **Target:** [MoodysRatingsClient.cs](../../../backend/FinancialServices.Api/Connectors/MoodysRatingsClient.cs#L25) L25-26 + [MorningstarDbrsRatingsClient.cs](../../../backend/FinancialServices.Api/Connectors/MorningstarDbrsRatingsClient.cs#L23) L23-24 both `throw new NotImplementedException(...)`; contract in [IProviderRatingsSource.cs](../../../backend/FinancialServices.Api/Connectors/IProviderRatingsSource.cs#L33) L33-36 ("Returns `null` on no coverage → `MISSING_COVERAGE` … never fabricate"); all three registered as one interface in [ServiceCollectionExtensions.cs](../../../backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L47) L47-49.
- **Attack:** The orchestrator (pkg 07) is told to inject `IEnumerable<IProviderRatingsSource>` and fan out. A naive `await Task.WhenAll(sources.Select(s => s.GetRatingAsOfAsync(issuer, asOf, ct)))` **faults on the first `NotImplementedException`**. `NotImplementedException` is a *bug-class* exception — it is neither `UpstreamServiceException` nor `null`, so it is in none of pkg 07's expected catch/degrade sets. Two of three registered sources violate the contract, and they are the **real anchor providers** (Moody's + Morningstar DBRS per the real-data pivot) — the stars of the "money moment" throw.
- **Impact (REL, OPS):** Every sweep hard-fails until pkg 07 defensively wraps each source, or the demo silently runs on the single synthetic MSCI lane. Either way the fan-out seam pkg 07 was promised is not usable as-is.
- **Concrete failure scenario:** Pkg 07 wires the obvious `Task.WhenAll` fan-out. First integration run: `AggregateException → NotImplementedException("Moody's API integration pending")`. Zero dossiers produced.
- **Hardening:** Decide the pending-source semantics **now** and encode it in the contract, not per-caller. Two honest options: (a) return `null` (→ `MISSING_COVERAGE`, "we don't have this provider wired yet" — truthful, keeps the sweep alive); or (b) throw a dedicated `ProviderNotConfiguredException` that pkg 07's degrade path explicitly swallows into a `MISSING_COVERAGE` note. Never `NotImplementedException` across a fan-out boundary. Document the choice on `IProviderRatingsSource`.
- **Best-practice reference:** WAF Reliability — *graceful degradation / bulkhead*; 03 "a failed non-critical agent degrades gracefully."

### [ARC-04] FRED API key travels in the URL and leaks into framework HTTP logs — Severity: High
- **Target:** [FredClient.cs](../../../backend/FinancialServices.Api/Connectors/FredClient.cs#L34) L34-36 (`&api_key={_options.FredApiKey}` in the request URI); [appsettings.json](../../../backend/FinancialServices.Api/appsettings.json#L2) L2-6 leaves `Logging:LogLevel:Default = Information` with **no override for `System.Net.Http.HttpClient`**.
- **Attack:** FRED only accepts the key as a query arg (no header option), so the secret is unavoidably in `request.RequestUri`. The `IHttpClientFactory` pipeline's `LoggingScopeHttpMessageHandler`/`LoggingHttpMessageHandler` log `"Sending HTTP request GET {Uri}"` at **Information** under category `System.Net.Http.HttpClient.IFredClient.*`. At the default level, **the full URI including `api_key=…` is written to console / App Insights**. The connector's own `UpstreamServiceException` messages correctly scrub it (L42-43), but the framework logger does not.
- **Impact (SEC/compliance, OPS):** A live secret lands in log sinks and any downstream log shipping — a 06/P6 "never log secrets" violation and a rotation event if logs are shared during the hackathon.
- **Concrete failure scenario:** A teammate opens the API console or App Insights "requests" trace to debug FRED and screenshots the `api_key` into the team chat.
- **Hardening:** Set `"System.Net.Http.HttpClient": "Warning"` (or `None`) in `appsettings.json` logging filters, **and/or** add a small redacting `DelegatingHandler` that rewrites the logged URI, **and/or** move the key into a header via a handler if FRED ever supports it. Minimum viable: raise the log filter.
- **Best-practice reference:** WAF Operational Excellence — *never emit secrets to telemetry*; 06 security / 07 "never log the payloads."

### [ARC-05] Financial derivation (EBITDA proxy, debt aggregation) is business logic inside the I/O connector — violates P2 deterministic-core separation — Severity: High
- **Target:** [EdgarClient.cs](../../../backend/FinancialServices.Api/Connectors/EdgarClient.cs#L94) `SelectDebt` L94-107 (total debt = `LongTermDebtNoncurrent` + current portion) and `SelectEbitda` L110-121 (EBITDA ≈ `OperatingIncomeLoss` + D&A). Contrast [IEdgarClient.cs](../../../backend/FinancialServices.Api/Connectors/IEdgarClient.cs#L27) L27-45 where the *ratio* math (`ToFundamentalSnapshot`) is correctly pure.
- **Attack:** The connector mixes four concerns: (1) HTTP I/O, (2) XBRL concept selection, (3) as-of filtering, and (4) **contestable accounting methodology** — "what counts as EBITDA / total debt." Per core principle P2, the deterministic, auditable math is supposed to live in `Analysis/` as plain, unit-tested C#; the connectors should be thin I/O. Here the numerator/denominator *construction* for the headline `DebtToEbitda` and `InterestCoverage` — the very numbers that drive the notch-divergence "money moment" — is buried in an HTTP class and exercised only via full-document integration tests, never as an isolated financial rule.
- **Impact (correctness, OPS):** If the EBITDA proxy or debt aggregation is wrong (e.g., D&A double-counted, operating-lease debt excluded), every downstream ratio, divergence bucket, and red flag is silently wrong, and the pkg 05 deterministic-core reviewers won't see the rule because it's in `Connectors/`, not `Analysis/`. Inconsistent seam: ratio math is pure-and-visible, but its inputs are derived invisibly.
- **Concrete failure scenario:** An issuer reports D&A both in the cash-flow statement tag and inside `OperatingIncomeLoss`'s components; the additive proxy overstates EBITDA, understates `DebtToEbitda`, and suppresses a `STALE_INPUT`/leverage red flag — the demo's headline divergence quietly disappears.
- **Hardening:** Keep `EdgarClient` returning **raw concept values** (`LongTermDebtNoncurrent`, `LongTermDebtCurrent`, `OperatingIncomeLoss`, `D&A`, `InterestExpense`, `Cash` + their `filed` dates). Move `SelectDebt`/`SelectEbitda` into a pure `Analysis/FundamentalsDerivation` function with dedicated unit tests, so the accounting choices are visible to the deterministic-core review and independently testable. The as-of selection (`AsOf.cs`) is already correctly separated — mirror that discipline for derivation.
- **Best-practice reference:** Core principle P2 (deterministic core in `Analysis/`, LLM only narrates); WAF Operational Excellence — *testable units*.

### [ARC-06] Retry handler resends one `HttpRequestMessage` — breaks the instant the pending provider clients POST a body — Severity: Medium
- **Target:** [TransientRetryHandler.cs](../../../backend/FinancialServices.Api/Infrastructure/Http/TransientRetryHandler.cs#L37) L37 (`base.SendAsync(request, …)` in a loop over one message instance).
- **Attack:** Re-sending the same `HttpRequestMessage` is tolerated by modern `SocketsHttpHandler` **only for bodyless requests**. EDGAR/FRED are GET today, so it works. But Moody's/DBRS auth + query calls (pkg 04.x) will almost certainly **POST** a body; after attempt 1 the request content stream is consumed/disposed, so retries send an empty body or throw `InvalidOperationException`. The retry meant to protect the flakiest, brand-new integrations is exactly the one that will corrupt them.
- **Impact (REL):** Silent wrong-payload retries or a hard throw on the first transient 5xx from a real provider — discovered only when the real API is wired, late.
- **Hardening:** Clone the request per attempt (copy method, URI, headers, and buffer/rebuild content) before `base.SendAsync`, or restrict the retry to idempotent methods, or adopt `AddStandardResilienceHandler` which handles request cloning. At minimum add an XML-doc warning that this handler is GET-safe only until cloning is added.
- **Best-practice reference:** WAF Reliability — *safe retries / idempotency*.

### [ARC-07] `ValidateOnStart` gates the entire API boot (health probe included) on FRED + SEC secrets, even for paths that need neither — Severity: Medium
- **Target:** [ServiceCollectionExtensions.cs](../../../backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L16) L16-19 (`ValidateDataAnnotations().ValidateOnStart()`); [PrismOptions.cs](../../../backend/FinancialServices.Api/Infrastructure/PrismOptions.cs#L13) L13-18 (`[Required]` FredApiKey + SecUserAgent); [Program.cs](../../../backend/FinancialServices.Api/Program.cs#L15) L15 (`/api/health` mapped after `Build()`).
- **Attack:** Fail-loud on missing secrets is *correct for a fake-data guard*, but it is wired as an **all-or-nothing boot gate**. A teammate who wants to demo only the labeled-synthetic MSCI source (zero external deps) or hit `/api/health` cannot even start the host without a FRED key — `ValidateOnStart` throws during `StartAsync`, so no route, including the health probe, ever serves.
- **Impact (OPS):** One missing env var = total API outage on demo day, including liveness, for code paths that don't touch FRED/SEC. Over-couples optional-path secrets to global liveness.
- **Hardening:** Keep fail-loud, but *scope* it: validate `FredApiKey` only when a FRED-dependent feature is enabled (feature flag / conditional `ValidateOnStart`), or downgrade to a startup **warning** for the synthetic-only dev profile, or split provider secrets into their own options so the synthetic + health paths boot independently. Ensure `/api/health` is reachable even when a data-source secret is absent.
- **Best-practice reference:** WAF Operational Excellence — *graceful startup / independent liveness*.

### [ARC-08] Hand-duplicated `ProviderRatingRecord` vs `ProviderRating` with no compiler-enforced mapping — Severity: Medium
- **Target:** [IProviderRatingsSource.cs](../../../backend/FinancialServices.Api/Connectors/IProviderRatingsSource.cs#L14) L14-22 (`ProviderRatingRecord`: `RatingActionDate` + `SourceRef`) vs [PrismModels.cs](../../../backend/FinancialServices.Api/Models/PrismModels.cs#L17) `ProviderRating` (`InputAsOfDate` + `MethodologyDocId`).
- **Attack:** Two 7-field near-twins that pkg 05 must reconcile by hand. A field added to one won't fail the build if the future mapper omits it — classic contract-drift seam between the connector boundary and the domain model. The comment openly defers the merge to pkg 05, so the risk is acknowledged but unmitigated.
- **Impact (correctness, OPS):** Silent field-drop during the pkg 05 merge (e.g., `SourceRef` citation lost), weakening the "every number is cited" story with no compiler signal.
- **Hardening:** Either converge on one record with optional fields, or add an explicit `ToProviderRating(...)` mapper **with a test** that round-trips every field, so an added field forces a compile/test failure. Co-locating both records is fine; the missing piece is an enforced mapping.
- **Best-practice reference:** WAF Operational Excellence — *single source of truth for contracts*.

### [ARC-09] Source selection via `IEnumerable` + `.Provider` scan instead of keyed DI — silent duplicate/absent handling — Severity: Low
- **Target:** [ServiceCollectionExtensions.cs](../../../backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L47) L47-49; [IProviderRatingsSource.cs](../../../backend/FinancialServices.Api/Connectors/IProviderRatingsSource.cs#L28) L28 (`Provider Provider { get; }`).
- **Attack:** Pkg 07 will likely select with `sources.First(s => s.Provider == p)`. A duplicate registration for the same `Provider` is silently masked by `First()`; an *absent* provider throws `InvalidOperationException` (another unmapped exception). .NET 8 keyed DI (`AddKeyedSingleton<IProviderRatingsSource>(Provider.Moodys, …)`) would make selection explicit and duplicates a startup error.
- **Impact (OPS):** Misconfiguration hides until runtime; wrong-source-wins is undetectable.
- **Hardening:** Use keyed DI keyed by `Provider`, or add a startup check that the set of registered `.Provider` values is exactly the expected three with no duplicates.
- **Best-practice reference:** WAF Operational Excellence — *fail fast on misconfiguration*.

### [ARC-10] No caching + large EDGAR `companyfacts` payload + per-agent refetch risks tripping SEC's 10 req/s — Severity: Low (deferred to pkg 08, quantified)
- **Target:** connectors have no cache (by design, deferred); [EdgarClient.cs](../../../backend/FinancialServices.Api/Connectors/EdgarClient.cs#L41) L41 fetches the **entire** issuer `companyfacts` document (routinely multi-MB) per call.
- **Attack:** Per issuer per sweep ≈ 1 EDGAR (large) + 2 FRED (`BAMLC0A0CM`, `BAMLH0A0HYM2`) + 3 provider calls. If the Fundamentals agent and the Freshness/Red-Flag agent each re-fetch `EdgarFacts`, the multi-MB doc is downloaded multiple times per issuer. A 5-issuer concurrent fan-out ⇒ ~30 upstream calls, and EDGAR enforces ~10 req/s → 403/429 → `TransientRetryHandler` **triples** the EDGAR pressure under load, amplifying the rate-limit trip.
- **Impact (PERF, COST):** Demo-time throttling and slow sweeps; wasted bandwidth on repeated multi-MB downloads.
- **Hardening (pkg 08):** Cache `EdgarFacts` per `(cik, asOf)` for the session; consider the lighter `companyconcept` endpoint for single concepts; serialize EDGAR calls under a small concurrency limiter (respect 10 req/s). Acknowledged as deferred — call it out so pkg 08 sizes it.
- **Best-practice reference:** WAF Performance Efficiency / Cost — *cache expensive idempotent reads; respect upstream rate limits*.

---

## Design Strengths (kept honest — brief)

- **As-of correctness is cleanly factored.** [AsOf.cs](../../../backend/FinancialServices.Api/Connectors/AsOf.cs#L18) is a single pure selector reused by both EDGAR and FRED — no hindsight, no duplication. This is the right seam and it is done right.
- **Labeled-synthetic is genuinely labeled, not a silent fake.** [SyntheticRatingsSource.cs](../../../backend/FinancialServices.Api/Connectors/SyntheticRatingsSource.cs#L30) prefixes every `SourceRef` with `synthetic:` and returns `null` for unknown issuers — honest offline seam, consistent with P1.
- **Input sanitation on untrusted tool args is present.** `NormalizeCik` (EdgarClient L189+) and `ValidateSeriesId` (FredClient L110+) constrain agent-supplied ids before URL building (P6/D8).
- **Testability without live network works.** [StubHttpMessageHandler.cs](../../../backend/FinancialServices.Tests/TestSupport/StubHttpMessageHandler.cs#L15) lets pkg 05/06 test EDGAR/FRED behavior (including `[500,500,200]` retry sequences) with no sockets and no Moq — the interface seams (`IEdgarClient`, `IFredClient`, `IProviderRatingsSource`) are real and injectable.
- **`ConnectorTelemetry` tags only safe ids** (cik, asOf, seriesId) — no financials/PII in spans (07-compliant).

---

## Top 3 Must-Fix (before pkg 07 builds the fan-out)

1. **ARC-03** — Stop the pending Moody's/DBRS clients from throwing `NotImplementedException` across the fan-out boundary; return `null` or a degradable `ProviderNotConfiguredException`, and document the semantics on `IProviderRatingsSource`. *(Otherwise the first pkg 07 sweep hard-faults.)*
2. **ARC-01 + ARC-02** — Give the connectors a real resilience contract: an explicit `HttpClient.Timeout` (~10 s) **and** wrap transport/timeout faults into `UpstreamServiceException`, so a transient blip becomes a clean, degradable 502 instead of an unmapped 500 or a 100 s stall. *(This is the resilience-vs-fail-loud tension resolved in the right direction.)*
3. **ARC-05** — Move the EBITDA/debt derivation out of `EdgarClient` into a pure, unit-tested `Analysis/` function, so the accounting methodology behind the headline ratios is visible to the deterministic-core review (P2). *(ARC-04 secret-log fix is a close 4th and trivial — one log-filter line.)*

---

## Verdict

**Conditionally sound** — the as-of core, labeled-synthetic seam, and injectable interfaces are the right foundation, but pkg 05–07 must not build on the connectors until the fan-out contract (ARC-03) and the resilience/fail-loud contract (ARC-01/02) are closed; today a single transient blip or the two real anchor providers will fault the whole sweep.
