# GitHub Copilot Instructions — Financial Services AI Platform

This repository builds AI-powered solutions for **Capital Markets, Banking, and Insurance** using
**Microsoft Azure Data and AI Services**. All code, architecture, and design decisions are
Azure-first and financially regulated-industry aware.

---

## Core Principle: Real Azure Services Only

The value of every solution is in **real, plugged-in Azure services** — not simulations.

- **No mock data, stubs, or fake clients** in application code. Agents, Cosmos DB, AI Search,
  Content Safety, Speech, and grounding/Bing calls must run against **real Azure resources**.
- **No silent fallbacks.** Do not degrade to canned/sample responses when a service is
  unconfigured or unreachable — fail loudly with a clear, actionable error instead.
- **Mocks/fakes are allowed only inside automated test code** (`pytest`, `vitest`) — never in
  runtime paths shipped to users.
- Configuration comes from a populated `.env`. If required Azure settings are missing, surface
  an explicit error telling the developer which variables to set — never substitute fake values.
- Code is not considered "done" until it has been exercised against live Azure resources.

---

## Domain Focus

All features, agents, models, APIs, and UI must be scoped to the financial services domain:

- **Capital Markets** — equities, fixed income, derivatives, portfolio management, market risk, trading, SEC filings, earnings analysis
- **Banking** — retail and corporate banking, credit risk, KYC/AML, fraud detection, loan origination, treasury
- **Insurance** — underwriting, claims processing, actuarial analysis, policy management, reinsurance, catastrophe modeling

When naming components, routes, agents, models, or data fields, prefer financial industry terminology
(e.g., `portfolio`, `position`, `counterparty`, `instrument`, `trade`, `underwriting_rule`).

---

## Architecture Pattern

Every solution follows a two-tier full-stack pattern:

### Backend — Python + FastAPI
```
backend/
├── app/
│   ├── main.py                   # FastAPI app, middleware, startup events
│   ├── agents/                   # Azure AI Foundry MAF agents
│   ├── models/                   # Pydantic v2 request/response models
│   ├── routers/                  # FastAPI route handlers (one file per domain)
│   ├── services/                 # Business logic, orchestration
│   ├── orchestration/            # Multi-agent workflow orchestration
│   ├── infra/                    # settings.py (pydantic-settings), cosmos.py, search.py
│   └── helpers/                  # Shared utilities
├── requirements.txt
├── .env.example
└── start.ps1
```

### Frontend — React + TypeScript + Vite + Tailwind CSS
```
frontend/
├── src/
│   ├── components/               # Reusable UI components
│   │   ├── layout/               # Sidebar, Header, AppLayout
│   │   ├── workflow/             # WorkflowDiagram, WorkflowNode, WorkflowDetailPanel
│   │   └── [feature]/            # Feature-specific components
│   ├── pages/                    # Route-level pages
│   │   ├── WorkflowPage.tsx      # System workflow visualization (always present)
│   │   ├── ArchitecturePage.tsx  # Architecture diagram and ADRs
│   │   └── SettingsPage.tsx      # App configuration
│   ├── utils/                    # API clients, helpers
│   ├── types/                    # TypeScript type definitions
│   ├── App.tsx                   # Root app with sidebar navigation
│   └── main.tsx
├── package.json
└── vite.config.ts
```

### Root Run Scripts

The repository root **must** include two batch files so the app can be started by executing them:

- `run-backend.bat` — creates/activates `backend/.venv`, installs `requirements.txt`, runs Uvicorn on port 8000
- `run-frontend.bat` — runs `npm install` (if needed) and `npm run dev` in `frontend/` on port 5173

---

## Required Sidebar Navigation Groups

Every frontend application **must** include these sidebar navigation groups:

### Main Navigation (feature-specific, project-defined)
Feature pages relevant to the application domain.

### "Architecture" Group (always present)
- **Workflow** — Interactive workflow diagram showing agent/service data flow (see workflow-visualization skill)
- **Architecture** — System architecture diagram, component details, ADRs

