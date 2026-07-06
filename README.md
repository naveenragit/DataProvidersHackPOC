# Financial Services Copilot Kit

Engineering components for building AI-powered financial services applications with
**GitHub Copilot**, **Microsoft Azure**, and the **RPI workflow** (Research → Plan → Implement → Review).

Designed for teams building solutions in **Capital Markets, Banking, and Insurance** on Azure.

---

## What Is This?

A drop-in collection of GitHub Copilot customization files that makes your team's AI-assisted
development **repeatable**, **standards-aligned**, and **scalable**:

| Component | What It Does |
|---|---|
| **Instructions** | Apply coding standards automatically to every file you edit |
| **Agents** | Specialized AI agents for Research, Planning, Implementation, and Review, plus an adversarial architecture red-team |
| **Prompts** | Repeatable workflow entry points (one command per phase) |
| **Skills** | Reusable domain knowledge for Azure services and workflow visualization |
| **Templates** | Ready-to-use shadcn/ui frontend design system, workflow visualization, and a C# API starter |

---

## Documentation

Start here to understand and grow the kit:

| Guide | Read it to... |
|---|---|
| [Understanding the Kit](internal/UNDERSTANDING-THE-KIT.md) | Internal reference: concepts (prompts/instructions/agents/skills), the RPI playbook, architecture decision matrices, and the demo talk track. Not packaged into the extension. |
| [Extending the Kit](docs/EXTENDING-THE-KIT.md) | Get concrete ideas to push the kit further and unlock your team's creativity |

---

## Structure

```
.github/
├── copilot-instructions.md                    # Root instructions — always active
├── agents/
│   ├── fin-task-researcher.agent.md           # Research phase agent
│   ├── fin-task-planner.agent.md              # Planning phase agent
│   ├── fin-task-implementor.agent.md          # Implementation phase agent
│   └── fin-task-reviewer.agent.md             # Review phase agent
├── instructions/
│   ├── coding-standards/
│   │   ├── csharp-backend.instructions.md      # ASP.NET Core + Azure C# standards
│   │   ├── react-frontend.instructions.md      # React 18 + shadcn + TanStack + CopilotKit standards
│   │   └── azure-services.instructions.md      # Azure .NET SDK integration patterns
│   └── financial-domain/
│       └── financial-domain.instructions.md   # Capital Markets, Banking, Insurance knowledge
├── prompts/
│   ├── fin-task-research.prompt.md            # /fin-task-research — start research
│   ├── fin-task-plan.prompt.md                # /fin-task-plan — create plan
│   ├── fin-task-implement.prompt.md           # /fin-task-implement — run implementation
│   ├── fin-task-review.prompt.md              # /fin-task-review — validate output
│   └── scaffold-financial-app.prompt.md       # Scaffold a new app from scratch
└── skills/
    ├── azure-financial-services/SKILL.md      # Azure patterns for finance
    └── workflow-visualization/SKILL.md        # Workflow page implementation guide

templates/
├── frontend-design-system/                    # shadcn/ui dark theme + providers (React 18)
├── workflow-visualization/
│   ├── workflowTypes.ts                       # TypeScript type definitions
│   ├── workflowData.ts                        # Sample workflow data (Meeting Intelligence)
│   ├── WorkflowNode.tsx                       # Node component
│   ├── WorkflowDetailPanel.tsx                # Right-side detail popup
│   ├── WorkflowDiagram.tsx                    # Main graph renderer
│   └── WorkflowPage.tsx                       # Full page component
└── csharp-api/                                # ASP.NET Core (.NET 9) API + CopilotKit Node sidecar
```

---

## Quick Start

### 1. Copy to Your Repository

Copy the `.github/` folder into the root of your project repository:

```
your-repo/
├── .github/
│   ├── copilot-instructions.md
│   ├── agents/
│   ├── instructions/
│   ├── prompts/
│   └── skills/
└── ...your code...
```

### 2. Start a New Feature with the RPI Workflow

```
1. In GitHub Copilot Chat:
   /fin-task-research <describe your feature>

2. After research completes — /clear — then:
   /fin-task-plan

3. After plan is created — /clear — then:
   /fin-task-implement

4. After implementation — /clear — then:
   /fin-task-review
```

### 3. Scaffold a Brand-New App

```
In GitHub Copilot Chat:
/scaffold-financial-app

Fill in the prompts:
- APP_NAME: my-portfolio-analyzer
- DOMAIN: capital-markets
- PRIMARY_FEATURE: portfolio-rebalancing
- AZURE_REGION: eastus
```

