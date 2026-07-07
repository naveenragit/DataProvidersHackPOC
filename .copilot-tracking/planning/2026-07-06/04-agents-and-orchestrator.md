# Plan 04 — Agents & Reconstruction Orchestrator (AG-UI)

**Objective:** Build the five-agent governed pipeline. One AG-UI entry agent (the Reconstruction
Orchestrator) orchestrates specialists **in-process as function tools**, so each specialist call
renders as a live CopilotKit card. The deterministic specialists (Market Snapshot, Timeline Assembler)
are C#; the LLM narrates and cites only.

**Depends on:** Plans 01, 02, 03. **Primary day:** 2.

> ⚠ THE landmine: multi-agent *workflow* streaming over AG-UI is **Python-only** (.NET "coming soon").
> Workaround (baked in): expose ONE AG-UI agent; orchestrate specialists in-process; each specialist
> call = function-tool call → live generative-UI card. Never bet the demo on prerelease workflow
> streaming.

---

## 1. Correct the Agent Framework SDK surface (⚠ STK-01/02/03)

- [ ] Confirm packages from Plan 01: `Microsoft.Agents.AI` + **`Microsoft.Agents.AI.AzureAI`** +
      `Azure.AI.Projects` **2.x**
- [ ] Agent retrieval is the extension `projectClient.GetAIAgentAsync(name/id, ct)` from
      `Microsoft.Agents.AI.AzureAI` — **not** a member of `AIProjectClient`
- [ ] Run with `var response = await agent.RunAsync(prompt, cancellationToken: ct);` and read
      `response.Text` — do **not** hard-code `AgentRunResponse` (GA type is `AgentResponse`)
- [ ] Agents are **defined in Azure AI Foundry** (Prompt Agents on Responses API v2) and referenced by
      name/id — never recreated per call. Inject the singleton `AIProjectClient`; cache resolved agents
      by name (⚠ ARC-13)

## 2. The five agents

Author under `backend/FinancialServices.Api/Agents/`.

- [ ] **`ReconstructionOrchestrator`** — parses the inquiry, plans the sweep, owns the confirm-scope
      human gate, assembles the dossier. AG-UI entry agent. Model: `gpt-5` / `gpt-5.4` medium thinking
      (fallback `gpt-4.1`). Specialists registered as its function tools.
- [ ] **`VaultForensics`** — retrieves + time-orders IC minutes, approvals, notes, Outlook, Teams with
      author/role/timestamps via Azure AI Search hybrid+vector (Plan 02). Model: `gpt-5-mini`.
- [ ] **`MarketSnapshotReconstructor`** — narrates the deterministic as-of tape from Plan 03. Model:
      `gpt-4.1-mini`. Filtering stays deterministic C#.
- [ ] **`TimelineAssembler`** — **pure deterministic C#, no LLM** (implemented in Plan 05); exposed as a
      function tool that returns the merged, strictly-ordered two-lane structure.
- [ ] **`GapAndRedFlag`** — runs the deterministic C# rules pass (Plan 05), then narrates each flag with
      `gpt-5-mini`; **every flag cites source doc + timestamp**. The LLM never decides a flag.

## 3. Function tools & the human scope gate

- [ ] Register each specialist as a function tool with `AIFunctionFactory.Create(...)` + `[Description]`
      attributes so the orchestrator can call them and CopilotKit renders each as a card
- [ ] Implement the **confirm-scope human gate** with `ApprovalRequiredAIFunction`, mapped onto
      CopilotKit `renderAndWaitForResponse` (Plan 09 UI). The gate is a real state transition, not a
      boolean (⚠ ARC-02 — enforced server-side, see Plan 06)
- [ ] Treat all LLM-provided tool arguments (decision id, date, scope) as **hostile**; the API
      re-authorizes them server-side (⚠ SEC-03)

## 4. Safety, resilience, observability

- [ ] Run Content Safety on the inbound inquiry text before it reaches the model (⚠ ARC-05); make
      `ContentSafetyEndpoint` required outside Development
- [ ] Wrap agent + Search calls in per-request timeouts (linked `CancellationTokenSource`) and
      retry-with-jitter; chatty specialists on `gpt-4.1-mini` to avoid 429s (⚠ ARC-11, TPM fan-out)
- [ ] `ActivitySource("Rewind.Agents")` spans on every agent + tool call; export to App Insights
- [ ] Never log PII or raw financial doc bodies

## 5. AG-UI hosting

- [ ] In `Program.cs`: `builder.Services.AddAGUI();` and `app.MapAGUI("/", reconstructionOrchestrator);`
      (the single AG-UI agent over HTTP+SSE)
- [ ] Verify prerelease `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` API surface against the pinned
      version before wiring
- [ ] Confirm SSE streams cleanly (Container Apps/Envoy later; localhost now)

## 6. Console end-to-end (Day-2 acceptance)

- [ ] A console/integration test drives the orchestrator on the NordStar inquiry and produces a full
      **dossier object** with the timestamp **red flag firing** on the planted needle
- [ ] The all-green control decision produces a dossier with **zero** flags (exoneration path)

## Acceptance criteria

- Backend compiles with the corrected SDK surface (no `GetAIAgentAsync`/`AgentRunResponse` errors)
- Orchestrator invokes all four specialists; each call is a discrete, traceable function-tool call
- Console run yields the dossier + the deterministic timestamp flag; control run is all-green
- Scope gate suspends the run and resumes on approval
- Content Safety gates inbound text; no PII logged

## Cut-lines

- Fallbacks if AG-UI path breaks (documented, in priority order): (A) already baked in — single agent +
  tool-call cards; (B) custom SSE/SignalR event envelope from ASP.NET (~1–1.5 days); (C) Python AG-UI
  backend for workflow streaming only; (D) Copilot Cloud instead of self-hosted Node runtime
- Guaranteed-demoable core = VaultForensics + TimelineAssembler + GapAndRedFlag (the one timestamp
  flag). Orchestrator narration can be trimmed.
