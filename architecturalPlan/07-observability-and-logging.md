# 07 — Observability & Logging

Tracing and logs are a judging asset (turn on day 1) and the fastest way to debug the demo.

---

## OpenTelemetry

- Wire OTel via the accelerator's `AddAppTelemetry` → `UseAzureMonitor()` when
  `APPLICATIONINSIGHTS_CONNECTION_STRING` is set.
- Trace sources: `AddSource("FinancialServices.Agents")` + ASP.NET Core + HttpClient instrumentation.
- **Every agent call and every external call gets a span:**
  - Agents: `ActivitySource("FinancialServices.Agents")`, span name `"{Agent}.{Method}"`.
  - Connectors: span per EDGAR/FRED/Treasury/Search request (HttpClient instrumentation covers most;
    add explicit spans around as-of logic).
  - Deterministic engines: a span around `Decompose` / `Evaluate` for timing (no PII in attributes).
- Span attributes: safe ids only (`issuerId`, `provider`, `asOf`, `flagCount`). **Never** put
  financials or PII in attributes.

## Structured logging

- `ILogger<T>` with **logging scopes** for context — never string concatenation.

```csharp
using (logger.BeginScope(new Dictionary<string, object> { ["IssuerId"] = issuerId, ["AsOf"] = asOf }))
{
    logger.LogInformation("Reconciliation produced {FlagCount} flags, confidence {Confidence:F2}",
        dossier.Flags.Count, dossier.ConfidenceScore);
}
```

- Log **ids and counts**, never payloads with financials or provider raw data.
- Levels: `Information` for lifecycle milestones (sweep start/finish, gate approved, dossier saved),
  `Warning` for degradations (agent narration discarded, provider missing), `Error` for exceptions at
  the boundary. Log an exception **once**, where context exists — not at every re-throw.
- Default `Microsoft.AspNetCore` at `Warning` (accelerator appsettings).

## Correlation

- One trace per reconciliation run; the AG-UI/HTTP request id ties frontend cards, agent spans, and
  connector calls together. Include the run id in audit events and dossier `Id`.

## Health

- `GET /api/health` (anonymous) for liveness. Consider a readiness check that verifies Cosmos + Search
  reachability for the deployed environment (optional; keep liveness cheap).

## What good looks like at demo time

- App Insights end-to-end transaction view shows: request → orchestrator → provider agents (parallel)
  → fundamentals → decomposer → red-flag → persist, with timings. Show it if asked — it demonstrates
  visible reasoning + governance.