### "Settings" Group (always present)
- **Settings** — Environment config, model selection, feature flags, API endpoints

The sidebar uses a collapsible group structure with icons from `lucide-react`.

---

## Azure Services

All cloud services must be from **Microsoft Azure**. Preferred services by category:

| Category | Service | SDK / Package |
|---|---|---|
| AI Agents / LLM | Azure AI Foundry (Responses API v2) | `azure-ai-projects` |
| Vector Search | Azure AI Search | `azure-search-documents` |
| NoSQL Database | Azure Cosmos DB | `azure-cosmos` (async) |
| Speech | Azure Speech Services | `azure-cognitiveservices-speech` |
| Document Intelligence | Azure Document Intelligence | `azure-ai-documentintelligence` |
| Content Safety | Azure AI Content Safety | `azure-ai-contentsafety` |
| Identity | Azure Identity | `azure-identity` |
| Monitoring | Azure Monitor + OpenTelemetry | `azure-monitor-opentelemetry` |
| Storage | Azure Blob Storage | `azure-storage-blob` |
| Key Vault | Azure Key Vault | `azure-keyvault-secrets` |

Always use `DefaultAzureCredential` for managed identity in production.
Use `ClientSecretCredential` only when managed identity is unavailable.

---

## Code Standards

### Python
- Python 3.11+, fully typed with `mypy`-compatible annotations
- `async`/`await` everywhere that I/O is involved
- Pydantic v2 for all data models (`model_validator`, `field_validator`)
- `pydantic-settings` for configuration (never `os.getenv` directly in business logic)
- OpenTelemetry spans on all agent calls and external service calls
- Structured JSON logging via `structlog` or `logging` with JSON formatter
- Never log PII, credentials, or raw financial data

### TypeScript / React
- Strict TypeScript (`"strict": true` in tsconfig)
- Functional components with hooks only — no class components
- Tailwind CSS for all styling — no inline styles, no CSS modules (unless the project already uses them)
- `lucide-react` for all icons
- Dark theme by default: `bg-slate-900` body, `bg-slate-800` surfaces, `border-slate-700` borders
- `text-white` or `text-slate-100` for primary text, `text-slate-400` for secondary
- Accent color: `indigo-500` / `purple-500` for primary CTAs and highlights

### API Design
- All routes prefixed with `/api/v1/`
- RESTful conventions: `GET /api/v1/portfolios`, `POST /api/v1/portfolios/{id}/rebalance`
- WebSocket routes for real-time features: `/ws/{feature}/{session_id}`
- Health check: `GET /api/health`
- OpenAPI docs at `/docs` (Swagger) and `/redoc`
- Consistent error response shape: `{"error": {"code": "...", "message": "...", "details": {...}}}`

---

## Security Requirements (Financial Services)

- **Never** hardcode credentials, API keys, or connection strings in source code
- All secrets via Azure Key Vault or environment variables (`.env` files are `.gitignore`d)
- PII detection and redaction before logging or storing conversation data
- Input validation on all API endpoints — reject unexpected fields
- Rate limiting on all public endpoints
- CORS configured to specific origins — never `allow_origins=["*"]` in production
- Audit logging for all financial data access and mutations
- Content safety checks on all user-generated text inputs

---

## Testing

- Unit tests: `pytest` with `pytest-asyncio` for async code
- Integration tests: `httpx` + `pytest` for API endpoints
- Frontend tests: `vitest` + `@testing-library/react`
- Minimum 80% coverage on business logic and agents

---

## RPI Workflow

Complex tasks use the Research-Plan-Implement-Review (RPI) workflow:

1. `/fin-research <topic>` — Start with Task Researcher
2. `/clear` then `/fin-plan` — Create implementation plan
3. `/clear` then `/fin-implement` — Execute the plan
4. `/clear` then `/fin-review` — Validate the implementation

Use the RPI workflow for any task that:
- Spans multiple files
- Involves Azure SDK integration
- Requires understanding a new financial domain pattern
- Has unclear requirements

See `.copilot-tracking/` for research, planning, and review artifacts.
