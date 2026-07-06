# GitHub Copilot Instructions — Financial Services AI Platform

This repository builds AI-powered solutions for **Capital Markets, Banking, and Insurance** using
**Microsoft Azure Data and AI Services**. All code, architecture, and design decisions are
Azure-first and financially regulated-industry aware.

**Stack at a glance:** a **C# / ASP.NET Core (.NET 9)** Web API service layer, AI agents built on the
**Microsoft Agent Framework (.NET)** over **Azure AI Foundry**, and a **React 18 + Vite** frontend using
**shadcn/ui**, **TanStack Query + Table**, **CopilotKit** (via a Node runtime sidecar), and **Tailwind CSS**.

---

## ⭐ Project Architecture — READ BEFORE WRITING CODE

The active project in this repository is **Prism — Corporate Bond Credit-Rating Divergence Explainer**.
Before generating or editing any code, **every agent and contributor must read and follow** the
project's architecture governance:

- **`architecturalPlan/`** — authoritative Prism architecture & standards. Start with
  [`architecturalPlan/00-core-principles.md`](../architecturalPlan/00-core-principles.md) (non-negotiables),
  then the file relevant to your change (naming `01`, folders `02`, errors `03`, agents `04`,
  config `05`, security `06`, observability `07`, data `08`, API `09`, frontend `10`, testing `11`).
  See [`architecturalPlan/README.md`](../architecturalPlan/README.md) for the index and precedence rules.
- **`implementationPlan/`** — *what* to build, in sequence (work packages 00–12).
- **`architecturalPlan/TASKS.md`** — progress tracker; update it as you complete work.
- **`PRISM-BUILD-PLAN.md`** — concept, agents, demo script.

**Never violate a core principle**, especially: (P2) the **deterministic core** — notch math and
red-flag triggers live in `Analysis/` as plain C#; the LLM only narrates and cites — and (P4) **never
say buy / sell / hold / recommend / allocate / trade / alpha / signal**. Prism reconciles data; it is
**not** a trading agent. These project rules specialize (and, for Prism-specific decisions, take
precedence over) the org standards below.

---

## Core Principle: Real Azure Services Only

The value of every solution is in **real, plugged-in Azure services** — not simulations.

- **No mock data, stubs, or fake clients** in application code. Agents, Cosmos DB, AI Search,
  Content Safety, Speech, and grounding/Bing calls must run against **real Azure resources**.
- **No silent fallbacks.** Do not degrade to canned/sample responses when a service is
  unconfigured or unreachable — fail loudly with a clear, actionable error instead.
- **Mocks/fakes are allowed only inside automated test code** (`xUnit`, `vitest`) — never in
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

### Backend — C# + ASP.NET Core Web API (.NET 9)
```
backend/
├── FinancialServices.Api/          # ASP.NET Core Web API (controllers)
│   ├── Program.cs                  # Host builder, DI, middleware, CORS, OpenAPI
│   ├── Controllers/                # API controllers (one file per domain)
│   ├── Agents/                     # Microsoft Agent Framework agents over Azure AI Foundry
│   ├── Models/                     # Request/response DTOs + domain records
│   ├── Services/                   # Business logic, orchestration
│   ├── Orchestration/              # Multi-agent workflow orchestration
│   ├── Infrastructure/             # Options, CosmosClientFactory, SearchClientFactory, Telemetry
│   ├── Helpers/                    # Shared utilities
│   └── appsettings.json            # Non-secret config (secrets via env vars / Key Vault)
├── FinancialServices.Tests/        # xUnit test project
└── FinancialServices.sln
```

### Frontend — React 18 + Vite + shadcn/ui + TanStack + CopilotKit + Tailwind CSS
```
frontend/
├── src/
│   ├── components/
│   │   ├── ui/                   # shadcn/ui primitives (button, card, table, dialog, ...)
│   │   ├── layout/               # Sidebar, Header, AppLayout
│   │   ├── workflow/             # WorkflowDiagram, WorkflowNode, WorkflowDetailPanel
│   │   └── [feature]/            # Feature-specific components
│   ├── pages/                    # Route-level pages
│   │   ├── WorkflowPage.tsx      # System workflow visualization (always present)
│   │   ├── ArchitecturePage.tsx  # Architecture diagram and ADRs
│   │   └── SettingsPage.tsx      # App configuration
│   ├── hooks/                    # TanStack Query hooks (useQuery/useMutation per domain)
│   ├── lib/                      # apiClient, queryClient, utils (cn), formatters
│   ├── types/                    # TypeScript type definitions
│   ├── App.tsx                   # CopilotKit provider + QueryClientProvider + router
│   └── main.tsx
├── components.json               # shadcn/ui config
├── package.json
├── tailwind.config.js
└── vite.config.ts
```

