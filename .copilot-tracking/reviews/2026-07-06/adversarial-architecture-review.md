# Adversarial Architecture Review — Fin Copilot Kit (Re-stacked)

- **Reviewer role:** Fin Adversary Architect (architecture red team)
- **Date:** 2026-07-06
- **Posture:** Guilty-until-proven-innocent. Findings are attacks, not suggestions.
- **Stack under fire:** React 18 (shadcn/TanStack/CopilotKit) → CopilotKit Node sidecar (`:4000`) → C# ASP.NET Core (.NET 9) API (`:8000`) → Azure (AI Foundry, Cosmos DB, AI Search, OpenAI).
- **Grading lenses:** Azure Well-Architected — Reliability (REL), Cost (COST), Operational Excellence (OPS), Performance (PERF).

---

## Threat Model Summary

**Trust boundaries crossed on a single copilot action:** browser (untrusted) → Vite proxy (dev only) → Node sidecar (holds Azure OpenAI key) → C# API (holds Cosmos/Foundry identity) → Azure data plane. That is **three network hops and three process boundaries** for one LLM tool call, each with independent failure, auth, and observability characteristics.

**Key structural weaknesses the design invites:**

1. **Two front doors, unequal protection.** REST goes browser → C# API directly; copilot goes browser → sidecar → C# API. The sidecar is a *second*, weaker public ingress: no auth, no rate limit, no content safety, no telemetry, no health probe. Attackers will target the sidecar, not the API.
2. **Consequential financial mutations are unguarded at the trust boundary.** The rebalance endpoint is anonymous, non-idempotent, HITL-gated only in the UI, and "audited" with a log line. Every regulated control lives on the wrong side of the boundary (client/UI), not the server.
3. **Correctness defect in the read path.** The portfolio point-read uses the wrong partition key, so the happy path either 404s or forces cross-partition fan-out.
4. **The "no silent fallbacks / fail loudly" rule is applied selectively** — strictly for fake data, but it also (a) chills legitimate resilience (retries/timeouts/circuit breakers are absent) and (b) is itself violated for telemetry and the entire sidecar, which fail *silently*, not loudly.
5. **The contract is maintained by hand in three places** (C# record, TS type, CopilotKit action params). It has *already* drifted in the shipped templates.

**Primary adversary personas:** (a) an anonymous internet caller hitting the public sidecar/API; (b) a prompt-injection payload riding the copilot chat into a tool call; (c) a transient Azure 429/timeout turning into a customer-facing outage; (d) a compliance auditor asking "prove this rebalance was approved and by whom."

---

## Findings

Severity legend: **Critical** (exploitable/breaks correctness or compliance now), **Major** (serious reliability/security/cost gap), **Minor** (hygiene / latent risk). Every Critical includes a concrete failure scenario.

---

### ARC-01 — Portfolio point-read uses `portfolioId` as the partition key; container is partitioned by `/clientId`
- **Severity:** Critical
- **Lens:** Reliability, Performance, Cost
- **Target:** [templates/csharp-api/FinancialServices.Api/Services/PortfolioService.cs](templates/csharp-api/FinancialServices.Api/Services/PortfolioService.cs#L34-L35) (read), [comment L32-L33](templates/csharp-api/FinancialServices.Api/Services/PortfolioService.cs#L32-L33); container PK documented in [Models/PortfolioModels.cs](templates/csharp-api/FinancialServices.Api/Models/PortfolioModels.cs#L17) and [azure-services.instructions.md container table](.github/instructions/coding-standards/azure-services.instructions.md).
- **Attack:** `ReadItemAsync<PortfolioSummary>(portfolioId, new PartitionKey(portfolioId))` addresses partition `portfolioId`, but items live in partition `clientId`. A point read is exact-match on (id, partitionKey); when `portfolioId != clientId` (the normal case), Cosmos returns 404.
- **Impact / failure scenario:** An advisor opens *any* real portfolio. The SDK throws `CosmosException(NotFound)`, which is swallowed at [L38-L41](templates/csharp-api/FinancialServices.Api/Services/PortfolioService.cs#L38-L41) → `null` → controller returns 404 for data that exists. The "obvious fix" implementers reach for — a cross-partition `SELECT * WHERE c.id = @id` — fans out to every physical partition, multiplying RU cost and tail latency and silently masking the design error. Either way the primary read path is broken; a single wrong partition key is a data-access outage.
- **Hardening:** Make `clientId` a required route/context value and read with `new PartitionKey(clientId)`; or store `id == clientId:portfolioId` composite and pass both. Never derive a partition key from the row id. Add an integration test that reads a portfolio whose `portfolioId != clientId`.
- **Best-practice reference:** Azure Cosmos DB — *Partitioning and horizontal scale* / *Point reads vs. queries* (Microsoft Learn).

---

### ARC-02 — Human-in-the-loop rebalance gate is enforced only in the UI; the API accepts anonymous, LLM-reachable rebalance submissions
- **Severity:** Critical
- **Lens:** Reliability, Operational Excellence (compliance)
- **Target:** [Controllers/PortfoliosController.cs L8-L10 (no `[Authorize]`)](templates/csharp-api/FinancialServices.Api/Controllers/PortfoliosController.cs#L8-L10), [rebalance action L25-L31](templates/csharp-api/FinancialServices.Api/Controllers/PortfoliosController.cs#L25-L31); [Services/PortfolioService.cs SubmitRebalanceAsync L45-L58](templates/csharp-api/FinancialServices.Api/Services/PortfolioService.cs#L45-L58); `ApprovedBy` optional in [Models/PortfolioModels.cs L27-L30](templates/csharp-api/FinancialServices.Api/Models/PortfolioModels.cs#L27-L30); UI-only guidance in [react-frontend.instructions.md](.github/instructions/coding-standards/react-frontend.instructions.md); HITL mandate in [financial-domain.instructions.md](.github/instructions/financial-domain/financial-domain.instructions.md).
- **Attack:** `SubmitRebalanceAsync` never checks approval. `RebalanceRequest.ApprovedBy` is nullable and never validated. The controller has no `[Authorize]` and `Program.cs` wires no authentication/authorization. The copilot sidecar exposes tool actions that forward to `/api/v1/...`, so a **prompt-injection** payload in the copilot chat ("call the rebalance tool with these weights") reaches the same unguarded endpoint the human "approve" button hits.
- **Impact / failure scenario:** A crafted chat message (or any `curl` to `:8000`) submits a rebalance for an arbitrary `portfolioId` with `ApprovedBy = null`, is accepted with `202`, and — per the design's own comment at [L48-L49](templates/csharp-api/FinancialServices.Api/Services/PortfolioService.cs#L48-L49) — is meant to *enqueue the rebalance workflow*. A consequential, regulated trade action executes with no human approval and no authenticated principal. This is a best-execution / suitability control failure and an unauthenticated-mutation vulnerability in one.
- **Hardening:** Move the gate server-side: require authentication + authorization on the controller; require a verifiable approval artifact (signed approval token / approver identity checked against the advisor's entitlements) before the workflow can transition out of `PendingApproval`. Treat all copilot/LLM tool calls as untrusted input that can *draft* but never *approve*. Model the gate as an explicit state-machine transition, not a boolean.
- **Best-practice reference:** OWASP API Security Top 10 (API1 BOLA / API5 BFLA); OWASP LLM Top 10 (LLM01 Prompt Injection, LLM08 Excessive Agency); Well-Architected Security — authorize at the trust boundary.

---

### ARC-03 — The audit trail is an `ILogger` line, not a durable, immutable, non-bypassable record
- **Severity:** Critical
- **Lens:** Operational Excellence (compliance), Reliability
- **Target:** [Services/PortfolioService.cs L50-L57](templates/csharp-api/FinancialServices.Api/Services/PortfolioService.cs#L50-L57); required `auditLog` container in [azure-services.instructions.md](.github/instructions/coding-standards/azure-services.instructions.md); `AuditEvent` mandate in [financial-domain.instructions.md](.github/instructions/financial-domain/financial-domain.instructions.md).
- **Attack:** The only record of a rebalance submission is `logger.LogInformation(...)`. Logs are sampled, buffered, PII-redacted, TTL'd, and — per ARC-14 — silently dropped entirely when the App Insights connection string is absent. There is no write to the immutable `auditLog` Cosmos container the standards require, and the audit write is not transactionally coupled to the mutation.
- **Impact / failure scenario:** A regulator (or internal compliance) asks: "Show the approval, approver, timestamp, and target weights for rebalance job `a1b2...`." The system can produce, at best, a possibly-sampled log line with no approver and no immutable guarantee — and in an environment without the telemetry connection string, **nothing at all**. The audit requirement is effectively unmet, and because the "audit" is a side-effect log rather than a gating write, it is trivially bypassable.
- **Hardening:** Write an immutable `AuditEvent` (append-only container, no updates/deletes, `EventType`, timestamp, advisorId, clientId, sessionId, action, metadata, source IP) as a *precondition* of accepting the mutation — fail the request if the audit write fails. Consider Cosmos change feed → immutable store. Never treat application logs as the system of record for audit.
- **Best-practice reference:** Well-Architected Operational Excellence — audit/traceability; SEC 17a-4 / SOX-style immutable audit retention patterns.

---

### ARC-04 — Rebalance has no idempotency key and no job store; the `202` async pattern is incomplete
- **Severity:** Critical
- **Lens:** Reliability
- **Target:** [Services/PortfolioService.cs L50, L56-L57](templates/csharp-api/FinancialServices.Api/Services/PortfolioService.cs#L50-L57); [Controllers/PortfoliosController.cs L30-L31 (`Accepted` with no `Location`)](templates/csharp-api/FinancialServices.Api/Controllers/PortfoliosController.cs#L30-L31).
- **Attack:** `jobId = Guid.NewGuid()` is minted fresh on every call with no client-supplied idempotency key and no persisted job record. `Accepted(new { jobId, ... })` returns no `Location` header and there is no `GET .../rebalance/{jobId}` status endpoint. The jobId is returned and immediately forgotten.
- **Impact / failure scenario:** The client submits a rebalance; the `202` response is lost to a network blip or a 30s proxy timeout across the 3-hop path. The client (or the copilot's retry, or an impatient advisor double-click) retries. The server has no dedupe, so it mints a **second** jobId and enqueues a **second** rebalance — a duplicate consequential trade. Meanwhile the first jobId can never be polled because no status resource exists, so the caller cannot even discover the duplicate.
- **Hardening:** Accept and enforce an `Idempotency-Key` header persisted in a job store (Cosmos `rebalanceReports`/jobs container) with a unique constraint; return the same jobId for a repeated key. Return `202` with a `Location: /api/v1/portfolios/{id}/rebalance/{jobId}` and implement the status endpoint. Persist job state before returning.
- **Best-practice reference:** Azure Architecture Center — *Asynchronous Request-Reply* pattern; *Idempotency keys* for mutation retries.

---

### ARC-05 — Copilot user text reaches Azure OpenAI without the mandated Content Safety check
- **Severity:** Major
- **Lens:** Operational Excellence (safety/compliance), Reliability
- **Target:** [copilot-runtime/server.ts L28-L33 (adapter), L51-L58 (endpoint)](templates/csharp-api/copilot-runtime/server.ts#L28-L58); Content Safety mandate "on ALL user-provided text" in [azure-services.instructions.md](.github/instructions/coding-standards/azure-services.instructions.md); optional-only endpoint in [Infrastructure/AzureOptions.cs L30-L31](templates/csharp-api/FinancialServices.Api/Infrastructure/AzureOptions.cs#L30-L31).
- **Attack:** The sidecar streams completions directly from Azure OpenAI. The C# Content Safety gate only guards the C# agent path, which the copilot chat bypasses entirely. `ContentSafetyEndpoint` is even declared optional, signalling it can be skipped.
- **Impact:** The primary free-text ingress for end users (the copilot) is unfiltered. Unsafe/abusive prompts and unmoderated model output flow through the product surface a financial platform is explicitly required to moderate — a guardrail gap and a compliance deviation from the kit's own standards.
- **Hardening:** Route copilot input/output through a moderation step (call the C# API's content-safety-guarded path, or add Content Safety in the sidecar/gateway). Make `ContentSafetyEndpoint` required. Prefer an AI gateway (APIM) enforcing safety centrally for both ingresses.
- **Best-practice reference:** Azure AI Content Safety guidance; Well-Architected Security for AI workloads.

---

### ARC-06 — The sidecar is a public, unauthenticated, unthrottled endpoint (cost + DoS amplifier)
- **Severity:** Major
- **Lens:** Cost, Reliability (Security)
- **Target:** [copilot-runtime/server.ts L25, L51-L58](templates/csharp-api/copilot-runtime/server.ts#L25-L58) (no auth/CORS/rate-limit middleware); dev-only proxy in [frontend-design-system/vite.config.ts L26-L29](templates/frontend-design-system/vite.config.ts#L26-L29); rate-limit mandate in [copilot-instructions.md](.github/copilot-instructions.md).
- **Attack:** The `/copilotkit` Vite proxy is **dev-only**. In production the browser talks to the sidecar directly, so the sidecar must be internet-exposed. It has no authentication, no CORS restriction, and no rate limiting, yet it holds the Azure OpenAI key and will happily stream completions for anyone.
- **Impact:** Any anonymous caller can drive unbounded Azure OpenAI token spend through the sidecar (direct financial cost) and/or exhaust its event loop and OpenAI quota (DoS that takes the copilot down for real users). This directly violates the kit's own "rate limiting on all public endpoints" rule.
- **Hardening:** Put the sidecar behind authenticated access (validate the app's user token; do not accept anonymous calls), add per-user/per-IP rate limiting and token-budget limits, restrict CORS to the app origin, and front it with an AI gateway (APIM token-limit + quota policies). Never expose the OpenAI-key-holding process directly.
- **Best-practice reference:** Azure API Management as AI gateway (token limit / quota policies); Well-Architected Cost — guardrails on consumption-billed services.

---

### ARC-07 — Sidecar is a single point of failure with no health probe and no graceful drain; deploys kill in-flight streaming completions
- **Severity:** Major
- **Lens:** Reliability, Operational Excellence
- **Target:** [copilot-runtime/server.ts L60 (`app.listen`, no `/health`, no SIGTERM handler)](templates/csharp-api/copilot-runtime/server.ts#L60); handler rebuilt per request [L51-L57](templates/csharp-api/copilot-runtime/server.ts#L51-L57).
- **Attack:** The sidecar exposes no readiness/liveness endpoint, so an orchestrator (Container Apps/AKS) cannot tell if it is healthy or restart it deterministically. CopilotKit responses are long-lived SSE streams; there is no `SIGTERM` handler to stop accepting new work and drain in-flight streams before exit.
- **Impact:** When the sidecar crashes or is rolled during a deploy, every in-flight copilot completion is severed mid-stream and the entire copilot surface goes dark (the REST path survives, but the AI value prop does not). Without a health probe, autohealing and connection draining cannot function.
- **Hardening:** Add `/health` (liveness) and `/ready` (readiness that checks OpenAI reachability), a `SIGTERM` handler that stops the listener and waits for active streams (bounded), configure the platform's `terminationGracePeriod`, and run ≥2 replicas. Build the `copilotRuntimeNodeHttpEndpoint` handler once at startup, not per request.
- **Best-practice reference:** Well-Architected Reliability — health modeling & graceful shutdown; Kubernetes/Container Apps probe + preStop drain guidance.

---

### ARC-08 — Sidecar has asymmetric fail-fast and weaker auth than the API (non-null assertions, no startup validation, API key vs. managed identity)
- **Severity:** Major
- **Lens:** Operational Excellence, Reliability (Security)
- **Target:** [copilot-runtime/server.ts L29 (`apiKey`), L30-L31 (`!` non-null on endpoint/deployment)](templates/csharp-api/copilot-runtime/server.ts#L29-L31); contrast C# [Program.cs `ValidateOnStart()` L10-L13](templates/csharp-api/FinancialServices.Api/Program.cs#L10-L13) and managed-identity rule in [azure-services.instructions.md](.github/instructions/coding-standards/azure-services.instructions.md).
- **Attack:** The C# API validates required options at boot and fails loudly. The sidecar does the opposite: `AZURE_OPENAI_ENDPOINT!` / `AZURE_OPENAI_DEPLOYMENT!` are `undefined`-swallowing non-null assertions, so a misconfigured sidecar starts "healthy" and only fails on the first user request. It also authenticates to Azure OpenAI with a raw **API key**, contradicting the kit's `DefaultAzureCredential`/managed-identity standard used everywhere else.
- **Impact:** Misconfiguration surfaces as user-facing 500s at runtime instead of a failed deploy (the exact "fail loudly at startup" property the kit prides itself on — violated on the weaker tier). The API key is a long-lived secret to rotate/leak, widening the blast radius and creating an inconsistent identity story across the two backends.
- **Hardening:** Validate required env at sidecar startup and exit non-zero if missing (mirror `ValidateOnStart`). Replace the API key with `DefaultAzureCredential` / managed identity (`@azure/identity` bearer token provider). Pin `engines.node`.
- **Best-practice reference:** Well-Architected Security — managed identities over keys; fail-fast configuration validation.

---

### ARC-09 — End-to-end tracing is broken across the sidecar boundary; the action seam is untestable
- **Severity:** Major
- **Lens:** Operational Excellence
- **Target:** sidecar has no telemetry and forwards no `traceparent`: [copilot-runtime/server.ts L43 (`fetch` with no headers)](templates/csharp-api/copilot-runtime/server.ts#L43-L45); C# OTel is API-only [Infrastructure/ServiceCollectionExtensions.cs L42-L58](templates/csharp-api/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L42-L58); action handlers are inline closures [server.ts L36-L49](templates/csharp-api/copilot-runtime/server.ts#L36-L49).
- **Attack:** The sidecar has no OpenTelemetry and its `fetch` to the C# API sends no W3C `traceparent`. The C# `AddHttpClientInstrumentation`/`AddAspNetCoreInstrumentation` therefore start a **new**, disconnected trace. The action handlers embed `fetch` directly with no injection point, so they cannot be unit tested and there is no sidecar test project.
- **Impact:** A failing copilot action produces two unlinked traces (sidecar invisible, API isolated); on-call must correlate the 3-hop path by wall-clock timestamps. The highest-risk seam (the process that forwards tool calls and holds the OpenAI key) ships with zero automated tests.
- **Hardening:** Instrument the sidecar with OpenTelemetry, propagate `traceparent`/correlation headers on every forwarded `fetch`, and export to the same App Insights resource. Extract action handlers into importable, injectable modules and add `vitest` coverage with a mocked API client.
- **Best-practice reference:** W3C Trace Context; OpenTelemetry context propagation; Well-Architected Operational Excellence — distributed tracing.

---

### ARC-10 — The API contract is hand-duplicated in three places and has already drifted (`Id` vs `PortfolioId`)
- **Severity:** Major
- **Lens:** Operational Excellence, Reliability
- **Target:** C# record [Models/PortfolioModels.cs L18-L25 (`Id`)](templates/csharp-api/FinancialServices.Api/Models/PortfolioModels.cs#L18-L25); instructions example uses `PortfolioId` in [csharp-backend.instructions.md](.github/instructions/coding-standards/csharp-backend.instructions.md); action param `portfolioId` in [copilot-runtime/server.ts L41](templates/csharp-api/copilot-runtime/server.ts#L41); frontend consumes `PortfolioSummary` via the hook in [react-frontend.instructions.md](.github/instructions/coding-standards/react-frontend.instructions.md).
- **Attack:** The same portfolio contract is defined independently as a C# record, a TypeScript type, and a CopilotKit action parameter list. Nothing generates one from another. The shipped templates already disagree: the domain record's key field is `Id` (serialized `id`), while the standards/docs and hooks talk in `PortfolioId`.
- **Impact:** A field rename or type change on one side compiles cleanly and breaks the others only at runtime (e.g., the frontend reads `portfolio.portfolioId` and gets `undefined` because the wire field is `id`). For financial data this is silent mis-binding of money/identifiers. Three-way manual sync guarantees recurring drift.
- **Hardening:** Generate TS types (and ideally the copilot action schemas) from the C# OpenAPI document (NSwag/Kiota/openapi-typescript) in CI; fail the build on drift. Keep one source of truth (the C# API) and align `Id`/`PortfolioId` naming deliberately.
- **Best-practice reference:** OpenAPI-driven client generation (Kiota / NSwag); contract-testing to prevent drift.

---

### ARC-11 — No timeouts, retries, or circuit breakers anywhere; the "fail loudly" rule is being read as "don't add resilience"
- **Severity:** Major
- **Lens:** Reliability, Performance
- **Target:** unbounded outbound `fetch` [copilot-runtime/server.ts L43-L45](templates/csharp-api/copilot-runtime/server.ts#L43-L45); C# service catches only `NotFound` and lets all else bubble [Services/PortfolioService.cs L38-L42](templates/csharp-api/FinancialServices.Api/Services/PortfolioService.cs#L38-L42); no `AddStandardResilienceHandler`/HttpClient policies in [Program.cs](templates/csharp-api/FinancialServices.Api/Program.cs) or [ServiceCollectionExtensions.cs](templates/csharp-api/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs); "No silent fallbacks" rule in [copilot-instructions.md](.github/copilot-instructions.md).
- **Attack:** The sidecar `fetch` has no `AbortSignal.timeout`, no retry, no breaker — a slow/hung C# API pins sidecar connections and, transitively, the copilot. The C# path adds no `HttpClient` resilience for Foundry/OpenAI/Search calls and no request timeout wrapping the plumbed `CancellationToken`. The kit's "fail loudly, no fallbacks" wording (intended against *fake data*) is easily mis-applied to mean "never catch/retry," so implementers turn transient 429/503/timeout blips into hard 500s.
- **Impact:** A routine Azure transient (Cosmos 429 after retries exhausted, OpenAI throttling, a 2s network stall) becomes a customer-facing outage instead of a brief, recovered hiccup. Unbounded calls also have no tail-latency ceiling, degrading the whole 3-hop path under load.
- **Hardening:** Add `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler`: timeout + retry-with-jitter + circuit breaker) to all outbound HttpClients; wrap agent/DB calls in per-request timeouts (linked `CancellationTokenSource`). Give the sidecar `fetch` an `AbortSignal.timeout` + bounded retry. Clarify the standard: "no *fake-data* fallbacks" ≠ "no resilience."
- **Best-practice reference:** Well-Architected Reliability — transient fault handling; `Microsoft.Extensions.Http.Resilience` / Polly; Retry + Circuit Breaker patterns.

---

### ARC-12 — SignalR is prescribed for real-time features but there is no backplane or sticky-session strategy
- **Severity:** Major
- **Lens:** Reliability, Performance
- **Target:** SignalR hub guidance in [csharp-backend.instructions.md](.github/instructions/coding-standards/csharp-backend.instructions.md) and [azure-services.instructions.md](.github/instructions/coding-standards/azure-services.instructions.md); no `AddSignalR`/`MapHub`/Azure SignalR wiring in [Program.cs](templates/csharp-api/FinancialServices.Api/Program.cs); proxy passes `ws: true` in [vite.config.ts L20-L23](templates/frontend-design-system/vite.config.ts#L20-L23).
- **Attack:** The standards push SignalR hubs (transcription, live workflow status) but the host template wires no backplane. Default SignalR requires sticky sessions with multiple replicas; scale-out without Azure SignalR Service or a Redis backplane means clients whose negotiate and long-poll/websocket land on different instances get dropped connections.
- **Impact:** As soon as the API scales beyond one replica (the whole point of Container Apps), real-time features intermittently fail negotiation and reconnect loops; every deployment/rollout severs hub connections with no drain. This surfaces as flaky live transcription/status for advisors.
- **Hardening:** Adopt Azure SignalR Service (or Redis backplane) and enable it in the host template; configure sticky sessions if self-hosting; define reconnection + drain behavior. Decide this before the first hub ships, not after scale-out breaks.
- **Best-practice reference:** Azure SignalR Service scale-out guidance; ASP.NET Core SignalR hosting/scale documentation.

---

### ARC-13 — Agent constructs a new `AIProjectClient` and a new `DefaultAzureCredential` on every request
- **Severity:** Major
- **Lens:** Performance, Cost
- **Target:** [Agents/PortfolioAnalysisAgent.cs L28-L33](templates/csharp-api/FinancialServices.Api/Agents/PortfolioAnalysisAgent.cs#L28-L33); singleton-client guidance in [azure-services.instructions.md](.github/instructions/coding-standards/azure-services.instructions.md) and [csharp-backend.instructions.md](.github/instructions/coding-standards/csharp-backend.instructions.md).
- **Attack:** `AnalyzeAsync` does `new AIProjectClient(new Uri(...), new DefaultAzureCredential())` per call. `DefaultAzureCredential` re-probes its credential chain and token cache is per-instance, and a fresh SDK client re-establishes connection/handler state each time — directly against the "register clients as singletons; never recreate per request" rule the kit states for Cosmos.
- **Impact:** Extra token-endpoint round trips and socket churn on every agent invocation add latency and can trip AAD/token throttling under load; the cost/perf regression compounds on the already-long 3-hop path. (Analogous risk exists anywhere Search/Content Safety clients are `new`'d per call in the docs' examples.)
- **Hardening:** Register `AIProjectClient` (and a single shared `DefaultAzureCredential`, `SearchClient`, `ContentSafetyClient`) as singletons in `AddAzureClients` and inject them; cache resolved agents by name. Reuse one credential instance process-wide.
- **Best-practice reference:** Azure SDK client lifetime guidance (singletons); `DefaultAzureCredential` reuse & token caching.

---

### ARC-14 — Telemetry silently disables itself when the connection string is absent (contradicts "fail loudly")
- **Severity:** Minor
- **Lens:** Operational Excellence
- **Target:** [Infrastructure/ServiceCollectionExtensions.cs L44-L52](templates/csharp-api/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L44-L52).
- **Attack:** `if (hasConnectionString) otel.UseAzureMonitor();` — when `APPLICATIONINSIGHTS_CONNECTION_STRING` is unset, Azure Monitor export is skipped with no error. This is a *silent fallback* for a critical operational capability, the very anti-pattern the kit forbids for data.
- **Impact:** A production deploy missing the connection string runs blind (and, via ARC-03, loses the only "audit" too) without any startup signal. Incidents become undiagnosable.
- **Hardening:** In non-Development environments, require the connection string via options validation and fail startup if missing; only allow the no-op path in local dev.
- **Best-practice reference:** Well-Architected Operational Excellence — observability as a deploy prerequisite.

---

### ARC-15 — CORS allow-lists the server-side sidecar origin (`:4000`), which is dead and misleading config
- **Severity:** Minor
- **Lens:** Operational Excellence (Security hygiene)
- **Target:** [Program.cs L21-L27](templates/csharp-api/FinancialServices.Api/Program.cs#L21-L27) (default origins include `http://localhost:4000`); mirrored default in [csharp-backend.instructions.md](.github/instructions/coding-standards/csharp-backend.instructions.md).
- **Attack:** CORS is browser-enforced; the sidecar calls the API server-to-server and sends no `Origin`, so listing `:4000` grants nothing. Its presence implies the sidecar is a browser origin and invites someone to "keep it working" by loosening CORS.
- **Impact:** Cargo-cult config that obscures the real trust model and can nudge future changes toward over-permissive CORS. Harmless today, misleading tomorrow.
- **Hardening:** Remove `:4000` from CORS origins; document that the sidecar is a trusted server-side caller secured by auth/network, not CORS.
- **Best-practice reference:** MDN/OWASP CORS guidance — CORS is not an authorization control.

---

### ARC-16 — Floating dependency ranges and a `tsx`-only run seam for `server.ts` with no runtime pin
- **Severity:** Minor
- **Lens:** Operational Excellence, Reliability
- **Target:** [copilot-runtime/package.json L11-L18](templates/csharp-api/copilot-runtime/package.json#L11-L18) (`^` ranges, no `engines`); NuGet wildcard versions (`1.*`, `11.*`) in [csharp-backend.instructions.md](.github/instructions/coding-standards/csharp-backend.instructions.md).
- **Attack:** `@copilotkit/runtime ^1.5.0`, `express ^4`, and NuGet `*` wildcards float transitively; no `engines.node` pin. A minor bump to the CopilotKit runtime or the Agent Framework surface (whose API the agent comment already warns "can vary by package version") can change behavior between builds.
- **Impact:** Non-reproducible builds; "works on my machine" drift; the untested sidecar (ARC-09) has no safety net to catch a breaking minor bump.
- **Hardening:** Pin exact versions (or commit a lockfile and use `npm ci`), add `engines.node`, and pin NuGet versions with `Directory.Packages.props` (central package management). Add CI that builds the sidecar.
- **Best-practice reference:** Reproducible builds; NuGet Central Package Management; `npm ci` + lockfile.

---

## Design Strengths (honest)

- **Options validated at startup on the C# tier.** `AddOptions<AzureOptions>().ValidateDataAnnotations().ValidateOnStart()` ([Program.cs L10-L13](templates/csharp-api/FinancialServices.Api/Program.cs#L10-L13)) genuinely fails fast for missing config on the primary backend — the right instinct (just not mirrored in the sidecar, ARC-08).
- **Thin controllers, real separation.** [PortfoliosController](templates/csharp-api/FinancialServices.Api/Controllers/PortfoliosController.cs#L15-L23) delegates to `IPortfolioService` and returns RFC 7807 `ProblemDetails`; DTOs are `record`s using `decimal` for money ([PortfolioModels.cs](templates/csharp-api/FinancialServices.Api/Models/PortfolioModels.cs#L9-L14)). Layering is clean where it exists.
- **Managed identity on the data/agent plane.** The C# clients use `DefaultAzureCredential` (Cosmos in [ServiceCollectionExtensions.cs L17-L28](templates/csharp-api/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L17-L28)); the browser holds no Azure creds. Correct direction (undercut only by the sidecar's API key).
- **CORS is origin-scoped, not wildcard;** `WithOrigins(...)` rather than `AllowAnyOrigin` ([Program.cs L23-L27](templates/csharp-api/FinancialServices.Api/Program.cs#L23-L27)).
- **REST and copilot are separated,** so a sidecar outage degrades AI features without taking down core REST — a real blast-radius containment benefit of the two-front-door topology (the same split that creates ARC-05/06).
- **Test hook present** (`public partial class Program;`) enabling `WebApplicationFactory<Program>` integration tests on the API tier.

---

## Top 3 Must-Fix

1. **Enforce the rebalance human-in-the-loop gate and authentication server-side (ARC-02).** Today any anonymous caller — or a prompt-injected copilot tool call — can submit an unapproved, unauthenticated rebalance; move authorization and the approval check to the API trust boundary.
2. **Fix the portfolio point-read partition key (ARC-01).** Reading by `PartitionKey(portfolioId)` against a `/clientId`-partitioned container makes the primary read path 404 (or forces a costly cross-partition fan-out) — the happy path is currently broken.
3. **Make the audit trail durable and non-bypassable (ARC-03).** Replace the `ILogger` "audit" with an immutable `auditLog` write that gates the mutation, so every rebalance has a provable approver/timestamp even when telemetry is off. *(Close behind: add idempotency + a job store, ARC-04.)*
