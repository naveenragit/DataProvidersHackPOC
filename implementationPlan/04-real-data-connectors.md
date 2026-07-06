# 04 — Data Connectors (Moody's / Morningstar ratings · EDGAR / FRED)

**Purpose:** provide the two data planes Prism reconciles: **real provider ratings** (Moody's +
Morningstar DBRS APIs) and **real market/fundamentals context** (SEC EDGAR + FRED), both with
**as-of correctness** (no hindsight). These feed the provider + fundamentals agents (package 06) and
the stale-input red flag (package 05).

**Depends on:** 02. **Blocks:** 05, 06.

Create under `backend/FinancialServices.Api/Connectors/`. All calls use `HttpClient` via
`IHttpClientFactory`, `async` + `CancellationToken`, OpenTelemetry spans, and Polly backoff. Auth
secrets come from `PrismOptions` / Key Vault — never hardcoded. Only fixed, allowlisted hosts are
called (no model-provided URLs — SSRF guard).

> **Pending API specs:** exact Moody's/Morningstar **product, endpoints, auth, and response shape**
> are TBC. Build against the `IProviderRatingsSource` abstraction below; implement the real HTTP once
> the product is confirmed. EDGAR + FRED are fully specified and built now.

---

## Provider ratings — Moody's + Morningstar DBRS (the ratings being reconciled)

Provider verdicts come from **real APIs**. Abstract behind one interface so the decomposer
(package 05) and provider agents (package 06) are source-agnostic:

```csharp
public sealed record ProviderRatingRecord(
    Provider Provider, string IssuerId, string Letter, int Notch,
    DateTimeOffset AsOfDate, DateTimeOffset RatingActionDate,   // action date drives the stale flag
    IReadOnlyList<RatingFactor> Factors, string SourceRef);

public interface IProviderRatingsSource
{
    Provider Provider { get; }
    Task<ProviderRatingRecord?> GetRatingAsOfAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct);
    // null = no coverage → RedFlagEngine emits MISSING_COVERAGE (never fabricate)
}
```

Implementations:
- `MoodysRatingsClient : IProviderRatingsSource` — real Moody's API (product/auth pending; likely
  OAuth2 client-credentials or API key via `PrismOptions`). Map the agency scale via `NotchLadder`.
- `MorningstarDbrsRatingsClient : IProviderRatingsSource` — real Morningstar/DBRS API (pending).
- `SyntheticRatingsSource : IProviderRatingsSource` — the labeled-synthetic **MSCI** 3rd slot, backed
  by the AI Search corpus (package 03); also the offline/dev source.

Rules: capture the real **`ratingActionDate`** and any factor/driver breakdown the API returns
(needed for the decomposition + the stale flag). Enforce as-of (`ratingActionDate <= asOf`). On no
entitlement/coverage return `null` → `MISSING_COVERAGE` (P1: never fake a rating). Cache fetched
records (Cosmos/Search) with provenance for reproducible, rate-limit-friendly demo runs.

---

## A. SEC EDGAR — issuer fundamentals + filing dates (the stale-flag ground truth)

- **Company facts:** `https://data.sec.gov/api/xbrl/companyfacts/CIK{cik:0000000000}.json`
- **Submissions (filing dates):** `https://data.sec.gov/submissions/CIK{cik:0000000000}.json`
- Requires a descriptive **User-Agent** (`Prism__SecUserAgent`) or SEC returns 403. No key.

```csharp
public sealed record EdgarFacts(
    string Cik, decimal? TotalDebt, decimal? Ebitda, decimal? InterestExpense,
    decimal? Cash, DateTimeOffset LatestFilingDate, string LatestFilingType);

public interface IEdgarClient
{
    Task<EdgarFacts> GetFactsAsOfAsync(string cik, DateTimeOffset asOf, CancellationToken ct);
}
```

- Parse XBRL concepts: `Liabilities` / `LongTermDebtNoncurrent`, `EarningsBeforeInterestTaxes...` (or
  derive EBITDA), `InterestExpense`, `CashAndCashEquivalentsAtCarryingValue`.
- **As-of filter:** only use facts whose filing `end`/`filed` date `<= asOf`; capture the max filing
  date as `LatestFilingDate`. This is what the stale-input rule compares against.
- Compute `DebtToEbitda`, `InterestCoverage` → build `FundamentalSnapshot` (package 02).

## B. FRED — credit spreads + rates (market context)

- Endpoint: `https://api.stlouisfed.org/fred/series/observations?series_id=...&api_key=...&file_type=json`
- Series: `DGS10` (10Y Treasury), `BAMLC0A0CM` (IG OAS), `BAMLH0A0HYM2` (HY OAS), optional sector OAS.
- **As-of filter:** request `observation_end={asOf}` and take the latest observation ≤ as-of. Never
  fetch observations after the decision date.

```csharp
public interface IFredClient
{
    Task<decimal> GetLatestOnOrBeforeAsync(string seriesId, DateTimeOffset asOf, CancellationToken ct);
}
```

## C. Treasury FiscalData — par yield curve (optional sanity layer)

- `https://api.fiscaldata.treasury.gov/services/api/fiscal_service/...` daily par yields, no key.
- Use for an implied-spread sanity check on the synthetic bond; low priority (cut candidate).

---

## D. As-of helper + resilience

- Single `AsOf` utility enforcing "latest value with timestamp ≤ asOf" across all connectors.
- Register typed clients in `ServiceCollectionExtensions.AddDomainServices` with Polly retry +
  jitter (FRED/SEC rate-limit politely). Cache responses per (id, asOf) for the demo run.
- **Never** invent a value on failure — surface a clear error (real-Azure-only principle). If a real
  filer field is missing, mark the factor `sourceRef` as `unavailable` rather than fabricating.

---

## Acceptance for this package
- [ ] `GetFactsAsOfAsync(nordstarCik, 2026-06-25)` returns Q3 figures with the correct `LatestFilingDate`.
- [ ] The same call with `asOf = 2026-04-01` returns only Q2 (proves as-of filtering, no hindsight).
- [ ] `GetLatestOnOrBeforeAsync("BAMLC0A0CM", asOf)` returns a spread dated ≤ as-of.
- [ ] Connectors emit OTel spans; failures throw actionable errors (no silent fallback).
- [ ] xUnit with a stubbed `HttpMessageHandler` for the as-of boundary cases.
