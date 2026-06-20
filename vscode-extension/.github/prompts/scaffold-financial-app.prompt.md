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
Generate the complete FastAPI backend structure:

```
backend/
├── app/
│   ├── main.py                     # FastAPI app with CORS, lifespan, all routers
│   ├── agents/
│   │   └── {primary_feature}_agent.py   # Primary Azure AI Foundry agent
│   ├── models/
│   │   └── {domain}_models.py      # Core Pydantic v2 models
│   ├── routers/
│   │   ├── health.py               # GET /api/health
│   │   └── {domain}.py             # Primary domain routes
│   ├── services/
│   │   └── {domain}_service.py     # Business logic
│   └── infra/
│       ├── settings.py             # pydantic-settings with all Azure env vars
│       ├── cosmos.py               # Cosmos DB client factory
│       └── telemetry.py            # Azure Monitor + OpenTelemetry
├── requirements.txt                # All Azure SDKs pinned
├── .gitignore                      # Excludes .venv/, __pycache__/, .env
├── .env.example                    # Complete env variable template
└── start.ps1                       # Creates/activates .venv, installs deps, runs Uvicorn
```

> Install all backend dependencies into a project-local `.venv` (never globally). `start.ps1`
> must create `.venv` if missing, activate it, `pip install -r requirements.txt`, then launch Uvicorn.

### 2. Frontend Scaffold (`frontend/`)
Generate the complete React + TypeScript frontend.

**Wire up Tailwind first** (or the UI renders as unstyled raw HTML). Copy the known-good
design-system files from `templates/frontend-design-system/`: `tailwind.config.js`,
`postcss.config.js`, `index.css` (with `@tailwind` directives + component classes),
`AppLayout.tsx`, `Sidebar.tsx`. Ensure `main.tsx` imports `./index.css`. Install
`tailwindcss@^3 postcss autoprefixer clsx`. The app must render a dark `#0f1117` background
with Inter font, a left sidebar, and rounded cards — verify with `npm run dev`.


```
frontend/src/
├── components/
│   ├── layout/
│   │   ├── AppLayout.tsx           # Sidebar + main content layout
│   │   ├── Sidebar.tsx             # Navigation with Architecture + Settings groups
│   │   └── Header.tsx
│   └── workflow/
│       ├── WorkflowDiagram.tsx     # Interactive flow graph
│       ├── WorkflowNode.tsx        # Individual node component
│       ├── WorkflowDetailPanel.tsx # Right-side detail popup
│       └── workflowTypes.ts        # TypeScript types for workflow data
├── pages/
│   ├── WorkflowPage.tsx            # System workflow visualization (REQUIRED)
│   ├── ArchitecturePage.tsx        # Architecture overview (REQUIRED)
│   ├── SettingsPage.tsx            # App settings (REQUIRED)
│   └── {domain}/
│       └── {PrimaryFeature}Page.tsx  # Primary feature page
├── utils/
│   ├── apiClient.ts                # Typed fetch wrappers
│   └── formatters.ts               # Financial number/date formatters
├── types/
│   └── index.ts                    # All TypeScript interfaces
├── App.tsx                         # React Router + AppLayout
└── main.tsx
```

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
- `.gitignore` (Python + Node + Azure)
- `README.md` with quickstart instructions
- `.vscode/settings.json` and `extensions.json`
- `docker-compose.yml` for local development
- `run-backend.bat` and `run-frontend.bat` at the **repo root** (one-click run)

### 6. Root Run Scripts (required)

Create these two batch files at the repository root so the app can be started by executing them.

`run-backend.bat`:
```bat
@echo off
REM Start the FastAPI backend with a project-local .venv
cd /d "%~dp0backend"
if not exist .venv (
    python -m venv .venv
)
call .venv\Scripts\activate.bat
python -m pip install --upgrade pip
pip install -r requirements.txt
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000
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
