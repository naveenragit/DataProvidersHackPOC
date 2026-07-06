# Bloomberg + Microsoft Hackathon — Research Findings & Build Plan
*Session record, 2026-07-06. Polished build plan (shareable): https://claude.ai/code/artifact/7e439dc2-f51c-44ba-9dda-9b7790dcf0e8 (v2; v1 "Deal Room" in version history).*

---

## 1. What we're building (final decision)

**Rewind — The Decision Black Box Recorder.** A flight recorder for investment decisions: compliance types a position + date ("On 14 Mar 2025 the firm added NordStar Industrials the day before its downgrade — reconstruct the decision, prove no MNPI"), and AI agents rebuild the regulator-ready who-knew-what-when dossier in ~90 seconds instead of ~3 weeks of email archaeology.

- **Money moment:** Red-Flag agent finds "the risk approval was signed 6 hours BEFORE the valuation model it cites was last edited" — a red arrow across the timeline. Crucially this is a **deterministic C# timestamp rule** (`approvalTs < modelEditedTs`), not LLM insight → reproducible on stage.
- **The fusion thesis:** two timeline lanes — *what the firm privately knew* (vault) vs *what the market publicly knew* (as-of-date FRED/Treasury + publication-stamped news). The information gap between lanes IS the compliance finding.
- **Why not cliché:** never recommends/scores/gates a trade; looks strictly backward. Bloomberg ASKB & MS Researcher look forward and summarize; nobody reconstructs with provenance.

### Decision log
| Decision | Choice |
|---|---|
| Market data | Synthetic-first + free live APIs (FRED/Treasury); no Bloomberg access assumed |
| Original persona | Trader capital-commit copilot ("The Deal Room") — **pivoted** after "automated trading agent is beaten to death" |
| Team / time | 2–4 people, 4–5 days |
| Stack | Vite + React 18, Tailwind, shadcn/Radix, TanStack Query/Table, CopilotKit; C#/.NET API layer |
| Orchestration preference | Fable orchestrates, Opus 4.8 subagents (session workflow preference) |

---

## 2. The five agents

| Agent | Job | Tools | Model |
|---|---|---|---|
| Reconstruction Orchestrator | Parses inquiry, plans sweep, owns confirm-scope human gate, assembles dossier | Specialists as in-process function tools; the AG-UI entry agent | gpt-5 / gpt-5.4 medium thinking |
| Vault Forensics | Retrieves + time-orders IC minutes, approvals, research notes, Outlook, Teams (author/role/timestamps) | Azure AI Search hybrid+vector | gpt-5-mini |
| Market Snapshot Reconstructor | Rebuilds market state AS OF the decision date (no hindsight) | Deterministic C#: as-of filtering over FRED/Treasury + publication-stamped synthetic news | gpt-4.1-mini |
| Timeline Assembler | Merges evidence into strictly-ordered chain; emits timeline structure for React | **Pure deterministic C# merge/sort — no LLM** | — |
| Gap & Red-Flag (the payoff) | Audits chain: out-of-order timestamps, unsigned approvals, restricted-list proximity, traded-ahead-of-news | C# rules pass + LLM narration; every flag cites source doc + timestamp | gpt-5-mini |

**Design rule:** timeline ordering and flag triggers are deterministic C#; the LLM only narrates and cites. Show the rule on screen when a flag fires (defuses "you rigged the corpus" skepticism). Also demo an **all-green control decision** — the system must exonerate, not just incriminate.

### 3-minute demo script
1. **0:00–0:20** Inquiry pasted; stopwatch starts; "manual baseline ~3 weeks" ghost bar.
2. **0:20–0:35** Human scope-confirmation gate (governed agents on display).
3. **0:35–1:15** Forensic sweep: evidence cards stream (CopilotKit); Market Snapshot rebuilds the decision-date tape.
4. **1:15–1:45** Two-lane black-box timeline assembles.
5. **1:45–2:15** RED FLAG: approval predates its own evidence; red arrow; click shows the deterministic rule + both source docs.
6. **2:15–2:35** Control run: all green, "fully defensible."
7. **2:35–3:00** Export dossier (paginated, every claim hyperlinked). Stopwatch: ~90s vs 3 weeks.

