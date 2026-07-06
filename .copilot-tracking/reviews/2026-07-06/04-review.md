# Package 04 — Adversarial Review (consolidated)
*2026-07-06 · Prism automated build workflow*

Reviewers: **Prism Standards Adversary**, **Fin Adversary Security**, **Fin Adversary Stack Critic**,
**Fin Adversary Architect** (full attack surface — connectors have external HTTP, secrets, and
resilience concerns). Two correction rounds applied; a focused Stack re-review confirmed the Critical.

## Verdict
**PASS.** Build 0 warnings (warnings-as-errors), **99 tests green**, no live network in tests. The
as-of/fail-loud/no-fabrication spine is sound; the four adversaries caught real bugs that would have
broken against **live** EDGAR data — all Critical/High/Major now fixed.

## Findings & resolution

| Sev | Finding | Source | Resolution |
|---|---|---|---|
| Critical | Transport/timeout faults escaped as raw `HttpRequestException`/`TaskCanceledException` (not `UpstreamServiceException`) — breaks fail-loud + pkg-07 degrade | Standards A4, Architect ARC-01 | **Fixed** — both clients wrap transport + non-caller cancellation → `UpstreamServiceException`; tests added |
| Critical | No `HttpClient.Timeout` (default 100s) — hung upstream stalls the sweep | Architect ARC-02, Security, Stack | **Fixed** — explicit 15s timeout on both typed clients |
| Critical | EDGAR EBITDA mixed 3-mo/12-mo durations; OI & D&A picked independently → silently wrong Debt/EBITDA; derivation lived in the I/O connector (P2) | Stack STK-01, Architect | **Fixed** — pure `Analysis/FundamentalsCalculator` (P2); annual-coherent, same-accn OI+D&A; regression tests |
| High | `LatestFilingDate` from 4 concept tags, not the submissions API → money-moment date wrong | Stack STK-02/03 | **Fixed** — from `submissions` API across all forms; fundamentals `FilingDate` distinct from issuer `LatestFilingDate` |
| High | Restated old period could beat a newer quarter (max-`filed` only) | Stack STK-04 | **Fixed** — max `end` ≤ asOf, then max `filed` within period; test |
| High | FRED `api_key` in URL leaks into framework HTTP logs | Security, Architect, Standards P6 | **Fixed** — `System.Net.Http.HttpClient: Warning`; custom span omits URL/key |
| High | Moody's/DBRS stubs threw → `Task.WhenAll` fan-out over the real anchors would fault the whole sweep | Architect ARC-03 | **Fixed** — return `null` → MISSING_COVERAGE + warning; no invented HTTP |
| Medium | Retry ignored `Retry-After` / retried non-GET (SEC IP-ban risk) | Security, Stack, Architect | **Fixed** — GET-only + `Retry-After` floor |
| Medium | `ValidateOnStart` gated `/api/health` on the FRED key (boot cliff) | Architect | **Fixed** — point-of-use `ConfigurationException`; health/synthetic boot without key |
| Medium | `ProviderRatingRecord`↔`ProviderRating` no mapper (silent field drop in pkg 05) | Architect, Stack | **Fixed** — `ToProviderRating()` + full-field test |
| Medium | STK-R1 debt noncurrent+current summed across different balance-sheet dates | Stack re-review | **Fixed** — same-`End` guard + tests |
| Medium | STK-R2 D&A matched accn+end but not `IsAnnual` | Stack re-review | **Fixed** — `IsAnnual` guard + test |
| Low | scheme not checked (http), `asOf` unbounded (future defeats no-hindsight), `GetDecimal` overflow, FRED `observation_end` offset | Security, Stack | **Fixed** — https assert, `EnsureNotFuture`, `TryGetDecimal`, UTC date |

## Residual risks (deferred — tracked)

| Item | Target |
|---|---|
| STK-R3: EBITDA silently = EBIT when D&A absent — add an `EbitdaBasis` marker | pkg 05/06 |
| TTM-from-4-quarters EBITDA reconstruction (only annual/latest used now) | refinement |
| Real Moody's / Morningstar DBRS HTTP (product/endpoint/auth spec) | when API specs confirmed |
| Keyed-DI provider resolution | pkg 07 |
| Response caching / N+1 (companyfacts re-fetch per issuer) | pkg 08 |
| NR/WR/D rating sentinels (from pkg 02 deferral) | with real-provider clients |

## Gate
`dotnet build -warnaserror` ✅ 0 warnings · `dotnet test` ✅ 99 passing · no live network.
