# 03 — Error Handling & Propagation

One consistent error model from the deepest connector to the React toast. Fail loud, never fake.

---

## Backend exception taxonomy (`Infrastructure/Errors/`)

| Exception | When | Maps to HTTP |
|---|---|---|
| `ConfigurationException` | required option missing/invalid | fail at **startup** (ValidateOnStart) |
| `NotFoundException` | issuer/dossier not found | 404 |
| `ValidationException` | bad request payload / unknown fields | 400 |
| `UpstreamServiceException` | EDGAR/FRED/Search/Foundry failure | 502 |
| `RateLimitException` | 429 from Foundry after retries | 503 (Retry-After) |
| `DomainInvariantException` | deterministic invariant broken (e.g. attribution sum ≠ gap) | 500 (this is a bug — surface it) |

Throw specific exceptions; a single exception-handling middleware maps them to the standard shape.

## Standard error response

`UseExceptionHandler()` + `AddProblemDetails()` produce one shape everywhere:

```json
{ "error": { "code": "STALE_INPUT_LOOKUP_FAILED", "message": "…", "details": { "issuerId": "nordstar" } } }
```

- `code` = stable UPPER_SNAKE identifier (client-switchable). `message` = human text (no PII/secrets).
  `details` = safe context only (ids, counts). Never leak stack traces or connection strings.

## Rules by layer

**Connectors (04 real data):** on failure, throw `UpstreamServiceException` naming the source. **Never
fabricate a value.** If a real XBRL field is genuinely absent, return the field as `null`/`unavailable`
with a `sourceRef` of `unavailable` — that is data, not an error; do not invent numbers.

**Deterministic engines (`Analysis/`):** validate invariants and throw `DomainInvariantException` if
violated (attribution must sum to the gap; notch must be 1–21). These throws indicate a bug and must
never be swallowed. Pure functions — no try/catch-and-continue.

**Agents / LLM:**
- 429 → exponential backoff **with jitter**, bounded retries; then `RateLimitException`. Put chatty
  agents on `gpt-4.1-mini` to avoid this.
- Ungrounded or contradictory narration → **discard the narrative, keep the deterministic result**
  (narrator-honesty validation). Log a warning; do not fail the whole dossier.
- A failed non-critical agent (e.g. one provider explainer) degrades gracefully: record a
  `MISSING_COVERAGE`-style note, continue. A failed **deterministic** step aborts the run.

**Controllers:** never catch broadly to hide errors. Let middleware map exceptions. Use
`Problem(...)` only for expected control-flow (e.g. 404) as the templates do.

**Config:** missing required settings → `ValidateOnStart` throws at boot naming the setting. No app
starts in a half-configured state.

## Cancellation

Plumb `CancellationToken` through every async call. On cancellation, let `OperationCanceledException`
propagate (do not convert to 500).

## Frontend

- All server state via TanStack Query — render explicit `isLoading` / `isError` states; surface
  `error.message` in a shadcn toast/inline alert. No empty catch blocks.
- A top-level React **error boundary** wraps the router; the reconciliation page shows a retry action.
- AG-UI stream errors: show the partial dossier plus an inline error card; offer "retry sweep".
- Never render fabricated placeholder data on error — show the error state instead (mirrors P1).

## Logging on error

Log with `ILogger` scopes at the boundary that has context (ids), once — not at every re-throw. See
[07](07-observability-and-logging.md). Never log the payloads that contain financials.
