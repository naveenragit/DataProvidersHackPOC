# Adversarial Security Review: Prism Package 04 — External Data Connectors

- **Date:** 2026-07-06
- **Reviewer role:** Red-team security & compliance (Fin Adversary Security)
- **Scope (this file):** package 04 connectors only — companion to the whole-kit `adversarial-security-review.md` in this folder.

Target: `backend/FinancialServices.Api/Connectors/*` + `Infrastructure/{PrismOptions,ServiceCollectionExtensions,Http/TransientRetryHandler,Telemetry/ConnectorTelemetry,Errors/UpstreamServiceException}` + `Program.cs` + tests.
Stack: net9.0, typed `HttpClient` via `AddHttpClient`, secrets in `PrismOptions` (`FredApiKey`, `SecUserAgent`).
Data in scope today: **public** EDGAR/FRED + **labeled-synthetic** MSCI. Moody's/Morningstar are `NotImplemented` stubs.

## Trust Boundary Map

| Boundary | What protects it today | Gap |
|---|---|---|
| Agent/LLM tool args → connector (`cik`, `seriesId`, `issuerId`, `asOf`) | `NormalizeCik` (digits, ≤10), `ValidateSeriesId` (`[A-Za-z0-9_]`), `issuerId` non-empty | **`asOf` fully trusted** — temporal/no-hindsight (P3) control is caller-defined (SEC-05) |
| Connector → EDGAR `data.sec.gov` | Relative URI + `EnsureAllowedHost` host allowlist | Scheme not asserted https in the guard (SEC-04) |
| Connector → FRED `api.stlouisfed.org` | Host allowlist + validated `seriesId` | **`api_key` in URL query** → leaks to default HTTP `ILogger` + future OTel `url.full` (SEC-01) |
| Connector → upstream (both) | `TransientRetryHandler` bounded retry | **No `Retry-After`** honoring (SEC-03); **no `HttpClient.Timeout`** override → 100s hang (SEC-02) |
| Secret at rest (`FredApiKey`) | `[Required]` + `ValidateOnStart`; not committed | Value travels in cleartext URL once used (SEC-01) |
| HTTP endpoint authN/authZ | — | Out of scope for pkg 04 (no controllers yet); must land in pkg 08 before licensed data |

## Findings

