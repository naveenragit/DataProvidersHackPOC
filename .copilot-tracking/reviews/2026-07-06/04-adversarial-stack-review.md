# Adversarial Stack-Fit / Correctness Review — Package 04 (Data Connectors)

- **Reviewer role:** Fin Adversary Stack Critic (adversarial technology + data-parsing red team)
- **Date:** 2026-07-06
- **Targets:** `Connectors/EdgarClient.cs`, `FredClient.cs`, `AsOf.cs`, `Infrastructure/Http/TransientRetryHandler.cs`, `Infrastructure/ServiceCollectionExtensions.cs`, `Infrastructure/PrismOptions.cs`, and the pkg-04 tests.
- **Method:** Every SDK call, XBRL/JSON navigation, and as-of comparison assumed wrong until proven against real API shape (SEC EDGAR APIs) or official docs (Microsoft Learn). Anything unverifiable is marked **Unverified**.
- **Headline:** The as-of *filtering* plane (filed ≤ asOf, boundary-inclusive, `"."` skip, invariant decimal, SSRF host guards, fail-loud) is genuinely correct. The **XBRL fact-selection semantics are not**: EBITDA/coverage silently mix 3-month and 12-month durations, and `LatestFilingDate` (the money-moment ground truth) is approximated from four tags. Compiles + green in CI, **wrong against real EDGAR data.**

**Severity counts:** 🔴 Critical **1** · 🟠 High **3** · 🟡 Medium **5** · 🟢 Low **4**

---

## Verification Log (claim → source → verdict)

