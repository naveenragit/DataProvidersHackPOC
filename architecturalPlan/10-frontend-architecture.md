# 10 — Frontend Architecture

React 18 + Vite + shadcn/ui + TanStack + CopilotKit + Tailwind. Follow the design-system template.

---

## Providers & routing (`App.tsx`)

Provider order (outer→inner): `QueryClientProvider` → `CopilotKit runtimeUrl="/copilotkit"` →
`BrowserRouter`. Routes mount inside `AppLayout`. Keep the required **Architecture** (Workflow +
Architecture) and **Settings** nav groups; the feature group is **Prism** (Issuers, Reconciliation).

## State management (strict)

- **All server state via TanStack Query.** No ad-hoc `useEffect` + `fetch`. Query keys are stable
  arrays (`['issuers']`, `['reconciliation', id]`).
- **All grids via TanStack Table** (the factor breakdown table).
- AG-UI agent state via `useCoAgent({ name: 'prism' })`; streaming tool calls rendered with
  `useCopilotAction({ render })`; the scope gate via `renderAndWaitForResponse`.
- Local UI state (selected provider pair, modal open) via `useState`. Settings persisted to
  `localStorage` for the demo.

## UI system

- **shadcn/ui only** for primitives (button, card, table, dialog, tabs, badge, tooltip). Never
  hand-roll them. Add primitives under `components/ui/`.
- **Tailwind + shadcn tokens**, dark theme by default: `bg-background`, `bg-card`, `border-border`,
  `text-foreground`, `text-muted-foreground`, accent via `primary` (indigo/violet). No inline styles,
  no CSS modules.
- Icons from `lucide-react` only.
- HITL gates render **amber/orange**; red flags render in the destructive/red token.

## Component boundaries

- Feature components under `components/prism/` (`DivergenceBoard`, `ProviderVerdictCard`,
  `DecompositionWaterfall`, `EvidenceStream`, `RedFlagBanner`, `RuleModal`, `DossierPanel`).
- Charts via **recharts** (the waterfall). Keep to ≤4 bars for legibility.
- Pages compose components; pages hold no fetching logic beyond calling hooks.
- Data types in `types/prism.ts` mirror the API DTOs ([09](09-api-and-contracts.md)).

## Data & errors

- `lib/apiClient.ts`: typed `fetch` wrapper on `/api/v1`, throws on non-2xx (parses the standard error
  shape). Hooks in `hooks/` wrap Query/Mutation.
- Every query renders explicit loading + error states; a top-level error boundary wraps the router.
- **Never render fabricated placeholder data** on error (P1) — show the error state.

## Accessibility & quality

- Strict TS (`"strict": true`). Functional components + hooks only (no classes).
- Semantic HTML, keyboard-navigable dialogs (shadcn/Radix handles most), `aria-` labels on icon-only
  buttons, sufficient contrast in the dark theme.

## Required pages

- `IssuersPage`, `ReconciliationPage` (the demo), `WorkflowPage` (the Rating Reconciliation Pipeline
  tab — mandatory), `ArchitecturePage`, `SettingsPage`.
