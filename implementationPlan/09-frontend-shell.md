# 09 — Frontend Shell & Navigation

**Purpose:** stand up the React app from the design-system template with Prism's navigation, data
layer, and Settings — the frame the Prism UI (package 10) mounts into.

**Depends on:** 01. **Blocks:** 10. Can run in parallel with backend work using the fallback REST API.

Standards (react-frontend instructions): strict TS, shadcn/ui only, TanStack Query for all server
state, TanStack Table for grids, Tailwind + shadcn tokens (dark theme), `lucide-react` icons.

---

## A. App structure (from `templates/frontend-design-system`)

```
frontend/src/
  App.tsx                # QueryClientProvider → CopilotKit(runtimeUrl="/copilotkit") → BrowserRouter
  main.tsx  index.css
  components/
    layout/   AppLayout.tsx  Sidebar.tsx  TopBar.tsx
    ui/       (shadcn primitives: card, table, badge, dialog, button, tabs, tooltip)
    workflow/ (from templates/workflow-visualization — package 10)
    prism/    (package 10 feature components)
  pages/
    IssuersPage.tsx        # pick an issuer → launch reconciliation
    ReconciliationPage.tsx # the Prism experience (package 10)
    WorkflowPage.tsx       # Architecture group
    ArchitecturePage.tsx   # Architecture group
    SettingsPage.tsx       # Settings group
  hooks/    useIssuers.ts  useReconciliation.ts
  lib/      apiClient.ts  queryClient.ts  utils.ts (cn)
  types/    prism.ts       # mirror the C# DTOs
```

---

## B. Navigation (edit `Sidebar.tsx` `NAV_GROUPS`)

Keep the required **Architecture** and **Settings** groups; replace the feature group:
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
Rebrand the sidebar header from "Financial AI" to **"Prism"**. Add the routes in `App.tsx`
(uncomment/extend the template's route block).

---

## C. Data layer

- `lib/apiClient.ts`: thin `fetch` wrapper on `/api/v1` (Vite proxies to :8000), typed, throws on non-2xx.
- `hooks/useIssuers.ts`: `useQuery(['issuers'], …)`; `hooks/useReconciliation.ts`:
  `useMutation`/`useQuery` for the fallback REST run + dossier fetch.
- `types/prism.ts`: TS mirrors of `Issuer`, `ProviderRating`, `PairDivergence`, `RedFlag`,
  `ReconciliationDossier` (keep in sync with package 02).

---

## D. Settings page (rubric: configurable → "customer build")

Model selection per agent (maps to `Prism__Models__*`), providers to include, default as-of date,
API/runtime endpoints (read-only), feature flags (e.g. show-deterministic-rule). Persist to
`localStorage` for the demo; note server-side config as the productionization path.

---

## Acceptance for this package
- [ ] Shell runs; sidebar shows Prism + Architecture + Settings groups; routes navigate.
- [ ] `IssuersPage` lists issuers from the **real** API via TanStack Query (loading/error states).
- [ ] Dark theme via shadcn tokens; icons from `lucide-react`; no inline styles.
- [ ] `types/prism.ts` compiles against the API DTO shapes.
