# 06 — Security & Compliance

Financial-services baseline. Applies even in a hackathon demo — degrade deliberately, document it.

---

## Identity & auth

- `DefaultAzureCredential` for all Azure SDK clients (local `az login` → managed identity in Azure,
  no code change). Never hardcode keys/connection strings.
- The accelerator enforces Entra JWT + a deny-by-default fallback policy. **Demo relaxation:** you may
  put `[AllowAnonymous]` on the specific read endpoints you demo, but keep the Entra pattern intact and
  documented so the "path to a customer build" is credible (rubric 04). Never ship anonymous access
  over **real PII**.

## The confused-deputy rule (CopilotKit sidecar)

- Authenticate the `/copilotkit` endpoint — reject anonymous browser calls.
- Forward the **end-user identity** (bearer token) to the C# API on every action/tool call so the API
  enforces per-user, object-level authorization. Do **not** call the API as an over-privileged service
  with no user context.
- **Treat every LLM-provided argument (issuerId, asOf, provider) as hostile.** The API re-validates and
  re-authorizes them — never trust the model.

## Input validation

- Validate all request DTOs (DataAnnotations); **reject unknown fields**. `asOf` must be a real date
  not in the future beyond today; `issuerId` must resolve to a known issuer.
- Rate-limit public endpoints. CORS to **specific** origins only (`Cors:Origins`) — never `*` in prod.

## Content safety & PII

- Run **Azure AI Content Safety** on any user-supplied free text before it reaches a model.
- **Never log or store PII** (names+financials together, account numbers, tax IDs). Mask if unavoidable
  (`****1234`). Prism data is synthetic issuers + public filings, but keep the discipline.
- Never place PII/secrets into prompts or telemetry.

## Audit trail (financial requirement)

- Write an `audit_events` record for every consequential action: **scope confirmed**, **dossier
  generated**, **dossier exported**. Include event type, timestamp, actor, `issuerId`, action,
  safe metadata (ids/counts) — **no PII/financials**.
- Audit writes are best-effort-non-blocking but failures are logged.

## OWASP / dependency hygiene

- Guard against injection, broken access control, SSRF (connectors only call the fixed EDGAR/FRED/
  Treasury hosts — validate/allowlist URLs; never fetch a model-provided URL).
- Pin package versions (esp. prerelease AG-UI NuGet). Keep secrets out of source and logs.

## Compliance language

- No buy/sell/recommend/allocate/trade language anywhere (P4). Prism reconciles data; it does not
  advise. Every red flag and narration cites its evidence (auditable by design).