### CopilotKit Runtime (Node sidecar)
```
copilot-runtime/
├── server.ts                     # CopilotRuntime + OpenAIAdapter (Azure OpenAI); proxies to the C# API
├── package.json
└── .env.example
```
The frontend talks to CopilotKit through this Node sidecar (CopilotKit's runtime is Node-based).
The sidecar forwards tool/action calls to the C# `/api/v1/` endpoints and streams completions from Azure OpenAI.

### Root Run Scripts

The repository root **must** include batch files so the app can be started by executing them:

- `run-backend.bat` — runs `dotnet restore` then `dotnet run` for the ASP.NET Core API on port 8000
- `run-copilot-runtime.bat` — runs `npm install` (if needed) and starts the CopilotKit Node runtime sidecar on port 4000
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

| Category | Service | SDK / Package (NuGet) |
|---|---|---|
| AI Agents / LLM | Azure AI Foundry via Microsoft Agent Framework | `Microsoft.Agents.AI`, `Azure.AI.Agents.Persistent`, `Azure.AI.Projects` |
| Vector Search | Azure AI Search | `Azure.Search.Documents` |
| NoSQL Database | Azure Cosmos DB | `Microsoft.Azure.Cosmos` |
| Speech | Azure Speech Services | `Microsoft.CognitiveServices.Speech` |
| Document Intelligence | Azure Document Intelligence | `Azure.AI.DocumentIntelligence` |
| Content Safety | Azure AI Content Safety | `Azure.AI.ContentSafety` |
| Identity | Azure Identity | `Azure.Identity` |
| Monitoring | Azure Monitor + OpenTelemetry | `Azure.Monitor.OpenTelemetry.AspNetCore` |
| Storage | Azure Blob Storage | `Azure.Storage.Blobs` |
| Key Vault | Azure Key Vault | `Azure.Security.KeyVault.Secrets` |

Always use `DefaultAzureCredential` for managed identity in production.
Use `ClientSecretCredential` only when managed identity is unavailable.

---

## Code Standards

### C# (.NET 9)
- Nullable reference types enabled; treat warnings as errors
- `async`/`await` end-to-end with a `CancellationToken` plumbed through all I/O
- Records + `System.Text.Json` for DTOs; DataAnnotations or FluentValidation for validation
- `IOptions<T>` / options pattern for configuration (never read env vars directly in business logic)
- OpenTelemetry activities/spans on all agent calls and external service calls
- Structured logging via `ILogger<T>` with logging scopes — no string concatenation
- Never log PII, credentials, or raw financial data

### TypeScript / React
- Strict TypeScript (`"strict": true` in tsconfig)
- React 18 functional components with hooks only — no class components
- **shadcn/ui** (Radix + Tailwind) for all UI primitives — do not hand-roll buttons, dialogs, tables
- **TanStack Query** for all server state (no ad-hoc `useEffect` fetching); **TanStack Table** for all data grids
- **CopilotKit** (`@copilotkit/react-core`, `@copilotkit/react-ui`) for the AI copilot surface, wired to the Node runtime sidecar
- Tailwind CSS for all styling — no inline styles, no CSS modules (unless the project already uses them)
- `lucide-react` for all icons
- Dark theme by default using shadcn CSS variables: `bg-background`, `bg-card`, `border-border`, `text-foreground`, `text-muted-foreground`
- Accent via the shadcn `primary` token (indigo / violet)

### API Design
- All routes prefixed with `/api/v1/`
- RESTful conventions: `GET /api/v1/portfolios`, `POST /api/v1/portfolios/{id}/rebalance`
- Real-time features use SignalR hubs (`/hubs/{feature}`) or WebSockets (`/ws/{feature}/{session_id}`)
- Health check: `GET /api/health`
- OpenAPI document via built-in `AddOpenApi()` / `MapOpenApi()` at `/openapi/v1.json` (add Swashbuckle if you want a Swagger UI at `/swagger`)
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

- Unit tests: `xUnit` + `FluentAssertions`; mock with `NSubstitute` or `Moq`
- Integration tests: `WebApplicationFactory<Program>` (`Microsoft.AspNetCore.Mvc.Testing`)
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
