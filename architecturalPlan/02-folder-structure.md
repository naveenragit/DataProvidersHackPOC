# 02 — Folder Structure

Canonical layout. New files go in the right place; do not invent parallel structures.

```
DataProvidersHackPOC/
├── backend/
│   ├── FinancialServices.Api/
│   │   ├── Program.cs                      # host, DI, middleware, CORS, AG-UI map
│   │   ├── Controllers/                    # REST — one per resource
│   │   │   ├── HealthController.cs
│   │   │   ├── IssuersController.cs
│   │   │   └── ReconciliationsController.cs
│   │   ├── Agents/                         # LLM agents (Microsoft Agent Framework) — one per file
│   │   │   ├── ReconciliationOrchestrator.cs
│   │   │   ├── ProviderExplainerAgent.cs
│   │   │   ├── FundamentalsAgent.cs
│   │   │   ├── DivergenceNarratorAgent.cs
│   │   │   └── RedFlagNarratorAgent.cs
│   │   ├── Analysis/                        # ★ DETERMINISTIC — no LLM, no network
│   │   │   ├── NotchLadder.cs
│   │   │   ├── DivergenceDecomposer.cs
│   │   │   └── RedFlagEngine.cs
│   │   ├── Connectors/                      # real external data (as-of filtered)
│   │   │   ├── EdgarClient.cs
│   │   │   ├── FredClient.cs
│   │   │   └── TreasuryClient.cs
│   │   ├── Orchestration/                   # AG-UI orchestrator wiring + function tools + gate
│   │   │   └── PrismOrchestrator.cs
│   │   ├── Services/                        # business logic / persistence access
│   │   │   ├── ReconciliationService.cs
│   │   │   ├── SearchCorpus.cs
│   │   │   └── AuditService.cs
│   │   ├── Models/                          # records: domain + DTOs (separate)
│   │   │   ├── PrismModels.cs               # domain records
│   │   │   └── PrismDtos.cs                 # request/response DTOs
│   │   ├── Infrastructure/                  # options, DI extensions, telemetry
│   │   │   ├── AzureOptions.cs
│   │   │   ├── PrismOptions.cs
│   │   │   └── ServiceCollectionExtensions.cs
│   │   ├── tools/SeedData/                  # console app: author + upload corpus to AI Search
│   │   ├── appsettings.json
│   │   └── FinancialServices.Api.csproj
│   ├── FinancialServices.Tests/            # xUnit
│   └── FinancialServices.sln
├── copilot-runtime/                        # Node CopilotKit sidecar (server.ts)
├── frontend/
│   └── src/
│       ├── App.tsx  main.tsx  index.css
│       ├── components/
│       │   ├── ui/                          # shadcn primitives
│       │   ├── layout/                      # AppLayout, Sidebar, TopBar
│       │   ├── prism/                        # DivergenceBoard, DecompositionWaterfall, RuleModal, …
│       │   └── workflow/                     # from templates/workflow-visualization
│       ├── pages/                            # IssuersPage, ReconciliationPage, WorkflowPage, ArchitecturePage, SettingsPage
│       ├── hooks/                            # useIssuers, useReconciliation
│       ├── lib/                              # apiClient, queryClient, utils(cn)
│       └── types/                            # prism.ts (mirrors C# DTOs)
├── infra/                                    # Bicep / azd (package 11)
├── architecturalPlan/                        # ← this folder (standards)
├── implementationPlan/                       # work packages
├── run-backend.bat  run-copilot-runtime.bat  run-frontend.bat
└── .env.example
```

## Placement rules

- **Deterministic logic → `Analysis/` only.** If it touches an LLM or the network, it does not belong
  there. This physical separation enforces core principle P2.
- **Real external data → `Connectors/`.** Each connector enforces as-of filtering.
- **Agents (`Agents/`) narrate/retrieve; engines (`Analysis/`) decide.** Never blur them.
- Domain records and DTOs are separate files; DTOs never leak Cosmos/Search concerns.
- Frontend feature components go under `components/prism/`; never hand-roll UI primitives (use `ui/`).
- One responsibility per file; file name matches the primary export.