### Planted needles (3–4 sharp, ranked — never 15)
1. Risk-approval signature timestamped before the valuation model file's last edit (the gotcha)
2. Analyst note dated after the trade (hindsight fabrication)
3. Teams thread naming a restricted-list contact (MNPI proximity)
4. Clean IC minute chain (the backbone) + a second fully-documented control decision (all green)

---

## 3. Architecture (validated by research)

```
Vite + React 18 (Tailwind · shadcn · TanStack · CopilotKit)
  └→ Node CopilotKit runtime (~30 lines; HttpAgent → C# endpoint; ExperimentalEmptyAdapter)
       └→ ASP.NET Core (.NET 8) + Microsoft Agent Framework v1.0 (GA Apr 2026)
            AddAGUI() + MapAGUI("/", orchestrator)   ← AG-UI over HTTP+SSE
            specialists = in-process function tools → live CopilotKit cards
            ├→ Azure AI Foundry  (AIProjectClient(...).AsAIAgent(...); GPT-5.x / GPT-4.1)
            ├→ Azure AI Search Basic (synthetic vault, hybrid+vector, timestamp metadata)
            └→ Cosmos DB free tier (inquiry log + audit trail)
```

- **THE landmine:** multi-agent *workflow* streaming over AG-UI is **Python-only** (.NET "coming soon"). Workaround (documented, low-effort): expose ONE AG-UI agent; orchestrate specialists in-process; each specialist call = function tool call → rendered as a live generative-UI card. Never bet the demo on prerelease workflow streaming.
- Key NuGet: `Microsoft.Agents.AI` (GA); `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` + `Microsoft.Agents.AI.Foundry` (**prerelease — pin exact versions day 0**); `Azure.AI.Projects` 2.x (GA); `Azure.Identity`.
- Function tools: `AIFunctionFactory.Create(...)` with `[Description]`; `ApprovalRequiredAIFunction` maps onto CopilotKit `renderAndWaitForResponse` for human gates.
- Canonical starter: CopilotKit monorepo `examples/integrations/ms-agent-framework-dotnet` (NOT the archived standalone repo). Per-feature examples: https://dojo.ag-ui.com/microsoft-agent-framework-dotnet. Docs: https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/ and https://docs.copilotkit.ai/microsoft-agent-framework.
- Direct React→AG-UI without the Node runtime works but is "not officially supported for production" — keep the thin Node runtime.
- Three local processes (Vite / Node / .NET) — script together; proxy `/api/copilotkit` in vite.config.ts.
- Fallbacks if the AG-UI path breaks: (A) already baked in — single agent + tool-call cards; (B) custom SSE/SignalR event envelope from ASP.NET (~1–1.5 days); (C) Python AG-UI backend for workflow streaming only; (D) Copilot Cloud instead of self-hosted Node runtime.

---

## 4. Azure AI Foundry research (mid-2026 state)