### [SEC-01] FRED `api_key` embedded in request URL → credential leak to logs & telemetry — Severity: High
- **Boundary / Target:** [backend/FinancialServices.Api/Connectors/FredClient.cs](backend/FinancialServices.Api/Connectors/FredClient.cs#L35) (`&api_key={_options.FredApiKey}`), used at [FredClient.cs L38](backend/FinancialServices.Api/Connectors/FredClient.cs#L38) `http.GetAsync(relativeUri, ...)`.
- **OWASP / Regulation:** A09 Security Logging Failures + A02 (secret exposure). Violates [architecturalPlan/06 L36](architecturalPlan/06-security-and-compliance.md#L36) "Never place PII/secrets into … telemetry" and [L49](architecturalPlan/06-security-and-compliance.md#L49) "Keep secrets out of source and logs."
- **Exploit:** The api_key is placed in the query string. `AddHttpClient` ([ServiceCollectionExtensions.cs L42](backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L42)) registers the default `Microsoft.Extensions.Http` logging handlers, which log the **full absolute request URI (query included)** at Information under category `System.Net.Http.HttpClient.IFredClient.*`. The base package does **not** redact query strings. [appsettings.json L4](backend/FinancialServices.Api/appsettings.json#L4) sets `Default: Information`, so every FRED call writes `…?series_id=…&api_key=<SECRET>&…` to the console logger — and to Azure Monitor/App Insights once pkg 07 wires an exporter. Additionally the pkg-07 `AddHttpClientInstrumentation()` OTel path captures `url.full`/`http.url` with the same key. An attacker with read access to logs/traces (insider, leaked App Insights, shared demo console) harvests the key.
- **Impact:** FRED key disclosure. Blast radius is limited **today** (free, read-only, public macro data, trivially rotated) — which is why this is High, not Critical — but it is a live, default-on secret-in-logs leak and the exact pattern that becomes Critical the moment a paid/scoped key (Moody's/Morningstar) is wired the same way.
- **Fix:** Keep the key out of every log/trace surface: (1) suppress/redact HTTP logging for the FRED named client — filter `System.Net.Http.HttpClient.IFredClient.LogicalHandler`/`.ClientHandler` to `Warning`, or register a redacting `IHttpClientLogger`; (2) when pkg 07 adds OTel HTTP instrumentation, enable URL **query redaction** (drop/redact `api_key`); (3) the custom connector span already omits the URL/key ([FredClient.cs L29-30](backend/FinancialServices.Api/Connectors/FredClient.cs#L29), [L65](backend/FinancialServices.Api/Connectors/FredClient.cs#L65)) — keep it that way. FRED only accepts key-as-query, so the control is **redaction**, not a header move.

### [SEC-02] No `HttpClient.Timeout` override → slow-upstream request hang / availability DoS — Severity: Medium
- **Boundary / Target:** [ServiceCollectionExtensions.cs L32-39](backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L32) (EDGAR) and [L42-44](backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L42) (FRED) — neither sets `client.Timeout`.
- **OWASP / Regulation:** A05 Security Misconfiguration (availability).
- **Exploit:** Default `HttpClient.Timeout` is 100s. A slow/hostile upstream (or a slow-response/slowloris body) pins each analysis request for up to ~100s. Under concurrent agent-driven analyses this exhausts server capacity — a cheap DoS with no code exec required.
- **Impact:** Thread/connection exhaustion, stalled analyses, denial of service for the whole API.
- **Fix:** Set an explicit, tight timeout per typed client (e.g., `client.Timeout = TimeSpan.FromSeconds(15)`), or add a per-attempt timeout `DelegatingHandler` inside the retry chain so no single attempt exceeds a few seconds.

### [SEC-03] `TransientRetryHandler` ignores `Retry-After` on 429/503 → rate-limit amplification & upstream ban — Severity: Medium
- **Boundary / Target:** [TransientRetryHandler.cs L55-58](backend/FinancialServices.Api/Infrastructure/Http/TransientRetryHandler.cs#L55) (`IsTransient` includes 429/503/5xx) and [L51](backend/FinancialServices.Api/Infrastructure/Http/TransientRetryHandler.cs#L51)/[L61](backend/FinancialServices.Api/Infrastructure/Http/TransientRetryHandler.cs#L61) (delay = jitter only; `response.Headers.RetryAfter` never read).
- **OWASP / Regulation:** A05 (availability/misconfiguration); SEC EDGAR fair-access ToS (10 req/s; abusive clients are blocked). The plan itself expects honoring it — [architecturalPlan/03 L15](architecturalPlan/03-error-handling-and-propagation.md#L15) maps rate-limit → "503 (Retry-After)".
- **Exploit:** On a `429 Retry-After: N` (or `503`), the handler retries after a few hundred ms of jittered backoff instead of `N` seconds, hammering an already-throttling upstream. SEC can IP-ban the caller; FRED throttles harder — taking down the connector for all issuers/the demo.
- **Impact:** Self-inflicted upstream ban / cascading outage; amplifies load on a service asking us to slow down.
- **Fix:** On 429/503, honor `response.Headers.RetryAfter` (delta or `Date`) as a **floor** for the next delay (cap to a max), keeping jitter above that floor. Optionally do not retry when `Retry-After` exceeds the remaining request budget.

### [SEC-04] Host allowlist does not assert HTTPS scheme (TLS-downgrade not blocked by the guard) — Severity: Low
- **Boundary / Target:** [FredClient.cs L115-121](backend/FinancialServices.Api/Connectors/FredClient.cs#L115) and [EdgarClient.cs L203-208](backend/FinancialServices.Api/Connectors/EdgarClient.cs#L203) — `EnsureAllowedHost` checks `BaseAddress.Host` only, not `Scheme`.
- **OWASP / Regulation:** A02 Cryptographic Failures / A05.
- **Exploit:** DI hardcodes `https://` ([ServiceCollectionExtensions.cs L35](backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L35), [L43](backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L43)), so this is not exploitable today — but the SSRF/allowlist guard would happily pass `http://data.sec.gov/`, sending the SEC `User-Agent` and (for FRED) the `api_key` in cleartext if BaseAddress were ever misconfigured or overridden via config.
- **Impact:** Latent cleartext transmission of the FRED key / request metadata on misconfig.
- **Fix:** Add `&& string.Equals(http.BaseAddress?.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)` to both guards; throw otherwise.

### [SEC-05] Caller-supplied `asOf` is fully trusted → no-hindsight (P3) temporal-integrity bypass — Severity: Low
- **Boundary / Target:** [EdgarClient.cs L30](backend/FinancialServices.Api/Connectors/EdgarClient.cs#L30) `GetFactsAsOfAsync(cik, asOf, …)` and [FredClient.cs L23](backend/FinancialServices.Api/Connectors/FredClient.cs#L23) `GetLatestOnOrBeforeAsync(seriesId, asOf, …)` — `asOf` is used unbounded (formatted into the FRED URL and the as-of filter) with no validation.
- **OWASP / Regulation:** A04 Insecure Design; domain temporal-integrity (MiFID II record integrity, P3 "no hindsight").
- **Exploit:** Per [architecturalPlan/04 L82](architecturalPlan/04-agent-architecture.md#L82) tool args from the model are hostile. `cik`/`seriesId`/`issuerId` are re-validated, but a prompt-injected tool call can pass a manipulated `asOf` (far future) to defeat the as-of guarantee that underpins Prism's divergence math, or an unreachable past to force `UpstreamServiceException` for a targeted issuer (nuisance/selective DoS).
- **Impact:** Temporal-integrity control bypass (undermines Prism's core reconciliation claim); targeted issuer errors.
- **Fix:** Validate `asOf` at this boundary — reject values after the analysis decision date / `DateTimeOffset.UtcNow` and absurdly old values — with a validation error rather than an upstream round-trip.

## Defenses That Hold (do not regress)

- **EDGAR SSRF/path-injection: blocked.** `NormalizeCik` strips to digits, bounds length ≤10, zero-pads ([EdgarClient.cs L191-200](backend/FinancialServices.Api/Connectors/EdgarClient.cs#L191)); the request is a relative URI ([L40](backend/FinancialServices.Api/Connectors/EdgarClient.cs#L40)) resolved against an allowlisted host ([L203](backend/FinancialServices.Api/Connectors/EdgarClient.cs#L203)). No caller-controlled host/path.
- **FRED URL-injection: blocked.** `ValidateSeriesId` allows only `[A-Za-z0-9_]` ([FredClient.cs L106-113](backend/FinancialServices.Api/Connectors/FredClient.cs#L106)) before URL-building, so `&`/`?`/`/`/space cannot be injected; the constrained charset makes encoding unnecessary.
- **TLS:** both clients are HTTPS via DI; no `ServerCertificateCustomValidationCallback` / cert-validation weakening anywhere.
- **Retry safety:** generic 4xx are not retried (verified by [TransientRetryHandlerTests.cs L34](backend/FinancialServices.Tests/Infrastructure/TransientRetryHandlerTests.cs#L34)); `OperationCanceledException` is intentionally not caught ([TransientRetryHandler.cs L44](backend/FinancialServices.Api/Infrastructure/Http/TransientRetryHandler.cs#L44)) → cancellation propagates, no infinite loop; all calls are idempotent GETs.
- **Log/telemetry hygiene:** connector spans tag only safe ids and **never** financial values or the api_key ([EdgarClient.cs L35-36](backend/FinancialServices.Api/Connectors/EdgarClient.cs#L35), [L82-83](backend/FinancialServices.Api/Connectors/EdgarClient.cs#L82); [FredClient.cs L29-30](backend/FinancialServices.Api/Connectors/FredClient.cs#L29), [L65](backend/FinancialServices.Api/Connectors/FredClient.cs#L65)); `LogDebug` logs only ids ([EdgarClient.cs L38](backend/FinancialServices.Api/Connectors/EdgarClient.cs#L38), [FredClient.cs L32](backend/FinancialServices.Api/Connectors/FredClient.cs#L32)).
- **Fail-loud without info leak:** `UpstreamServiceException` messages carry only service + HTTP status + public identifier, never the URL or key ([FredClient.cs L41-43](backend/FinancialServices.Api/Connectors/FredClient.cs#L41), [EdgarClient.cs L44-46](backend/FinancialServices.Api/Connectors/EdgarClient.cs#L44)).
- **No committed secret:** [appsettings.json](backend/FinancialServices.Api/appsettings.json#L1) has no `Prism` section; `FredApiKey`/`SecUserAgent` are `[Required]` + `ValidateOnStart` ([PrismOptions.cs L13-20](backend/FinancialServices.Api/Infrastructure/PrismOptions.cs#L13), [ServiceCollectionExtensions.cs L17-19](backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L17)) → boot fails loud, no fake fallback.
- **No fabrication:** Moody's/Morningstar throw `NotImplemented` and log only a bool `configured` flag — no secret leak ([MoodysRatingsClient.cs L20-27](backend/FinancialServices.Api/Connectors/MoodysRatingsClient.cs#L20), [MorningstarDbrsRatingsClient.cs L20-27](backend/FinancialServices.Api/Connectors/MorningstarDbrsRatingsClient.cs#L20)); synthetic source is `synthetic:`-prefixed and labeled.

## Assumptions That Would Change the Verdict

- **SEC-01 severity assumes the FRED key is free/read-only/public-data.** If any connector reuses this URL-key pattern for a **paid or entitlement-scoped** provider key (Moody's/Morningstar), SEC-01 becomes **Critical**.
- **AuthN/authZ is out of scope for pkg 04** (no controllers; [Program.cs](backend/FinancialServices.Api/Program.cs#L1) is health + root only). If pkg 08 does **not** add per-endpoint auth and **object-level** authorization/entitlement before the ratings sources go live on licensed data, that is a new Critical (confused-deputy: any caller queries any issuer's licensed rating).
- If pkg 07 wires OTel/App Insights **before** SEC-01 is fixed, the api_key leak moves from local console to a shared, retained telemetry store — raise operational urgency to High/Critical.

## Top 3 Must-Fix Before Any Real Data

1. **SEC-01** — Redact the FRED `api_key` from HTTP request logging and OTel `url.full` (filter the `IFredClient` HTTP log category + enable query redaction). Do this before any centralized/retained logging is enabled.
2. **SEC-02 + SEC-03** — Add an explicit `HttpClient.Timeout` and honor `Retry-After` on 429/503, so a slow or throttling upstream cannot hang requests or get Prism banned.
3. **Object-level authorization (pkg 08 gate)** — Before Moody's/Morningstar licensed data is wired, require per-user entitlement checks at the endpoint boundary and re-validate `asOf` (SEC-05) as a hostile tool arg.

---

**GO / NO-GO:** **GO** for continued dev + demo on synthetic/public data; **NO-GO for production or any licensed-provider/PII data** until SEC-01–SEC-03 and pkg-08 object-level authZ are in place.