### 4. Add the Workflow Visualization Page

Copy files from `templates/workflow-visualization/` into your frontend:

```
frontend/src/
├── types/workflowTypes.ts           ← copy from templates/
├── data/workflowData.ts             ← copy and customize for your app
├── components/workflow/
│   ├── WorkflowNode.tsx             ← copy from templates/
│   ├── WorkflowDetailPanel.tsx      ← copy from templates/
│   └── WorkflowDiagram.tsx          ← copy from templates/
└── pages/WorkflowPage.tsx           ← copy from templates/
```

Then register the route in `App.tsx`:
```tsx
<Route path="/workflow" element={<WorkflowPage />} />
```

And add to the sidebar `Architecture` group:
```tsx
{ to: '/workflow', icon: GitBranch, label: 'Workflow' }
```

---

## The RPI Workflow

Based on the [HVE-Core RPI methodology](https://github.com/microsoft/hve-core/blob/main/docs/rpi/README.md):

```
Uncertainty → Knowledge → Strategy → Working Code → Validated Code
    🔬             📋            ⚡             ✅
  Research       Plan       Implement        Review
```

Each phase uses a dedicated custom agent. **Always `/clear` between phases** —
each agent needs clean context to work optimally.

Artifacts are stored in `.copilot-tracking/`:
```
.copilot-tracking/
├── research/YYYY-MM-DD/             # Research documents
├── planning/YYYY-MM-DD/             # Plan documents
├── changes/YYYY-MM-DD/              # Changes logs
└── reviews/YYYY-MM-DD/              # Review reports
```

---

## Architecture Pattern

Every application follows this stack:

| Layer | Technology | Port |
|---|---|---|
| Frontend | React 18 + Vite + shadcn/ui + TanStack Query/Table + CopilotKit + Tailwind CSS | 5173 |
| Copilot runtime | CopilotKit Node sidecar (Azure OpenAI adapter) | 4000 |
| Backend | C# / ASP.NET Core Web API (.NET 9) | 8000 |
| Agents | Microsoft Agent Framework (.NET) over Azure AI Foundry | — |
| Database | Azure Cosmos DB (.NET SDK) | — |
| Search | Azure AI Search (hybrid vector+keyword) | — |
| Speech | Azure Speech Services | — |
| Identity | Azure Identity (DefaultAzureCredential) | — |

### Required Sidebar Groups (every app)

```
Navigation
├── [Feature pages specific to the app]
│
├── Architecture
│   ├── Workflow          ← Interactive agent/service flow diagram
│   └── Architecture      ← System architecture overview
│
└── Settings
    └── Settings          ← Azure config, model selection, feature flags
```

---

## Azure Services Reference

| Use Case | Azure Service | SDK (NuGet) |
|---|---|---|
| AI Agents / LLM | Azure AI Foundry via Microsoft Agent Framework | `Microsoft.Agents.AI`, `Azure.AI.Projects` |
| Vector + Keyword Search | Azure AI Search | `Azure.Search.Documents` |
| NoSQL Database | Azure Cosmos DB | `Microsoft.Azure.Cosmos` |
| Real-time Speech | Azure Speech Services | `Microsoft.CognitiveServices.Speech` |
| Document Processing | Azure Document Intelligence | `Azure.AI.DocumentIntelligence` |
| Content Moderation | Azure AI Content Safety | `Azure.AI.ContentSafety` |
| Observability | Azure Monitor + OpenTelemetry | `Azure.Monitor.OpenTelemetry.AspNetCore` |
| Authentication | Azure Identity | `Azure.Identity` |

---

## Financial Domain Coverage

- **Capital Markets** — Portfolio management, equity research, trading, risk (VaR, duration), MAF orchestration
- **Banking** — KYC/AML, credit risk, fraud detection, loan origination
- **Insurance** — Underwriting, claims processing, actuarial analysis, Solvency II

All agents include:
- Rationale and source attribution in recommendations
- PII detection and redaction on meeting transcripts
- Audit logging for all financial data mutations
- Human-in-the-loop gates before consequential actions

---

## Contributing

When adding new domain patterns or Azure service integrations:
1. Add examples to the relevant instruction file under `.github/instructions/`
2. Update the Azure Financial Services skill at `.github/skills/azure-financial-services/SKILL.md`
3. Add a new workflow tab to `workflowData.ts` if the feature has a distinct data flow