- Rebranded "Microsoft Foundry"; Agent Service is **GA**. **Two SDK generations — do not mix**: modern = `azure-ai-projects >= 2.0` / `Azure.AI.Projects` 2.x, primitives = Agents (versioned by name), Conversations, **Responses API** (single entry point, `agent_reference`); classic = 1.x threads/runs/ConnectedAgentTool (docs now "foundry-classic"). Copy-pasting across generations breaks builds.
- **Auth is Entra ID ONLY** on the Projects client (no API key). `az login` locally, managed identity in Azure.
- Models: GPT-5 family (reasoning; 4 thinking levels; MS's recommendation for agentic tool-use + financial analysis), plus GPT-5.4/-mini/-nano (5.4-mini ~2× faster than 5-mini); GPT-4.1/-mini for cheap high-throughput generation; also Grok-4, Llama, DeepSeek, Claude (partner/marketplace). Model Router preview can cut cost ~60%.
- **GPT-5 requires one-time registration (aka.ms/openai/gpt-5) — do day 0; keep GPT-4.1 fallback.**
- Tools (GA): File Search/vector stores, Azure AI Search, Code Interpreter, function calling, web/Bing grounding, MCP (1,400+ connectors), OpenAPI. Preview (avoid for demo-critical): SharePoint, Fabric, A2A, Memory, Browser/Computer Use.
- Region-gated: Responses API + File Search + Code Interpreter not everywhere (File Search absent in Italy North / Brazil South) — pick region before provisioning.
- Rate limits: multi-agent fan-out hits 429s on default TPM quota fast — request bump day 0, backoff with jitter, put chatty agents on gpt-4.1-mini. Parallel tool calls NOT supported at 'minimal' reasoning effort.
- Fixed service limits: 128 tools/agent, 512 MB/file, 2M tokens/file into vector store, etc.
- Observability built in: end-to-end tracing + App Insights — turn on day 1, it's a judging asset.

## 5. Market data research

- **Bloomberg APIs are off-limits** for a hackathon: BLPAPI needs Terminal license, B-PIPE/Data License need enterprise contracts. Only **OpenFIGI** (identifiers) is free. Cannot legally scrape Terminal data — label mock data clearly.
- Free real feeds worth using: **FRED** (DGS2/DGS10/T10Y2Y, ICE BofA OAS credit-spread indices), **US Treasury FiscalData** (daily par yield curves, no key), SEC EDGAR. Corporate bond *prices* free = essentially nothing usable (FINRA TRACE has friction) → **synthesize the bond, real curves underneath**.
- Model the mock market feed as an **MCP-shaped tool** — Bloomberg itself is an MCP adopter / Agentic AI Foundation co-supporter, so the architecture mirrors a real future integration (judge talking point).

## 6. Enterprise vault research

- **M365 Developer Program is closed to individuals** (since 2024; 2025 revamp limits E5 sandboxes to VS Pro/Enterprise subscribers, ISV/partner members, Premier/Unified support). Real Graph auth (Entra app registration, admin-consent app-only permissions) would burn 1–2 days for a *less controllable* demo. **Simulate.**
- Synthetic corpus: 40–80 curated LLM-generated docs (emails, Teams, CRM notes, IC minutes, approvals, postmortems) with author/role/timestamp metadata. **Curation beats volume — the demo wins on planted needles, not corpus size.**
- **Azure AI Search Basic (~$75/mo prorated, inside $200 credit)** via Import-and-vectorize wizard. **Free tier is a trap**: 50 MB + 20 docs/day enrichment cap. Embedding model at index time MUST match query-time vectorizer (`text-embedding-3-small`) or retrieval silently degrades.
- Optional realism veneer: Dev Proxy Graph mocks (waldekmastykarz/graph-mocks) make code call graph.microsoft.com-shaped endpoints without a tenant.

## 7. Deployment research

- **Azure Container Apps** for both servers (ASP.NET API + Node CopilotKit runtime, same environment). **Avoid App Service for SSE** (Windows plans buffer via IIS/ARR and break streaming; Linux needs DisableBuffering + flush hacks). ACA/Envoy is clean.
- **`minReplicas: 1`** (scale-to-zero cold start would hit during judging); **heartbeat every ~20s** (ingress/LB idle out at 230–240s).
- Frontend: Static Web Apps Free (auto HTTPS) or serve `dist/` from a container.
- **RBAC is client-dependent**: `AIProjectClient` → **`Foundry User`** (GUID `53ca6127-db72-4b80-b1b0-d745d6d5456d`); `AzureOpenAIClient` → **`Cognitive Services OpenAI User`**; never `Azure AI Developer` (wrong product). Use GUIDs in Bicep (rename mid-rollout). **RBAC propagation takes ~10 min** — "works locally, 403 in cloud" = wait, don't debug.
- `DefaultAzureCredential` promotes local→managed identity with no code change; set `AZURE_CLIENT_ID` for user-assigned identity.
- azd skeletons (ranked): `Azure-Samples/ai-chat-quickstart-csharp` (lightest C#: ACA+ACR+RBAC), `azure-search-openai-demo-csharp` (RAG pre-wired: ACA+OpenAI+AI Search Basic+Blob), `get-started-with-ai-agents` (Foundry reference, Python/TS), `azd-ai-starter-basic` (Foundry+model Bicep). The only official .NET Agent Framework sample targets App Service and does NOT provision Foundry.
- DBs: **Cosmos DB lifetime free tier** (1000 RU/s + 25 GB, one/subscription, opt-in at creation) for blotter/audit. Cosmos DiskANN vector search exists but wants ≥1000 vectors + dedicated-throughput container → AI Search stays the vault store for our small curated corpus.
- First green deploy: ~half a day from a template; then `azd deploy <service>` = 1–3 min. Costs fit inside $200 credit (ACA a few $/day warm + AI Search ~$2.50/day + tokens).

## 8. Prior art / differentiation research

- **Bloomberg ASKB (Feb 2026)**: Terminal conversational interface run by parallel agent networks with citations + reusable workflows → "AI summarizes market data with citations" is incumbent table stakes. Bloomberg is an MCP adopter.
- Microsoft: Researcher/Analyst agents (GA Jun 2025, reason over email/meetings/files), Finance in M365 Copilot (GA Oct 2025, ERP/accounting) — all horizontal, not desk-level.
- Open source saturation: ai-hedge-fund & TradingAgents (~60k stars each), FinRobot/FinGPT/OpenBB — equities, public-data-only, buy/sell personas. **This is the cliché the user rightly vetoed.**
- 2025 MS AI Agents Hackathon winners' formula: **data fusion + visible orchestration + action-taking + trust/verification**; rubrics weight running code, innovation, impact, theme alignment (~25% each) + visible reasoning traces + human-in-the-loop + observability.
- Hard constraints are graded: models MUST come from Foundry (not raw OpenAI/Anthropic keys); app + DBs MUST deploy to Azure.

## 9. Concept evolution (for the record)

- **v1: "The Deal Room"** — trader capital-commitment cockpit with risk clock ($100M AAPL 30Y block, DV01 $170k/bp, 5bp ≈ $850k), needle email, human gate, distribution loop. Fully planned, then user vetoed the trading-decision framing.
- **Ideation fleet**: 6 lenses (trade recommendations banned) × 2 concepts, each judged by adversarial novelty + feasibility judges. All 12 buildable. Shortlist scores (novelty − cliché + alignment + buildable):
  1. **Rewind — Decision Black Box Recorder (19.5) ← CHOSEN**
  2. Echo — Idea Autopsy Room (19.0): grades the firm's dying Teams hunches against what the market did; "4 people were right and never met — convene them?"
  3. Deal Radiography (19.0): workflow archaeology; stuck deals priced by live spread decay
  4. Rewind — Institutional Memory for Market Regimes (18.5); Assumption Ledger (18.5, unstated-assumption tripwires); Successor — departing trader's knowledge estate (18.5); Thesis Coroner (18.2); Crosswire — cross-desk collision detector (18.0, most electric moment, higher build risk)
- Runner-ups are viable pivots — Echo and Assumption Ledger share ~70% of the Rewind backend.

## 10. Day 0 checklist (external lead time — do these first)

- [ ] Register GPT-5 access: aka.ms/openai/gpt-5 (gates deployment)
- [ ] `az login` for every teammate + `Foundry User` RBAC on the project
- [ ] Pick region with Responses API + File Search + Code Interpreter
- [ ] Request model quota (TPM) bump
- [ ] Pin prerelease AG-UI NuGet versions
- [ ] Clone CopilotKit monorepo .NET example + `ai-chat-quickstart-csharp`; hello-world `azd up`

## 11. Day-by-day

| Day | Deliverable |
|---|---|
| 0 (½) | Gates cleared + skeleton deployed |
| 1 | Evidence corpus authored + indexed (acceptance test: mis-ordered approval retrieves with metadata intact); C# as-of FRED filter; React shell |
| 2 | All agents end-to-end in console: dossier object + red flag firing |
| 3 | **Timeline UI (the big investment)**: two lanes, streaming evidence cards, red-arrow animation, scope gate |
| 4 | Dossier export (pre-rendered PDF fallback mandatory) + all-green control + Cosmos audit + `azd up` |
| 5 | Polish, severity-ranked flags, rehearse 3× |

**Cut lines:** live PDF → pre-rendered; MNPI flag → keep timestamp gotcha only; control run → narrated; Azure deploy → localhost against live Foundry.

**Top risks:** timeline illegible in 90s (day 3 is dedicated); PDF time-sink (fallback mandatory); rigged-corpus skepticism (show the deterministic rule + green control); as-of overclaim (say "observation-date filter," not point-in-time vintage); eDiscovery genre adjacency (differentiate on market-fusion lane + visible agents); no buy/sell language anywhere.

---

*Session memory for future Claude sessions is saved under `C:\Users\naveenra\.claude\projects\C--GitHub\memory\` (project state, stack, orchestration preference, and a Workflow structured-output pitfall). Start a fresh session in `C:\GitHub` and it will pick this context up; this file is the human-readable record.*
