<!-- markdownlint-disable-file -->
# Implementation Plan: Package 09 — Frontend Shell & Navigation

**Package doc:** `implementationPlan/09-frontend-shell.md`
**Governance:** `architecturalPlan/` 00 (P1 no mock data / fail-loud; P4 no trading language; P8 reuse `templates/`), 01 (naming — components PascalCase, hooks `useX`, TS types mirror C# DTOs), 02 (folders), 09 (API contract the TS types mirror), 10 (frontend architecture — shadcn only, TanStack for **all** server state, dark tokens, lucide icons), 11 (vitest)
**Financial Domain:** Capital Markets — corporate-bond credit-rating **reconciliation** (NOT a trading agent — P4)
**Azure Services:** none at build time (frontend only). Runtime talks to the C# API (`/api/v1` → :8000) and the CopilotKit sidecar (`/copilotkit` → :4000), both via Vite proxy.
**Repo state at plan time:** backend green (**123 tests**); **no `frontend/` folder exists yet** — this package scaffolds it from `templates/frontend-design-system` + `templates/workflow-visualization`. Provider enum is `Moodys | MorningstarDbrs | Msci` (code authoritative post real-data pivot; `01`/template comments still say "Bloomberg" / "Financial AI" — stale, rebrand to Prism).

**Acceptance gate (automatable in a sub-agent):** `npm install` clean → `npm run build` (**tsc `--noEmit`/`-b` + `vite build`**) GREEN under strict TS → `npm run test` (vitest) GREEN. `npm run dev` / visual verification is **out of scope for the gate** (not automatable headless). `npm run lint` only if ESLint is configured (see Open Decision 9 — recommended **deferred**).

---

## Context the implementor must not re-derive

### Template → destination map (copy VERBATIM unless a task says "edit"/"author")

| Template file (`templates/frontend-design-system/`) | Destination (`frontend/`) | Action |
|---|---|---|
| `index.html` | `index.html` | copy, then **edit** `<title>` + tab text → Prism |
| `vite.config.ts` | `vite.config.ts` | copy verbatim (alias `@`→`./src`, `/api`→:8000, `/copilotkit`→:4000 already correct) |
| `tailwind.config.js` | `tailwind.config.js` | copy verbatim |
| `postcss.config.js` | `postcss.config.js` | copy verbatim |
| `components.json` | `components.json` | copy verbatim |
| `index.css` | `src/index.css` | copy verbatim (shadcn dark tokens) |
| `main.tsx` | `src/main.tsx` | copy verbatim (imports `./index.css`) |
| `App.tsx` | `src/App.tsx` | **author** (routes + rebrand — Task 4.1) |
| `lib/utils.ts` | `src/lib/utils.ts` | copy verbatim (`cn`) |
| `lib/queryClient.ts` | `src/lib/queryClient.ts` | copy verbatim |
| `AppLayout.tsx` | `src/components/layout/AppLayout.tsx` | copy verbatim |
| `Sidebar.tsx` | `src/components/layout/Sidebar.tsx` | **edit** (NAV_GROUPS + brand — Task 4.2) |
| `TopBar.tsx` | `src/components/layout/TopBar.tsx` | **edit** (TITLES + default — Task 4.3) |
| `PageHeader.tsx` | `src/components/ui/PageHeader.tsx` | copy verbatim |
| `StatCard.tsx` | `src/components/ui/StatCard.tsx` | copy verbatim |
| `DashboardPage.tsx` | — | **DO NOT COPY** (off-domain + P4 vocab "recommendation"/"rebalanced"; IssuersPage replaces it) |
| `ExampleFormPage.tsx` | — | reference only (shows the `apiPost` + shadcn form pattern) — **do not copy** |

### Workflow-visualization wiring (RESOLVED — Option A: co-locate)

The template has an internal path inconsistency: `WorkflowPage.tsx` imports `../data/workflowData` + `../types/workflowTypes`, but `WorkflowDiagram/Node/DetailPanel` + `workflowData` all import `./workflowTypes` (co-located). **Resolution: place ALL five non-page files together under `src/components/workflow/`** so every relative `./workflowTypes` / `./WorkflowNode` / `./WorkflowDetailPanel` stays valid, and edit only `WorkflowPage.tsx`'s three imports to `@/`-alias paths. No `src/data/` or `src/types/workflowTypes.ts` is created (`src/types/` holds only `prism.ts`).

| Template file (`templates/workflow-visualization/`) | Destination (`frontend/src/`) | Action |
|---|---|---|
| `workflowTypes.ts` | `components/workflow/workflowTypes.ts` | copy verbatim |
| `WorkflowNode.tsx` | `components/workflow/WorkflowNode.tsx` | copy verbatim |
| `WorkflowDetailPanel.tsx` | `components/workflow/WorkflowDetailPanel.tsx` | copy verbatim |
| `WorkflowDiagram.tsx` | `components/workflow/WorkflowDiagram.tsx` | copy verbatim |
| `workflowData.ts` | `components/workflow/workflowData.ts` | **REWRITE** to a minimal Prism dataset (Task 8.1 — the sample "meeting-intelligence" data contains P4 vocab "recommendation" and is off-domain) |
| `WorkflowPage.tsx` | `pages/WorkflowPage.tsx` | copy, then **edit 3 imports** → `@/components/workflow/{WorkflowDiagram,workflowData,workflowTypes}` (Task 8.2) |

> The workflow template intentionally uses raw `slate-*`/`text-white` classes (self-contained diagram styling), not shadcn tokens. That is **accepted verbatim** for pkg 09; do not refactor to tokens (out of scope; pkg 10 owns enrichment).

### DTO contract the TS mirrors (INFERRED — see Open Decision 8)

`Models/PrismDtos.cs` is authored in **pkg 08 (not yet built)**. Until then, `types/prism.ts` mirrors the authoritative shape from `architecturalPlan/09` (`DossierResponse`, `ReconciliationRequest`) and derives the `*Dto` sub-shapes from the domain records in `backend/FinancialServices.Api/Models/PrismModels.cs`. Wire format is **camelCase** (`System.Text.Json`, per arch-09). When pkg 08 lands, re-sync names/shapes in the same change (contract-sync rule, arch-09).

- `Provider` = `'Moodys' | 'MorningstarDbrs' | 'Msci'` (mirror the C# enum member names verbatim; serializer emits enum **names**).
- Buckets = `'Weighting' | 'Input' | 'MethodologyAdjustment'`; flag codes = `'STALE_INPUT' | 'MISSING_COVERAGE' | 'OUTLIER_PROVIDER' | 'METHODOLOGY_CONFLICT'`; severities = `'high' | 'medium' | 'low'`.
- `DateTimeOffset` → TS `string` (ISO 8601). `decimal`/`double`/`int` → TS `number`.

### Hard rules for this package

- **P1 / arch-10:** every hook calls the **real** `/api/v1`. Backend endpoints arrive in **pkg 08**, so until then queries **error honestly** — render explicit loading + error + empty states, **never fabricated placeholder rows**. No mock data in runtime code (mocks live only in vitest tests).
- **P4:** no buy/sell/hold/recommend/allocate/trade/alpha/signal anywhere in copy, comments, node labels, or settings. Prism vocabulary = reconciliation, divergence, provenance, as-of, coverage, notch gap.
- **arch-10 state:** all server state via TanStack Query (no `useEffect` fetching); all grids via TanStack Table. `localStorage` (Settings) is local UI state — `useEffect`/`useState` there is fine.
- **shadcn only** for primitives (hand-authored — Phase 3); Tailwind + shadcn tokens; dark theme; `lucide-react` icons only; no inline styles, no CSS modules.

---

## Phase 0 — Scaffold, config & dependency pinning (REPRODUCIBLE)

> Backend Phases 1–4 of the standard planner taxonomy are **N/A** (frontend-only package). Phases are renumbered for a scaffold package; Phase 8 (Workflow) and Phase 11 (Tests) remain REQUIRED.

- [ ] **Task 0.1** Create `frontend/package.json` with **all versions PINNED** (exact, no `^`/`~` where feasible; carets only where noted for peer-safe minors). Scripts: `dev`, `build`, `preview`, `typecheck`, `test`, `test:watch`.
  - **Runtime deps (pinned):** `react@18.3.1`, `react-dom@18.3.1`, `react-router-dom@6.26.2`, `@tanstack/react-query@5.59.15`, `@tanstack/react-table@8.20.5`, `@copilotkit/react-core@1.4.4`, `@copilotkit/react-ui@1.4.4`, `lucide-react@0.451.0`, `recharts@2.12.7`, `class-variance-authority@0.7.0`, `clsx@2.1.1`, `tailwind-merge@2.5.4`, `tailwindcss-animate@1.0.7`, `@radix-ui/react-slot@1.1.0`, `@radix-ui/react-label@2.1.0`, `@radix-ui/react-dialog@1.1.2`, `@radix-ui/react-tabs@1.1.1`, `@radix-ui/react-tooltip@1.1.3`, `@radix-ui/react-select@2.1.2`.
  - **Dev deps (pinned):** `vite@5.4.9`, `@vitejs/plugin-react@4.3.2`, `typescript@5.6.3`, `tailwindcss@3.4.14`, `postcss@8.4.47`, `autoprefixer@10.4.20`, `@types/react@18.3.11`, `@types/react-dom@18.3.1`, `@types/node@22.7.5`, `vitest@2.1.3`, `jsdom@25.0.1`, `@testing-library/react@16.0.1`, `@testing-library/jest-dom@6.5.0`, `@testing-library/user-event@14.5.2`.
  - **Scripts:** `"build": "tsc --noEmit && vite build"`, `"typecheck": "tsc --noEmit"`, `"test": "vitest run"`, `"test:watch": "vitest"`, `"dev": "vite"`, `"preview": "vite preview"`.
  - **Acceptance:** `npm install` completes with no peer-dependency ERROR (warnings OK); lockfile written. (Version churn risk — Open Decision 1.)
- [ ] **Task 0.2** Create `frontend/tsconfig.json` (strict app config).
  - `"strict": true`, `"noUnusedLocals": true`, `"noUnusedParameters": true`, `"noFallthroughCasesInSwitch": true`, `"module": "ESNext"`, `"moduleResolution": "bundler"`, `"jsx": "react-jsx"`, `"target": "ES2020"`, `"lib": ["ES2020","DOM","DOM.Iterable"]`, `"types": ["vitest/globals","@testing-library/jest-dom","node"]`, `"baseUrl": "."`, `"paths": { "@/*": ["./src/*"] }`, `"noEmit": true`. `"include": ["src"]`, `"references": [{ "path": "./tsconfig.node.json" }]`.
  - **Acceptance:** `npx tsc --noEmit` resolves `@/` imports (no `TS2307`).
- [ ] **Task 0.3** Create `frontend/tsconfig.node.json` for Vite config typing (`composite: true`, `moduleResolution: "bundler"`, `include: ["vite.config.ts","vitest.config.ts"]`).
  - **Acceptance:** referenced build has no error on `vite.config.ts` / `vitest.config.ts`.
- [ ] **Task 0.4** Copy `vite.config.ts`, `tailwind.config.js`, `postcss.config.js`, `components.json` from the template to `frontend/` **verbatim**.
  - **Acceptance:** files present; `vite.config.ts` proxies `/api`→:8000 and `/copilotkit`→:4000 (unchanged from template).
- [ ] **Task 0.5** Copy `index.html` to `frontend/index.html`; **edit** `<title>` → `Prism · Rating Reconciliation` and the comment header. (No `class="dark"` change — keep it.)
  - **Acceptance:** `<html lang="en" class="dark">` retained; title reads Prism.
- [ ] **Task 0.6** Create `frontend/.gitignore` (`node_modules/`, `dist/`, `dist-ssr/`, `.vite/`, `coverage/`, `*.local`, `.env`, `.env.*`, `!.env.example`, editor dirs).
  - **Acceptance:** `node_modules`/`dist` ignored.
- [ ] **Task 0.7** Create `frontend/vitest.config.ts` (separate from `vite.config.ts` to keep the template file pristine — Open Decision 7). Import `defineConfig` from `vitest/config`; re-declare `@`→`./src` alias; `test: { environment: 'jsdom', globals: true, setupFiles: './src/test/setup.ts', css: false }`.
  - **Acceptance:** `npx vitest run` boots with jsdom (no "environment not found").
- [ ] **Task 0.8** Create `frontend/src/test/setup.ts` → `import '@testing-library/jest-dom'`.
  - **Acceptance:** matchers like `toBeInTheDocument()` typecheck in tests.

## Phase 5 — Data layer (types, apiClient, hooks) — arch-09/10

- [ ] **Task 5.1** Author `frontend/src/types/prism.ts` mirroring the pkg-09 DTO contract (camelCase; INFERRED per Context — Open Decision 8). Export: `Provider`, `Bucket`, `RedFlagCode`, `Severity` (union types); interfaces `IssuerListItem`, `ProviderVerdictDto`, `BucketAttributionDto`, `PairDivergenceDto`, `RedFlagDto`, `DossierResponse`, `ReconciliationRequest`; and the standard error shape `ApiErrorBody = { error: { code: string; message: string; details?: unknown } }`.
  - `IssuerListItem { issuerId; legalName; ticker; cik; sector; sampleBondIsin; coverage?: Provider[] }`
  - `ProviderVerdictDto { provider: Provider; letter; notch; asOfDate; inputAsOfDate; methodologyDocId }`
  - `BucketAttributionDto { bucket: Bucket; notches; explanation; evidenceRefs: string[] }`
  - `PairDivergenceDto { a: Provider; b: Provider; notchGap; attribution: BucketAttributionDto[] }`
  - `RedFlagDto { code: RedFlagCode; severity: Severity; rule; narrative; evidenceRefs: string[] }`
  - `DossierResponse { id; issuerId; asOf; verdicts: ProviderVerdictDto[]; divergences: PairDivergenceDto[]; flags: RedFlagDto[]; consensusSummary; confidence }`
  - `ReconciliationRequest { issuerId; asOf; providers?: Provider[] }`
  - **Acceptance:** compiles under strict TS; union literals match the C# enum/code strings verbatim.
- [ ] **Task 5.2** Author `frontend/src/lib/apiClient.ts` — typed `fetch` wrapper on base `/api/v1`. Export `class ApiError extends Error { code: string; status: number; details?: unknown }`; `apiGet<T>(path): Promise<T>` and `apiPost<T>(path, body): Promise<T>`. On non-2xx: parse the standard `{ error: { code, message, details } }` shape (fallback to `{ code: 'UNKNOWN', message: statusText }` if body isn't JSON) and **throw `ApiError`** (never return fabricated data — P1). `Content-Type: application/json`; `JSON.stringify` body.
  - **Acceptance:** referenced by hooks; unit-tested in Task 11.2; no `any` leaks (generics typed).
- [ ] **Task 5.3** Author `frontend/src/hooks/useIssuers.ts` → `export function useIssuers()` = `useQuery({ queryKey: ['issuers'], queryFn: () => apiGet<IssuerListItem[]>('/issuers') })`.
  - **Acceptance:** returns `UseQueryResult<IssuerListItem[]>`; no `useEffect`; query key is the stable array `['issuers']` (arch-10).
- [ ] **Task 5.4** Author `frontend/src/hooks/useReconciliation.ts` → `useReconciliationRun()` = `useMutation({ mutationFn: (req: ReconciliationRequest) => apiPost<DossierResponse>('/reconciliations', req) })`; and `useDossier(id?: string)` = `useQuery({ queryKey: ['reconciliation', id], queryFn: () => apiGet<DossierResponse>(\`/reconciliations/\${id}\`), enabled: !!id })`.
  - **Acceptance:** typed against `DossierResponse`; `useDossier` disabled when `id` is undefined; keys `['reconciliation', id]` (arch-10). Full run/stream UI is pkg 10 — this is the fallback-REST wiring only.

## Phase 3 — shadcn UI primitives (HAND-AUTHORED — deterministic, no CLI)

> Recommend hand-authoring standard shadcn (new-york style, matches `components.json`) over `npx shadcn add` to avoid CLI/network flakiness (Open Decision 2). Author exactly the 10 primitives the shell + Settings need. Each is a `.tsx` under `frontend/src/components/ui/` using `cn` from `@/lib/utils`.

- [ ] **Task 3.1** `components/ui/button.tsx` — shadcn Button (`cva` variants: default/secondary/destructive/outline/ghost/link; sizes sm/default/lg/icon; `@radix-ui/react-slot` `asChild`). Export `Button`, `buttonVariants`.
  - **Acceptance:** `import { Button } from '@/components/ui/button'` typechecks; `variant`/`size` props typed.
- [ ] **Task 3.2** `components/ui/card.tsx` — `Card`, `CardHeader`, `CardTitle`, `CardDescription`, `CardContent`, `CardFooter` (div wrappers, `bg-card`/`border-border` tokens).
  - **Acceptance:** used by pages (Task 7.x) with no missing exports.
- [ ] **Task 3.3** `components/ui/badge.tsx` — `Badge` + `badgeVariants` (default/secondary/destructive/outline). Consumed by `TopBar` (already imports `@/components/ui/badge`).
  - **Acceptance:** `TopBar.tsx` resolves its `Badge` import; build clean.
- [ ] **Task 3.4** `components/ui/input.tsx` — `Input` (native `<input>`, shadcn classes, `React.forwardRef`).
  - **Acceptance:** typechecks; used by SettingsPage.
- [ ] **Task 3.5** `components/ui/label.tsx` — `Label` on `@radix-ui/react-label`.
  - **Acceptance:** typechecks; used by SettingsPage.
- [ ] **Task 3.6** `components/ui/table.tsx` — `Table`, `TableHeader`, `TableBody`, `TableFooter`, `TableRow`, `TableHead`, `TableCell`, `TableCaption` (styled `<table>` elements). Backs the TanStack Table grid.
  - **Acceptance:** IssuersPage renders a grid from these + TanStack Table (Task 7.1).
- [ ] **Task 3.7** `components/ui/dialog.tsx` — shadcn Dialog on `@radix-ui/react-dialog` (`Dialog`, `DialogTrigger`, `DialogContent`, `DialogHeader`, `DialogFooter`, `DialogTitle`, `DialogDescription`, `DialogClose`; `X` icon from lucide).
  - **Acceptance:** typechecks (consumed fully by pkg 10 `RuleModal`; authored now for completeness).
- [ ] **Task 3.8** `components/ui/tabs.tsx` — shadcn Tabs on `@radix-ui/react-tabs` (`Tabs`, `TabsList`, `TabsTrigger`, `TabsContent`).
  - **Acceptance:** typechecks; used by SettingsPage section grouping (Task 7.5).
- [ ] **Task 3.9** `components/ui/tooltip.tsx` — shadcn Tooltip on `@radix-ui/react-tooltip` (`TooltipProvider`, `Tooltip`, `TooltipTrigger`, `TooltipContent`).
  - **Acceptance:** typechecks (used by pkg 10; authored now).
- [ ] **Task 3.10** `components/ui/select.tsx` — shadcn Select on `@radix-ui/react-select` (`Select`, `SelectGroup`, `SelectValue`, `SelectTrigger`, `SelectContent`, `SelectLabel`, `SelectItem`, `SelectSeparator`; `Check`/`ChevronDown`/`ChevronUp` lucide icons).
  - **Acceptance:** SettingsPage model-per-agent + as-of controls render (Task 7.5).

## Phase 6 — Layout, lib & providers (shell)

- [ ] **Task 6.1** Copy `main.tsx`, `index.css`, `lib/utils.ts`, `lib/queryClient.ts` verbatim to their `frontend/src/` destinations (per template map).
  - **Acceptance:** `main.tsx` imports `./index.css`; `queryClient`/`cn` importable.
- [ ] **Task 6.2** Copy `AppLayout.tsx` verbatim to `src/components/layout/AppLayout.tsx`.
  - **Acceptance:** renders `<Sidebar/> <TopBar/> <Outlet/>`; imports resolve.
- [ ] **Task 6.3** Copy `PageHeader.tsx` + `StatCard.tsx` verbatim to `src/components/ui/`.
  - **Acceptance:** importable as `@/components/ui/PageHeader` / `@/components/ui/StatCard`.

## Phase 4 — App wiring & navigation (rebrand to Prism)

- [ ] **Task 4.1** Author `frontend/src/App.tsx` — provider order (arch-10) `QueryClientProvider` → `CopilotKit runtimeUrl="/copilotkit"` → `BrowserRouter`. Routes inside `<AppLayout/>`: `index` → `<Navigate to="/issuers" replace/>`, `/issuers`→`IssuersPage`, `/reconciliation`→`ReconciliationPage`, `/workflow`→`WorkflowPage`, `/architecture`→`ArchitecturePage`, `/settings`→`SettingsPage`. Keep `<CopilotSidebar>` but rebrand labels → `{ title: 'Prism Copilot', initial: 'Ask about a rating divergence or red flag.' }` (P4-clean; Open Decision 3 — sidebar-while-sidecar-absent). Import `@copilotkit/react-ui/styles.css`.
  - **Acceptance:** all 6 routes typecheck; no reference to `DashboardPage`; build clean.
- [ ] **Task 4.2** Edit `src/components/layout/Sidebar.tsx` — replace `NAV_GROUPS` with the Prism groups; swap icon imports to `Building2, Scale, GitBranch, Network, Settings, ChevronRight`; rebrand the brand block "Financial AI"/"Platform"/letter `F` → **"Prism"**/"Reconciliation"/letter `P`.
    ```ts
    const NAV_GROUPS = [
      { label: 'Prism', items: [
          { to: '/issuers', icon: Building2, label: 'Issuers' },
          { to: '/reconciliation', icon: Scale, label: 'Reconciliation' } ] },
      { label: 'Architecture', items: [
          { to: '/workflow', icon: GitBranch, label: 'Workflow' },
          { to: '/architecture', icon: Network, label: 'Architecture' } ] },
      { label: 'Settings', items: [{ to: '/settings', icon: Settings, label: 'Settings' }] },
    ]
    ```
  - **Acceptance:** three group labels render (asserted in Task 11.1); no "Financial AI" string remains.
- [ ] **Task 4.3** Edit `src/components/layout/TopBar.tsx` — update `TITLES` map to `{ '/issuers':'Issuers', '/reconciliation':'Reconciliation', '/workflow':'Workflow', '/architecture':'Architecture', '/settings':'Settings' }`; default fallback `'Prism'`; badge text → `'Prism · Azure AI Foundry'`.
  - **Acceptance:** default title is `Prism`, not `Financial AI`.

## Phase 7 — Pages

- [ ] **Task 7.1** Author `frontend/src/pages/IssuersPage.tsx` — consumes `useIssuers()`. Render **explicit** `isPending` (loading text/skeleton in a Card), `isError` (error Card showing `error.message` — **no fabricated rows**, P1), empty (`data.length === 0` → "No issuers covered yet" Card), and success = **TanStack Table** grid (`@tanstack/react-table`) over `IssuerListItem[]` using the `components/ui/table` primitives. Columns: Legal name, Ticker, Sector, Sample bond ISIN, Coverage (Badges per `coverage?`), Action (`<Button asChild><Link to={\`/reconciliation?issuer=\${issuerId}\`}>Reconcile</Link></Button>`). Header via `PageHeader`.
  - **Acceptance:** typechecks; renders loading/error/empty/success; grid uses TanStack Table (arch-10); no `useEffect` fetch; P4-clean copy ("Reconcile", not "trade").
- [ ] **Task 7.2** Author `frontend/src/pages/ReconciliationPage.tsx` — **placeholder shell** (full UI = pkg 10). Read `?issuer=` via `useSearchParams`. Render `PageHeader` + a Card explaining the reconciliation experience is mounted here and, when an issuer is present, a stub note ("Selected issuer: {id} — run pipeline (coming in the Prism UI package)"). No fetching yet beyond optionally showing the id.
  - **Acceptance:** route renders without error; no mock dossier; P4-clean.
- [ ] **Task 7.3** Copy `WorkflowPage.tsx` to `src/pages/WorkflowPage.tsx` and edit its 3 imports to `@/components/workflow/{WorkflowDiagram,workflowData,workflowTypes}` (per Workflow wiring). (Data + component tasks are Phase 8.)
  - **Acceptance:** page imports resolve to `components/workflow/`; renders the diagram (Phase 8 data).
- [ ] **Task 7.4** Author `frontend/src/pages/ArchitecturePage.tsx` — **static** architecture summary (no fetching). `PageHeader` + Cards listing: agents (`ReconciliationOrchestrator`, `ProviderExplainerAgent`, `FundamentalsAgent`, `DivergenceNarratorAgent`, `RedFlagNarratorAgent`), deterministic `Analysis/` engines (`NotchLadder`, `DivergenceDecomposer`, `RedFlagEngine`, `ReconciliationScoring`), Azure services (AI Foundry, AI Search `prism-ratings`, Cosmos `prism`), and data sources (SEC EDGAR, FRED, Moody's, Morningstar DBRS, MSCI synthetic). P2/P4 framing: "deterministic core; LLM narrates & cites; reconciliation not trading."
  - **Acceptance:** renders static content; every label P4-clean; uses shadcn Card + tokens only.
- [ ] **Task 7.5** Author `frontend/src/pages/SettingsPage.tsx` — reads/writes `lib/settings.ts` (Task 7.6) via `useState` seeded from `loadSettings()`, persisting on change with `saveSettings()`. Controls: **model-per-agent** (`Select` per agent → maps to `Prism__Models__*`), **providers to include** toggles (Moodys / MorningstarDbrs / MSCI), **default as-of date** (`Input type="date"`), **API/runtime endpoints** (read-only `Input disabled` showing `/api/v1` + `/copilotkit`), **feature flags** (e.g. `showDeterministicRule` toggle). Group sections with `Tabs` or Cards. Note server-side config as the productionization path (comment/muted text).
  - **Acceptance:** changes persist across reload (localStorage); no server calls; provider toggles limited to the 3 real providers; P4-clean.
- [ ] **Task 7.6** Author `frontend/src/lib/settings.ts` — `PrismSettings` type (models per agent, `providers: Provider[]`, `defaultAsOf: string`, `flags: { showDeterministicRule: boolean }`), `DEFAULT_SETTINGS`, `loadSettings()` (parse `localStorage['prism.settings']`, merge over defaults, try/catch → defaults on bad JSON), `saveSettings(s)`.
  - **Acceptance:** pure module; `loadSettings()` returns `DEFAULT_SETTINGS` when storage empty/corrupt (no throw).

## Phase 8 — Workflow Visualization (REQUIRED)

- [ ] **Task 8.1** Copy `workflowTypes.ts`, `WorkflowNode.tsx`, `WorkflowDetailPanel.tsx`, `WorkflowDiagram.tsx` verbatim to `src/components/workflow/`; then **rewrite** `src/components/workflow/workflowData.ts` as a **minimal Prism** dataset — ONE `WorkflowTab` `{ id: 'rating-reconciliation', label: 'Rating Reconciliation Pipeline', ... }` with ~7 nodes (kebab-case ids per naming-01) and their edges. Keep node `detail` fields honest and P4-clean. Suggested nodes/types:
    - `issuer-selected` (service) → `reconciliation-orchestrator` (agent)
    - `reconciliation-orchestrator` → fan-out to `provider-explainer-agent` (agent), `fundamentals-agent` (agent)
    - `provider-explainer-agent` + `fundamentals-agent` → `divergence-decomposer` (service/`Analysis`)
    - `divergence-decomposer` → `red-flag-engine` (service/`Analysis`)
    - `red-flag-engine` → `confirm-scope-gate` (gate, amber) → `dossier-ready` (outcome)
  - **Acceptance:** `workflowTabs` is a non-empty `WorkflowTab[]`; no P4 vocab; the sample "meeting-intelligence"/"recommendation" content is gone. (Scope split — Open Decision 4: minimal seed here; pkg 10 enriches node details/positions and adds the full annotated pipeline.)
- [ ] **Task 8.2** Confirm `WorkflowPage.tsx` (Task 7.3) renders `<WorkflowDiagram tab={activeTab} />` from the Prism `workflowTabs` with `LEGEND` intact.
  - **Acceptance:** WorkflowPage builds and references the co-located data/types; `npm run build` green.

## Phase 9 — Architecture Page (covered by Task 7.4)

- [ ] **Task 9.1** (satisfied by Task 7.4) — no infra change in pkg 09; the static Architecture summary reflects the planned services. Revisit when pkg 11 (deployment) lands.
  - **Acceptance:** Architecture route present in nav + router.

## Phase 10 — Settings (covered by Tasks 7.5 / 7.6)

- [ ] **Task 10.1** (satisfied by Tasks 7.5/7.6) — settings persist to `localStorage`; server-side config noted as productionization path.
  - **Acceptance:** Settings route present; persistence verified manually / via optional test.

## Phase 11 — Tests (vitest smoke harness)

- [ ] **Task 11.1** Author `frontend/src/components/layout/Sidebar.test.tsx` — render `<Sidebar/>` inside `<MemoryRouter>`; assert the three group labels **Prism**, **Architecture**, **Settings** render, and the five nav items (Issuers, Reconciliation, Workflow, Architecture, Settings) are present. Proves the harness + nav wiring.
  - **Acceptance:** `npm run test` passes; fails if a nav group is dropped (guards Task 4.2).
- [ ] **Task 11.2** Author `frontend/src/lib/apiClient.test.ts` — mock `global.fetch`; assert (a) a 200 JSON body is returned typed; (b) a non-2xx with `{ error: { code:'NOT_FOUND', message:'…' } }` **throws `ApiError`** with `.code === 'NOT_FOUND'` and `.status`. Proves the P1 error-contract (no fabricated fallback).
  - **Acceptance:** both cases pass; `ApiError` shape asserted.

---

## File Change Summary

| File | Action | Notes |
|---|---|---|
| `frontend/package.json` | Create | pinned deps + scripts (Task 0.1) |
| `frontend/tsconfig.json` | Create | strict + `@/*` paths (0.2) |
| `frontend/tsconfig.node.json` | Create | vite/vitest config typing (0.3) |
| `frontend/vite.config.ts` | Create (copy) | verbatim template (0.4) |
| `frontend/tailwind.config.js` | Create (copy) | verbatim (0.4) |
| `frontend/postcss.config.js` | Create (copy) | verbatim (0.4) |
| `frontend/components.json` | Create (copy) | verbatim (0.4) |
| `frontend/index.html` | Create (copy+edit) | title → Prism (0.5) |
| `frontend/.gitignore` | Create | node_modules/dist/coverage (0.6) |
| `frontend/vitest.config.ts` | Create | jsdom + setup (0.7) |
| `frontend/src/test/setup.ts` | Create | jest-dom (0.8) |
| `frontend/src/main.tsx` | Create (copy) | verbatim (6.1) |
| `frontend/src/index.css` | Create (copy) | shadcn dark tokens (6.1) |
| `frontend/src/lib/utils.ts` | Create (copy) | `cn` (6.1) |
| `frontend/src/lib/queryClient.ts` | Create (copy) | TanStack client (6.1) |
| `frontend/src/lib/apiClient.ts` | Create | typed fetch + `ApiError` (5.2) |
| `frontend/src/lib/settings.ts` | Create | localStorage settings (7.6) |
| `frontend/src/types/prism.ts` | Create | DTO mirrors (5.1) |
| `frontend/src/hooks/useIssuers.ts` | Create | `useQuery(['issuers'])` (5.3) |
| `frontend/src/hooks/useReconciliation.ts` | Create | run mutation + dossier query (5.4) |
| `frontend/src/components/ui/{button,card,badge,input,label,table,dialog,tabs,tooltip,select}.tsx` | Create (10) | hand-authored shadcn (3.1–3.10) |
| `frontend/src/components/ui/PageHeader.tsx` | Create (copy) | verbatim (6.3) |
| `frontend/src/components/ui/StatCard.tsx` | Create (copy) | verbatim (6.3) |
| `frontend/src/components/layout/AppLayout.tsx` | Create (copy) | verbatim (6.2) |
| `frontend/src/components/layout/Sidebar.tsx` | Create (copy+edit) | Prism NAV_GROUPS + brand (4.2) |
| `frontend/src/components/layout/TopBar.tsx` | Create (copy+edit) | TITLES + default Prism (4.3) |
| `frontend/src/components/workflow/{workflowTypes.ts,WorkflowNode.tsx,WorkflowDetailPanel.tsx,WorkflowDiagram.tsx}` | Create (copy) | verbatim (8.1) |
| `frontend/src/components/workflow/workflowData.ts` | Create (rewrite) | minimal Prism tab (8.1) |
| `frontend/src/App.tsx` | Create (author) | providers + routes + rebrand (4.1) |
| `frontend/src/pages/IssuersPage.tsx` | Create | TanStack Table + states (7.1) |
| `frontend/src/pages/ReconciliationPage.tsx` | Create | placeholder shell (7.2) |
| `frontend/src/pages/WorkflowPage.tsx` | Create (copy+edit) | 3 import fixes (7.3/8.2) |
| `frontend/src/pages/ArchitecturePage.tsx` | Create | static summary (7.4) |
| `frontend/src/pages/SettingsPage.tsx` | Create | settings + localStorage (7.5) |
| `frontend/src/components/layout/Sidebar.test.tsx` | Create | nav smoke test (11.1) |
| `frontend/src/lib/apiClient.test.ts` | Create | error-contract test (11.2) |

**Totals:** ~40 files created (2 copied-then-edited configs, ~11 verbatim copies, ~27 authored/edited). 0 backend files touched. Workflow nodes added: **~7** (1 minimal Prism tab). Files modified: **0 pre-existing** (net-new `frontend/`).

---

## Acceptance mapping (to `implementationPlan/09` Acceptance list)

| Package acceptance item | Satisfied by |
|---|---|
| Shell runs; sidebar shows Prism + Architecture + Settings; routes navigate | Tasks 4.1–4.3, 6.2, 7.1–7.5, 8.2 + smoke Task 11.1 |
| `IssuersPage` lists issuers from the **real** API via TanStack Query (loading/error states) | Tasks 5.1–5.3, 7.1 (P1: honest states, no mock data) |
| Dark theme via shadcn tokens; icons from `lucide-react`; no inline styles | Tasks 0.4/0.5/6.1 (tokens), 3.1–3.10 (primitives), 4.2 (lucide) |
| `types/prism.ts` compiles against the API DTO shapes | Tasks 5.1 + build gate (tsc `--noEmit`) |
| **Build/gate (11 + task brief):** `npm run build` green + strict typecheck + `npm run test` green | Tasks 0.1–0.3 (strict tsconfig, scripts), all Phases, Tasks 11.1–11.2 |

---

## Open Decisions (flag for the implementor / adversary)

1. **Exact dependency versions.** Pins in Task 0.1 are known-good-era values; the implementor may need to nudge a patch if `npm install` surfaces a peer conflict (esp. CopilotKit ↔ React 18). **Rule:** keep pins exact, bump only the offending patch, never widen to `latest`. Record any change in the changes log.
2. **Hand-author vs `npx shadcn add`.** Plan recommends **hand-authoring** the 10 primitives (deterministic, no CLI/network). Alternative: `npx shadcn@latest add --yes card badge button input label table dialog tabs tooltip select` (non-interactive) — faster but network-dependent and can drift from `components.json` style. **Default: hand-author.**
3. **CopilotKit sidebar while the sidecar (:4000) is absent.** The sidecar arrives in pkg 07; until then the `/copilotkit` proxy has no target. This does **not** break `npm run build` (static). **Default: keep `CopilotKit` provider + `<CopilotSidebar>` (arch-10 provider order), rebranded to Prism** — the failing proxy only affects the copilot panel at runtime, not the app or the gate. Alternative: render `<CopilotSidebar>` behind a feature flag until pkg 07. Flag for review.
4. **Workflow tab scope: pkg 09 vs pkg 10.** Plan ships a **minimal single Prism tab** (~7 nodes, basic details) so the page is on-domain + P4-clean + build-green; pkg 10 enriches node details/positions and the full annotated pipeline. Risk: mild overlap with pkg 10. Alternative: ship the diagram with an empty/placeholder tab. **Default: minimal seed.**
5. **INFERRED DTO shapes (`types/prism.ts`).** `Models/PrismDtos.cs` is pkg 08 — the TS mirrors are derived from arch-09 + `PrismModels.cs` and **will need re-sync** when pkg 08 lands (contract-sync rule). Names locked: `DossierResponse`, `ProviderVerdictDto`, `PairDivergenceDto`, `RedFlagDto`, `IssuerListItem`, `ReconciliationRequest`. Whether `ProviderVerdictDto` carries `factors` and whether `IssuerListItem` carries `coverage` are **provisional** (marked optional). Flag.
6. **`IssuersPage` grid.** Uses TanStack Table (arch-10 mandates TanStack for grids) even though the issuer list is small — chosen for standards-conformance over a plain `<ul>`. Confirm acceptable.
7. **`vitest.config.ts` separate vs merged into `vite.config.ts`.** Plan keeps them **separate** so the template `vite.config.ts` stays verbatim (avoids the `vite` vs `vitest/config` `defineConfig` type friction). Alternative: merge and import `defineConfig` from `vitest/config`. **Default: separate.**
8. **camelCase wire contract.** Assumes `System.Text.Json` default camelCase (arch-09) so TS fields are camelCase and enum values serialize as **names** (`'MorningstarDbrs'`). If pkg 08 configures `JsonStringEnumConverter` differently or PascalCase properties, re-sync. Flag.
9. **ESLint / `npm run lint`.** **Recommended deferred** — no ESLint config in this package to avoid version churn; the acceptance gate is `build` + `test`. If a lint gate is desired, add `eslint@8` + `@typescript-eslint` + `eslint-plugin-react-hooks` in a follow-up. Flag.
10. **`recharts` pinned but unused in pkg 09.** Included per the task's dependency list (pkg 10 waterfall needs it). Alternative: defer to pkg 10. **Default: pin now** (keeps pkg 10 install-free).
