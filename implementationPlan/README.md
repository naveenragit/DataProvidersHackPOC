# Prism — Implementation Plan

Detailed, sequenced work packages for building **Prism — Corporate Bond Credit-Rating
Divergence Explainer**. Concept and rationale live in [../PRISM-BUILD-PLAN.md](../PRISM-BUILD-PLAN.md);
validated stack research lives in [../HACKATHON-FINDINGS.md](../HACKATHON-FINDINGS.md).

Each file below is a **self-contained work package**: purpose, dependencies, concrete file paths,
code skeletons, and acceptance criteria. Build them roughly in order — dependencies are noted.

## Build order & status

| # | Work package | Day | Depends on | Status |
|---|---|---|---|---|
| 00 | [Architecture & conventions](00-architecture-and-conventions.md) | 0 | — | ✅ |
| 01 | [Day 0 — Azure gates & scaffold](01-day0-azure-and-scaffold.md) | 0 | 00 | ◐ backend scaffold done; Azure gates pending |
| 02 | [Domain model & notch ladder](02-domain-model-and-notch-ladder.md) | 1 | 01 | ✅ |
| 03 | [Synthetic data & AI Search index](03-synthetic-data-and-search-index.md) | 1 | 02 | ✅ (30 labeled-synthetic docs; live `prism-ratings` index seeded + verified; 21 tests) |
| 04 | [Real data connectors (EDGAR/FRED/Treasury)](04-real-data-connectors.md) | 1 | 02 | ◐ EDGAR/FRED + provider abstraction done (99 tests); real Moody's/DBRS HTTP pending API specs |
| 05 | [Divergence decomposer & red-flag rules](05-divergence-decomposer-and-redflags.md) | 2 | 02, 03, 04 | ✅ (123 tests; honest residual-dominance; wiring → pkg 07/10) |
| 06 | [Foundry agents](06-foundry-agents.md) | 2 | 03, 04 | ☐ |
| 07 | [Orchestration, AG-UI & CopilotKit](07-orchestration-agui-and-copilotkit.md) | 2–3 | 05, 06 | ☐ |
| 08 | [API & persistence](08-api-and-persistence.md) | 2 | 02, 05 | ✅ (all 5 acceptance live: issuers from Search, NordStar STALE dossier persisted to Cosmos, round-trip, audit) |
| 09 | [Frontend shell & navigation](09-frontend-shell.md) | 1–3 | 01 | ✅ (Vite app scaffolded; 12 tests; build green) |
| 10 | [Prism UI & workflow visualization](10-frontend-prism-ui-and-workflow.md) | 3 | 07, 08, 09 | ✅ (verdict board + waterfall + red-flag panel render live; 07 copilot/narration deferred as honest placeholders; 36 tests) |
| 11 | [Deployment to Azure Container Apps](11-deployment-aca.md) | 4 | all backend | ☐ |
| 12 | [Demo, testing & cut lines](12-demo-testing-and-cutlines.md) | 4–5 | all | ☐ |

## The two rules that keep this demo safe

1. **Deterministic core.** Notch math, gap ordering, and every red-flag trigger are **plain C#**
   (files 02, 05). The LLM only *narrates and cites*. When a flag fires, the UI shows the rule.
2. **Never say buy / sell / hold / recommend / allocate / trade / alpha / signal.** Prism explains
   *why the data disagrees* — it is a data-quality / reconciliation tool, never a trading agent.

## How to use with the RPI workflow

Each work package can be handed to the kit's implementation flow:
`/fin-task-research` (if SDK surface is unclear) → `/fin-task-plan` → `/fin-task-implement` →
`/fin-task-review`. For well-specified packages (02, 03, 04, 05) you can implement directly.

## Automated build workflow (one command per package)

To build a package end-to-end automatically — detailed plan → implement → adversarial review →
correct → update the tracker — run:

```
/prism-build-package {NN}      e.g. /prism-build-package 02
```

The orchestrator (`Prism Build Orchestrator` agent + `prism-build-package` prompt) delegates each
phase to specialist sub-agents (`Fin Task Planner` → `Fin Task Implementor` →
`Prism Standards Adversary` + `Fin Adversary Architect/Security/Stack Critic` → `Fin Task Reviewer`),
loops on corrections until no Critical/High findings remain, then appends an entry to the
**Implementation Log** in [`../architecturalPlan/TASKS.md`](../architecturalPlan/TASKS.md). Artifacts
land under `.copilot-tracking/{plans,changes,reviews}/`. Run packages in dependency order.
