---
mode: agent
description: "Scaffold a new financial services application with full-stack structure, Azure services, and workflow visualization"
---

# Scaffold Financial Services Application

Create a complete project scaffold for a new financial services AI application.

## Application Details

**Application Name:** $APP_NAME  
**Financial Domain:** $DOMAIN (capital-markets | banking | insurance)  
**Primary Feature:** $PRIMARY_FEATURE  
**Azure Region:** $AZURE_REGION (default: eastus)

## What to Create

### 1. Backend Scaffold (`backend/`)
Generate the complete C# / ASP.NET Core (.NET 9) Web API solution:

```
backend/
├── FinancialServices.Api/
│   ├── Program.cs                       # Host builder, DI, CORS, OpenAPI, health checks
│   ├── Controllers/
│   │   ├── HealthController.cs          # GET /api/health
│   │   └── {Domain}Controller.cs        # Primary domain routes
│   ├── Agents/
│   │   └── {PrimaryFeature}Agent.cs     # Primary Microsoft Agent Framework agent
│   ├── Models/
│   │   └── {Domain}Models.cs            # Core DTO / domain records
│   ├── Services/
│   │   └── {Domain}Service.cs           # Business logic
│   ├── Infrastructure/
│   │   ├── AzureOptions.cs              # Strongly-typed options for all Azure settings
│   │   ├── CosmosClientFactory.cs       # Cosmos DB client registration
│   │   └── Telemetry.cs                 # Azure Monitor + OpenTelemetry
│   ├── appsettings.json                 # Non-secret config
│   └── FinancialServices.Api.csproj     # net9.0, Nullable, TreatWarningsAsErrors
├── FinancialServices.Tests/             # xUnit test project
├── FinancialServices.sln
├── .gitignore                           # Excludes bin/, obj/, .env
└── .env.example                         # Complete env variable template
```

> Enable `Nullable` and `TreatWarningsAsErrors`. Add Azure packages with `dotnet add package`.
> `run-backend.bat` restores and runs the API on port 8000.

### 2. Frontend Scaffold (`frontend/`)
Generate the complete React 18 + Vite + TypeScript frontend.

**Wire up Tailwind + shadcn/ui first** (or the UI renders as unstyled raw HTML). Copy the
known-good design-system files from `templates/frontend-design-system/`: `tailwind.config.js`,
`postcss.config.js`, `index.css` (with `@tailwind` directives + shadcn CSS variables), `App.tsx`
(CopilotKit + QueryClientProvider), `AppLayout.tsx`, `Sidebar.tsx`. Ensure `main.tsx` imports
`./index.css`. Install `react-router-dom lucide-react class-variance-authority clsx
tailwind-merge @tanstack/react-query @tanstack/react-table @copilotkit/react-core
@copilotkit/react-ui` plus `-D tailwindcss@^3 postcss autoprefixer tailwindcss-animate`, then run
`npx shadcn@latest init`. The app must render a dark background with Inter font, a left
sidebar, and rounded shadcn cards — verify with `npm run dev`.

```
frontend/src/
├── components/
│   ├── ui/                          # shadcn/ui primitives (button, card, table, dialog, ...)
│   ├── layout/
│   │   ├── AppLayout.tsx            # Sidebar + main content layout
│   │   ├── Sidebar.tsx              # Navigation with Architecture + Settings groups
│   │   └── TopBar.tsx
│   └── workflow/
│       ├── WorkflowDiagram.tsx      # Interactive flow graph
│       ├── WorkflowNode.tsx         # Individual node component
│       └── WorkflowDetailPanel.tsx  # Right-side detail popup
├── pages/
│   ├── WorkflowPage.tsx             # System workflow visualization (REQUIRED)
│   ├── ArchitecturePage.tsx         # Architecture overview (REQUIRED)
│   ├── SettingsPage.tsx             # App settings (REQUIRED)
│   └── {domain}/
│       └── {PrimaryFeature}Page.tsx  # Primary feature page
├── hooks/                           # TanStack Query hooks (one file per domain)
├── lib/
│   ├── apiClient.ts                 # Typed fetch wrappers
│   ├── queryClient.ts               # TanStack Query client
│   ├── utils.ts                     # shadcn cn() helper
│   └── formatters.ts                # Financial number/date formatters
├── types/
│   └── index.ts                     # All TypeScript interfaces
├── App.tsx                          # CopilotKit + QueryClientProvider + Router + AppLayout
└── main.tsx
```

### 2b. CopilotKit Runtime (`copilot-runtime/`)
Generate a small Node runtime sidecar that the frontend's `CopilotKit runtimeUrl` targets. It
hosts `CopilotRuntime` with an Azure OpenAI adapter and forwards actions to the C# `/api/v1/`
endpoints. Runs on port 4000.

### 3. Workflow Data for This App
Define the initial workflow visualization data in `frontend/src/data/workflowData.ts`:
- Map out all agents, services, and data stores this app will use
- Define connections between components
- Pre-populate detail panel content based on the architecture

### 4. Infrastructure (`infra/`)
Create Bicep templates:
```
infra/
├── main.bicep
├── modules/
│   ├── containerApp.bicep
│   ├── cosmosDb.bicep
│   └── aiFoundry.bicep
└── parameters/
    └── dev.bicepparam
```

### 5. Configuration Files
- `.gitignore` (.NET + Node + Azure)
- `README.md` with quickstart instructions
- `.vscode/settings.json` and `extensions.json`
- `docker-compose.yml` for local development (api, copilot-runtime, frontend)
- `run-backend.bat`, `run-copilot-runtime.bat`, and `run-frontend.bat` at the **repo root** (one-click run)

### 6. Root Run Scripts (required)

Create these batch files at the repository root so the app can be started by executing them.

`run-backend.bat`:
```bat
@echo off
REM Start the ASP.NET Core Web API
cd /d "%~dp0backend"
dotnet restore
dotnet run --project FinancialServices.Api --urls http://0.0.0.0:8000
```

`run-copilot-runtime.bat`:
```bat
@echo off
REM Start the CopilotKit Node runtime sidecar
cd /d "%~dp0copilot-runtime"
if not exist node_modules (
    npm install
)
npm run dev
```

`run-frontend.bat`:
```bat
@echo off
REM Start the React + Vite frontend
cd /d "%~dp0frontend"
if not exist node_modules (
    npm install
)
npm run dev
```

## Workflow Visualization Requirements

The initial Workflow page must show the complete data flow for the primary feature:
1. **Start** node (entry point / trigger)
2. **Pre-processing** nodes (data fetching, document loading)  
3. **AI Agent** nodes (one per Azure AI Foundry agent) — purple
4. **Human Gate** nodes (approval checkpoints) — amber
5. **Data Store** nodes (Cosmos DB, AI Search) — teal
6. **Outcome** nodes (final outputs) — green

Each node must have populated detail panel content.

## Scaffold the application now.
