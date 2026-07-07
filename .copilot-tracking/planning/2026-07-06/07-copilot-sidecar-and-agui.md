# Plan 07 — CopilotKit Node Sidecar & AG-UI Wiring

**Objective:** Stand up the thin CopilotKit Node runtime sidecar that bridges the React app to the C#
AG-UI endpoint (HttpAgent → C# `/`), streaming over HTTP+SSE. Harden it — the sidecar is a real
public ingress, not just glue.

**Depends on:** Plans 01, 04. **Primary day:** 0 (vanilla) → 2 (AG-UI wired).

> Folds ⚠ STK-03 (no `AzureOpenAIAdapter` export), ARC-06/07/08/09/11/16, SEC-01/02/04.

---

## 1. Project setup

- [ ] `copilot-runtime/package.json` — **pinned exact versions** (no `^`), add `engines.node`
      (⚠ ARC-16); deps: `@copilotkit/runtime`, `openai`, `express`, `@azure/identity`,
      OpenTelemetry SDK; commit a lockfile and use `npm ci`
- [ ] `copilot-runtime/tsconfig.json`, `copilot-runtime/.env.example` (Plan 00 §6)

## 2. AG-UI bridge (the real integration)

- [ ] Configure the CopilotKit runtime with an **`HttpAgent`** pointing at the C# AG-UI endpoint
      (`http://localhost:8000/`) so the browser talks AG-UI to the orchestrator; use
      `ExperimentalEmptyAdapter` for the AG-UI passthrough path
- [ ] Keep a direct **`OpenAIAdapter` + `AzureOpenAI`** service adapter for any non-AG-UI copilot chat
      (⚠ STK-03: there is **no** `AzureOpenAIAdapter` export — construct `AzureOpenAI` from `openai`
      and pass to `OpenAIAdapter`)
- [ ] Build the `copilotRuntimeNodeHttpEndpoint` handler **once at startup**, not per request
      (⚠ ARC-07)

## 3. Hardening (must-fix before real data)

- [ ] **Startup env validation** — validate required env and `process.exit(1)` if missing; no
      `undefined`-swallowing non-null assertions (⚠ ARC-08, mirror C# `ValidateOnStart`)
- [ ] **Managed identity over keys** — prefer `DefaultAzureCredential` (`@azure/identity`) bearer token
      provider for Azure OpenAI instead of `AZURE_OPENAI_API_KEY` (⚠ ARC-08, SEC-07)
- [ ] **AuthN + user-token forwarding (OBO)** — require the end-user Entra token on every
      `/copilotkit` request; **forward it** as `Authorization` on the outbound call to the C# API so the
      API authorizes the *user*, not the app (⚠ SEC-01/02, confused-deputy)
- [ ] **Rate limiting + CORS** — per-user/per-IP rate limit + token budget; restrict CORS to the app
      origin (⚠ ARC-06)
- [ ] **Input validation** — URL-encode/allow-list any id used to build outbound URLs (⚠ SEC-04, SSRF)
- [ ] **Health + graceful drain** — `GET /health` (liveness), `GET /ready` (checks OpenAI/API
      reachability), `SIGTERM` handler that stops accepting and drains in-flight SSE streams (⚠ ARC-07)
- [ ] **Resilience** — `AbortSignal.timeout(...)` + bounded retry on outbound `fetch` (⚠ ARC-11)
- [ ] **Tracing** — OpenTelemetry in the sidecar; propagate W3C `traceparent` on every forwarded call so
      the sidecar + API share one trace; export to the same App Insights (⚠ ARC-09)

## 4. Testability

- [ ] Extract action/forwarding handlers into importable modules (no inline closures) and add `vitest`
      coverage with a mocked API client (⚠ ARC-09 — the highest-risk seam ships untested today)

## 5. Run script

- [ ] `run-copilot-runtime.bat` (Plan 01) runs `npm ci` (if needed) + starts the sidecar on 4000
- [ ] Vite proxies `/copilotkit` → sidecar in dev (Plan 08); document that in production the sidecar is
      a first-class authenticated ingress (the dev proxy does not exist in prod)

## Acceptance criteria

- Sidecar boots only with valid env (fails fast otherwise); no `AzureOpenAIAdapter` import error
- Browser → sidecar → C# AG-UI streams the orchestrator's agent cards over SSE end-to-end
- `/copilotkit` rejects anonymous calls and forwards the user token; `/health` + `/ready` respond
- One correlated trace spans sidecar + API for a single copilot action
- `vitest` covers the forwarding handler

## Cut-lines

- Day-0 vanilla: start from the CopilotKit monorepo `examples/integrations/ms-agent-framework-dotnet`
  sample streaming locally before adding hardening
- If AG-UI bridging is unstable, fall back per Plan 04 cut-lines (single agent + tool cards, or custom
  SSE envelope). Auth/rate-limit hardening may be simplified for a localhost-only demo but must be
  documented as demo-only.