| # | Claim under test | Source | Verdict |
|---|---|---|---|
| V1 | companyfacts shape is `facts."us-gaap".<Concept>.units."USD"[]` with `end/val/filed/form/frame` | [SEC EDGAR APIs](https://www.sec.gov/search-filings/edgar-application-programming-interfaces) | ✅ Correct navigation. |
| V2 | Duration (flow) concepts carry both **annual (`CY####`)** and **quarterly (`CY####Q#`)** facts; instants use `CY####Q#I` | SEC frames API doc: *"CY#### for annual (365d±30), CY####Q# for quarterly (91d±30), CY####Q#I for instantaneous"* | ✅ Confirms EBITDA period-mix bug (STK-01). |
| V3 | companyfacts aggregates facts **across submissions** (same period re-filed as comparatives) | SEC: *"aggregate facts from across submissions… comparable… across time"* | ✅ Confirms restatement/comparative risk (STK-04). |
| V4 | True issuer latest-filing date lives in the **submissions** API, not companyfacts | SEC: `data.sec.gov/submissions/CIK…json` = filing history | ✅ Confirms STK-02. |
| V5 | A sent `HttpRequestMessage` "should not be modified and/or reused after being sent" | [Learn: HttpRequestMessage Remarks](https://learn.microsoft.com/dotnet/api/system.net.http.httprequestmessage) | ⚠️ Handler reuses request across retries — works for body-less GET only (STK-07). |
| V6 | `AddHttpMessageHandler(Func<DelegatingHandler>)` overload exists; `ValidateDataAnnotations().ValidateOnStart()` resolve under Web SDK | builds 0-warn (session log) | ✅ Wiring compiles. |
| V7 | JSON numbers are culture-invariant; `JsonElement.GetDecimal()` reads raw token | JSON RFC 8259 / STJ | ✅ EDGAR decimal parse is culture-safe (no bug). |

---

## Findings

### [STK-01] EBITDA & interest-coverage mix 3-month and 12-month XBRL durations — Severity: 🔴 Critical
- **Target:** `EdgarClient.cs` L119 (`SelectEbitda`), driven by `Select` L124-L162 (esp. L159) and concept list L22.
- **Claim under test:** `EBITDA = OperatingIncomeLoss + D&A` and coverage = `EBITDA / InterestExpense` reflect a coherent period.
- **Reality:** `OperatingIncomeLoss`, `DepreciationDepletionAndAmortization`, `InterestExpense` are **duration** concepts. Per SEC (V2), companyfacts carries **both** a 3-month (`CY####Q#`) and a 12-month (`CY####`) fact, frequently sharing the **same `end`**. `Select` never inspects `start`, duration length, or `frame`; it picks by max `filed`, tie-broken by max `end` — but a Q4 3-month and an FY 12-month share `end`, so the End-desc tie-break does **not** disambiguate them; array order wins. Worse, operating income and D&A are picked **independently** (two separate `Select` calls at L117-L118), so a 12-month D&A can be added to a 3-month operating income.
- **Why it matters:** Debt/EBITDA is Prism's central leverage number and the whole reconciliation narrative hangs off it. A silently mixed-duration EBITDA yields a plausible-but-wrong ratio with **no error** — violates correctness and P1 (a fabricated-in-effect figure). This breaks on the first real issuer, not an edge case.
- **Fix:** Select duration facts on a single consistent window: prefer `frame == "CY{fy}"` (annual) or reconstruct TTM from four quarters; require `start` present and `(end - start) ≈ 365d` for annual EBITDA; pick operating income **and** D&A from the *same* period (match `start`/`end`/`accn`), never independently. Instant concepts (debt, cash) are `end`-only and are fine.

### [STK-02] `LatestFilingDate` is derived from 4 tags, not the issuer's real latest filing — Severity: 🟠 High
- **Target:** `EdgarClient.cs` L72 (`dated.MaxBy(p => p.Filed)`), L79 (`LatestFilingDate:`).
- **Claim under test:** `LatestFilingDate` = issuer's latest filing date (the stale-input rule's ground truth: `provider.inputAsOfDate < issuer.latestFilingDate`).
- **Reality:** It is the max `filed` across only {debt, ebitda, interest, cash}. Any filing that doesn't tag one of these (8-K, many 6-K/press items) is invisible, so the computed date **under-approximates** the true latest filing.
- **Why it matters:** The money moment hinges on this exact date; approximating it from four concepts makes the stale-input red flag under-fire — the core divergence trigger is unreliable.
- **Fix:** Source `LatestFilingDate`/`LatestFilingType` from the **submissions** API (`data.sec.gov/submissions/CIK…json`, already in the pkg-04 spec, currently deferred), not companyfacts.

### [STK-03] Mixed-vintage snapshot stamped with the newest filing date — Severity: 🟠 High
- **Target:** `EdgarClient.cs` L72-L80; codified by `EdgarClientTests.cs` L58-L70 (A1).
- **Claim under test:** An `EdgarFacts` is an as-of-coherent snapshot.
- **Reality:** Each concept is picked independently, so Cash can come from a May 10-Q (`end` Mar-31) while Debt/EBITDA remain from a March 10-Q (`end` Dec-31), yet `LatestFilingDate` = May-20. The A1 test **asserts this as correct** (`Cash=150` from May + `LatestFilingDate=2026-05-20`, while debt/EBITDA silently stay March).
- **Why it matters:** The narrator will cite "the latest 10-Q (May 20)" for a Debt/EBITDA that actually came from March — a business-accuracy defect and P1-adjacent (implies figures came from a filing they didn't). Rubric: business accuracy.
- **Fix:** Pin all fundamentals to one coherent filing (single `accn`/period), or expose per-figure filing dates and never let the narrator imply the leverage inputs share `LatestFilingDate`. Update A1 to assert coherence.

### [STK-04] Selection by max `filed` lets a restatement of an OLDER period beat a newer period — Severity: 🟠 High
- **Target:** `EdgarClient.cs` L159 (`AsOf.LatestOnOrBefore(…, en => en.Filed, asOf)`); `AsOf.cs` L20-L41.
- **Claim under test:** "latest fact ≤ asOf" returns the most recent *data*.
- **Reality:** It returns the max **`filed`** ≤ asOf. A 10-K/A restating FY2023 (`end` Dec-31) filed *after* a Q1-2024 10-Q (`end` Mar-31) has the later `filed`, so it wins — returning the older-period value over the newer quarter. SEC confirms same-period comparatives are re-filed with later `filed` dates (V3).
- **Why it matters:** Leverage computed off a stale period even though newer data exists.
- **Fix:** Standard point-in-time XBRL selection: max `end` ≤ asOf first (most recent period), then max `filed` ≤ asOf **within that period** (latest restatement).

### [STK-05] Debt concept list too narrow (`LongTermDebtNoncurrent` only) — Severity: 🟡 Medium
- **Target:** `EdgarClient.cs` L20-L21.
- **Reality:** Many filers tag `LongTermDebt`, `LongTermDebtAndCapitalLeaseObligations`, `DebtLongtermAndShorttermCombinedAmount`, or only `Liabilities` (the spec itself lists `Liabilities`). Absent noncurrent base → debt null → DebtToEbitda null → the leverage story silently vanishes with no error.
- **Why it matters:** Brittle against real issuers; even a curated demo issuer may miss the one tag.
- **Fix:** Broaden the fallback chain (`LongTermDebt`, capital-lease-combined), log which concept matched, keep null-not-zero.

### [STK-06] Retry handler retries every HTTP method and ignores `Retry-After` — Severity: 🟡 Medium
- **Target:** `TransientRetryHandler.cs` L31-L55 (no `request.Method` check), `IsTransient` L57-L60.
- **Reality:** Any request is retried on 5xx/408/429; the class doc claims GET-politeness but nothing enforces it. It also ignores the `Retry-After` header SEC/FRED send on 429/503.
- **Why it matters:** Pkg-04 is GET-only so it's latent, but the first POST client that adds this handler gets silent duplicate side effects; ignoring `Retry-After` worsens the exact SEC throttling (~10 req/s hard-block) this handler exists to respect.
- **Fix:** Gate retry on `request.Method == HttpMethod.Get` (or an idempotency allowlist); honor `Retry-After` before applying jitter.

### [STK-07] Retry resends the same `HttpRequestMessage` instance — Severity: 🟡 Medium
- **Target:** `TransientRetryHandler.cs` L34 (`await base.SendAsync(request, …)` in a loop).
- **Reality (verified V5):** Microsoft docs: a sent request "should not be modified and/or reused after being sent." It works **today only** because the `MarkAsSent` guard is in `HttpClient` (not the `DelegatingHandler` pipeline) and the requests are body-less GETs.
- **Why it matters:** The moment a retried request carries content (POST/PUT), the consumed/disposed content stream makes the retry throw or send an empty body; it also violates the documented contract.
- **Fix:** Clone the request per attempt (method, URI, version, headers, buffered content) before re-sending — as Polly's HTTP handler does.

### [STK-08] EDGAR test fixture isn't shaped like real companyfacts (green but wouldn't parse real data) — Severity: 🟡 Medium
- **Target:** `EdgarClientTests.cs` L20-L49 (`NordStarFacts`).
- **Reality:** Fixture omits `start` (every duration concept has it), `frame`, `fy`/`fp`/`accn`, and — critically — never includes the quarterly+annual duplicate per `end` (STK-01) or same-`end`/multi-`filed` comparatives (STK-04). `form:"10-Q"` with `end:2025-12-31` is internally inconsistent and only passes because the parser is lenient.
- **Why it matters:** CI gives false confidence; the two highest-value parsing bugs are invisible.
- **Fix:** Add fixtures: (a) `OperatingIncomeLoss` with both a 3-month and a 12-month entry sharing `end` (+ `start`), asserting the annual value is chosen; (b) a restated older period filed after a newer period, asserting the newer period wins.

### [STK-09] `GetDecimal()` overflow escapes the `catch (JsonException)` — Severity: 🟢 Low
- **Target:** `EdgarClient.cs` L153.
- **Reality:** `JsonElement.GetDecimal()` throws `FormatException`/`OverflowException` (not `JsonException`) for a value outside decimal range; it would bypass the L84 catch and surface as an unhandled 500 instead of a clean `UpstreamServiceException`. Real USD figures stay within decimal, so low probability. (Decimal parsing itself is culture-invariant — no culture bug.)
- **Fix:** Use `TryGetDecimal(out …)` and skip/throw-upstream on false.

### [STK-10] FRED `observation_end` formats offset-local date, not UTC — Severity: 🟢 Low
- **Target:** `FredClient.cs` L36 (`observation_end={asOf:yyyy-MM-dd}`).
- **Reality:** For a non-UTC `DateTimeOffset` the server-side cutoff date can differ from UTC intent by a day at the boundary. Client-side `LatestOnOrBefore` (L58) largely compensates but the request can drop/keep a boundary observation inconsistently. (FRED value parse L94 is correctly invariant and skips `"."`.)
- **Fix:** Normalize to UTC (`asOf.UtcDateTime:yyyy-MM-dd`) for the query to match the invariant client-side compare.

### [STK-11] No explicit `HttpClient.Timeout`; relies on 100s default across retries — Severity: 🟢 Low
- **Target:** `ServiceCollectionExtensions.cs` L33-L45.
- **Reality:** A hung SEC/FRED socket ties a request up to 100s; that throws `TaskCanceledException`, which the handler does **not** retry (only `HttpRequestException` at L45). Bounded 3-attempt retry keeps it acceptable, but slow.
- **Fix:** Set a modest per-attempt timeout (per-try `CancellationTokenSource`) so a stall fails fast.

### [STK-12] Stub handler returns shared, once-readable responses — Severity: 🟢 Low
- **Target:** `StubHttpMessageHandler.cs` L40-L48.
- **Reality:** Returns shared `HttpResponseMessage` instances with once-readable `StringContent`, reusing the last for over-calls. It can't detect a double content-read or request-reuse fault a real handler would, so retry/content edge cases pass artificially.
- **Fix:** Build a fresh response + content per call via a factory to catch reuse bugs (would surface STK-07 for POST).

---

## Unverified (needs live confirmation)
- Exact `frame`/`start` presence per concept for the specific demo issuers — inferred from SEC frames doc (V2), not from a live companyfacts pull. Confirm against a real `CIK…json` before shipping the STK-01 fix.
- Whether SEC returns `Retry-After` on 429 vs bare 403 hard-block — behavior varies; confirm live before implementing STK-06 header handling.

## Top 3 Won't-parse-real-data risks
1. **STK-01** — EBITDA mixes quarterly/annual durations → wrong Debt/EBITDA on the first real issuer.
2. **STK-02 / STK-03** — `LatestFilingDate` under-approximated and stamped over mixed-vintage figures → the money-moment date and the numbers it narrates are inconsistent.
3. **STK-04** — max-`filed` selection returns a restated old period over a newer quarter.

## Verdict
**NO-GO on real-data correctness.** The connectors compile, pass CI, and get the as-of *filtering* right, but against live EDGAR they will emit a silently wrong leverage number (STK-01) and an approximate/inconsistent money-moment date (STK-02/03/04) — the exact things package 04 exists to get right. Acceptable as a stubbed, curated-demo scaffold; **not** "as-of correct against real data" as claimed until STK-01..04 are fixed and STK-08 fixtures prove it.
