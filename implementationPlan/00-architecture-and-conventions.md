# 00 — Architecture & Conventions

**Purpose:** lock the moving parts, the integration style, and the naming rules before any code.
Read once; refer back from every other package.

---

## Component & process map

```
┌─ Vite + React 18 (port 5173) ─ Tailwind · shadcn · TanStack Query/Table · CopilotKit · recharts
│     proxies:  /api → :8000    /copilotkit → :4000
├─ Node CopilotKit runtime (port 4000) ─ ExperimentalEmptyAdapter + HttpAgent → C# AG-UI
└─ ASP.NET Core + Microsoft Agent Framework (port 8000)
        AddAGUI() + MapAGUI("/prism", orchestrator)     ← AG-UI over HTTP+SSE (streaming demo)
        REST controllers under /api/v1/...              ← TanStack Query data (issuers, dossiers)
        specialists = in-process function tools → live CopilotKit cards
        ├→ Azure AI Foundry  (GPT-5.x / GPT-4.1 · Responses API v2)
        ├→ Azure AI Search Basic  (synthetic rating cards + methodology docs)
        ├→ SEC EDGAR / FRED / Treasury  (real, free, as-of filtered in C#)
        └→ Cosmos DB free tier  (reconciliation dossiers + audit log)
```

Three local processes — start with `run-backend.bat`, `run-copilot-runtime.bat`, `run-frontend.bat`.

---

## Integration decision: AG-UI primary, actions fallback

- **Primary (do this):** the C# API hosts **one AG-UI agent** (the orchestrator). Specialists run
  as in-process **function tools**; each tool call renders as a live CopilotKit generative-UI card.
  The Node sidecar uses `ExperimentalEmptyAdapter` + an `HttpAgent` pointed at the C# AG-UI endpoint.
  This is the demo centerpiece (visible orchestration). See package 07.
- **Fallback (if AG-UI streaming misbehaves):** revert the Node sidecar to the accelerator's
  `OpenAIAdapter` + `actions` pattern (already in `templates/csharp-api/copilot-runtime/server.ts`)
  and drive the pipeline through a plain REST `POST /api/v1/reconciliations` + SSE progress. Keep
  this path buildable at all times.

> **The .NET landmine (from HACKATHON-FINDINGS §3):** multi-agent *workflow* streaming over AG-UI
> is Python-only today. We expose exactly ONE AG-UI agent and orchestrate specialists in-process.
> Never bet the demo on prerelease workflow streaming.

---

## Repository layout (target)

```
backend/
  FinancialServices.Api/        # copied from templates/csharp-api, renamed for Prism
    Program.cs
    Agents/                     # provider agents, fundamentals, red-flag narrator, orchestrator wiring
    Orchestration/              # AG-UI orchestrator + function-tool definitions + HITL gate
    Analysis/                   # DivergenceDecomposer, RedFlagEngine, NotchLadder (DETERMINISTIC, no LLM)
    Connectors/                 # EdgarClient, FredClient, TreasuryClient
    Models/                     # domain records + DTOs
    Services/                   # ReconciliationService, AuditService, corpus/search access
    Controllers/                # IssuersController, ReconciliationsController, HealthController
    Infrastructure/             # AzureOptions, ServiceCollectionExtensions, telemetry
  FinancialServices.Tests/      # xUnit
  tools/SeedData/               # console app: authors + uploads synthetic corpus to AI Search
copilot-runtime/                # Node sidecar (server.ts)
frontend/                       # copied from templates/frontend-design-system + workflow-visualization
run-backend.bat  run-copilot-runtime.bat  run-frontend.bat
infra/                          # Bicep/azd for ACA deploy (package 11)
```

---

## Naming (financial-domain aligned)

| Concept | Type / name |
|---|---|
| Bond issuer | `Issuer` (`issuerId`, `legalName`, `ticker`, `cik`, `sector`) |
| One provider's assessment | `ProviderRating` (`provider`, `letter`, `notch`, `asOfDate`, `factors[]`) |
| A scored input | `RatingFactor` (`name`, `weight`, `score`, `sourceRef`) |
| Real financials | `FundamentalSnapshot` (from EDGAR, with `filingDate`) |
| The comparison output | `DivergenceResult` (pairwise gaps + attribution waterfall) |
| A finding | `RedFlag` (`code`, `severity`, `rule`, `evidence[]`) |
| The deliverable | `ReconciliationDossier` |
| The three providers | `Provider` enum: `Moodys`, `MorningstarDbrs`, `Msci` |

**Say:** reconciliation, divergence, provenance, as-of, coverage, notch gap, data quality,
methodology attribution, auditable, cited.
**Never say:** buy / sell / hold / recommend / allocate / trade / position sizing / alpha / signal.

---

## Environment variables (single source of truth)

Backend binds `Azure__*` → `AzureOptions` (see accelerator `AzureOptions.cs`). Add Prism keys:

```
# Foundry / OpenAI
Azure__AiProjectEndpoint=      Azure__AiProjectName=
Azure__OpenAiEndpoint=         Azure__OpenAiApiVersion=2024-12-01-preview
# Data stores
Azure__CosmosEndpoint=         Azure__CosmosDatabase=prism
Azure__SearchEndpoint=         Azure__SearchIndex=prism-ratings
# Real data (package 04)
Prism__FredApiKey=             Prism__SecUserAgent=prism-hack contact@example.com
# Models per agent (package 06)
Prism__Models__Orchestrator=gpt-5      Prism__Models__Provider=gpt-4.1-mini
Prism__Models__Fundamentals=gpt-4.1-mini   Prism__Models__RedFlag=gpt-5-mini
# Copilot runtime (Node)
AZURE_OPENAI_ENDPOINT=  AZURE_OPENAI_DEPLOYMENT=  AZURE_OPENAI_API_VERSION=2024-12-01-preview
API_BASE_URL=http://localhost:8000   PRISM_AGUI_URL=http://localhost:8000/prism
```

`.env` is git-ignored. Missing required settings must **fail fast** (accelerator already validates
`AzureOptions` on start). No mock/fallback values.

---

## Acceptance for this package
- [ ] Team agrees AG-UI is primary, actions is fallback.
- [ ] Folder layout created (empty dirs ok).
- [ ] Env var catalog copied into `.env.example`.
- [ ] Say/never-say list pinned where the whole team sees it.
