# 01 — Naming Conventions

Consistent names across C#, TypeScript, Azure resources, and agents. When in doubt, match the
accelerator templates.

---

## C# (.NET 9)

| Element | Convention | Example |
|---|---|---|
| Namespace | `FinancialServices.Api.{Area}` | `FinancialServices.Api.Analysis` |
| Type (class/record/enum/interface) | PascalCase; interfaces `I`-prefixed | `DivergenceDecomposer`, `IReconciliationService` |
| Method | PascalCase; async methods end `Async` | `DecomposeAsync`, `GetFactsAsOfAsync` |
| Parameter / local | camelCase | `issuerId`, `asOf` |
| Private field | `_camelCase` | `_options` |
| Constant | PascalCase | `MaxNotch` |
| DTO record | PascalCase, suffix by role | `ReconciliationRequest`, `DossierResponse` |
| Domain record | PascalCase, no suffix | `ProviderRating`, `RedFlag` |
| Enum members | PascalCase | `Provider.MorningstarDbrs` |

- Prefer **primary constructors** + `sealed record` for models (as in the templates).
- One agent per file; one primary type per file; file name = type name.

## Prism domain vocabulary (use these exact terms)

`Issuer`, `ProviderRating`, `RatingFactor`, `FundamentalSnapshot`, `PairDivergence`,
`BucketAttribution`, `RedFlag`, `ReconciliationDossier`, `NotchLadder`, `Provider`
(`Moodys` | `MorningstarDbrs` | `Msci`). Buckets: `Weighting` | `Input` | `MethodologyAdjustment`.
Flag codes: `STALE_INPUT` | `MISSING_COVERAGE` | `OUTLIER_PROVIDER` | `METHODOLOGY_CONFLICT`.

## Agents

`{Domain}{Function}Agent` (org standard). Prism agents:
`ReconciliationOrchestrator`, `ProviderExplainerAgent`, `FundamentalsAgent`,
`DivergenceNarratorAgent`, `RedFlagNarratorAgent`. Deterministic engines are **not** agents — they are
services in `Analysis/` (`DivergenceDecomposer`, `RedFlagEngine`).

## TypeScript / React

| Element | Convention | Example |
|---|---|---|
| Component | PascalCase file + export | `DivergenceBoard.tsx` |
| Hook | `useX` camelCase | `useReconciliation.ts` |
| Type/interface | PascalCase | `ReconciliationDossier` |
| Variable/function | camelCase | `notchGap`, `fetchIssuers` |
| Constant | UPPER_SNAKE for module consts | `NAV_GROUPS` |
| Types mirror C# DTOs | same names | `types/prism.ts` |

## API routes

- Prefix `/api/v1/`; plural nouns; kebab/lower. `GET /api/v1/issuers`,
  `POST /api/v1/reconciliations`, `GET /api/v1/reconciliations/{id}/export`.
- Health: `GET /api/health`. AG-UI agent: `POST /prism`.

## Azure resources & config

- Cosmos DB database: `prism`. Containers: `rating_reconciliations`, `audit_events` (snake_case).
- AI Search index: `prism-ratings`. Fields camelCase (`inputAsOfDate`, `contentVector`).
- Env vars: nested via double underscore. Org section `Azure__*`; Prism section `Prism__*`
  (`Prism__FredApiKey`, `Prism__Models__Orchestrator`). Never read raw env in business logic.

## Workflow-visualization node ids

kebab-case, descriptive: `reconciliation-orchestrator`, `provider-agent-msci`,
`divergence-decomposer`, `red-flag-engine`, `confirm-scope-gate`, `dossier-ready`.

## Files & tests

- Test file: `{TypeUnderTest}Tests.cs` / `{Component}.test.tsx`.
- Seed/tooling under `backend/FinancialServices.Api/tools/SeedData/`.
