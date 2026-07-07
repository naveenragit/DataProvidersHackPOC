# Plan 08 — Frontend Shell, Routing & Settings

**Objective:** Stand up the React 18 + Vite + shadcn/ui + TanStack + CopilotKit + Tailwind shell with
the Rewind dark theme, the required sidebar groups, provider stack, a typed API client generated from
OpenAPI, and the Settings page. This is the Day-1 React shell (parallel with the corpus work).

**Depends on:** Plan 01. **Primary day:** 1.

**Reference templates:** `templates/frontend-design-system/` — copy the known-good files **verbatim**,
then rebrand. `templates/workflow-visualization/` is consumed by Plan 11.

> Folds ⚠ STK-07 (`tailwindcss-animate` must be installed), STK-09/19 (SignalR client if real-time is
> used), STK-10/ARC-10 (generate TS types from OpenAPI — no hand-drift), STK-16/17 (theme + plugin).

---

## 1. Scaffold + dependencies

- [ ] Vite React-TS app in `frontend/` (strict TypeScript)
- [ ] Runtime deps: `react-router-dom`, `lucide-react`, `class-variance-authority`, `clsx`,
      `tailwind-merge`, `@tanstack/react-query`, `@tanstack/react-table`, `@copilotkit/react-core`,
      `@copilotkit/react-ui`, `recharts`
- [ ] Dev deps: `tailwindcss@^3`, `postcss`, `autoprefixer`, `@vitejs/plugin-react`,
      **`tailwindcss-animate`** (⚠ STK-07 — without it Tailwind aborts to a white unstyled page)
- [ ] If live streaming status uses SignalR rather than AG-UI SSE, add `@microsoft/signalr` +
      a typed `useHubConnection` hook (⚠ STK-09/19); otherwise rely on CopilotKit SSE and skip
- [ ] `npx shadcn@latest init` (dark base, CSS variables); add primitives: `card badge button input
      textarea label table dialog tabs tooltip scroll-area separator`

## 2. Copy + rebrand the design system

- [ ] Copy `index.html`, `vite.config.ts`, `tailwind.config.js`, `postcss.config.js`, `components.json`
      to `frontend/`; copy `index.css`, `main.tsx`, `App.tsx`, `lib/` to `frontend/src/`
- [ ] ⚠ STK-16: keep `components.json` `baseColor` consistent with the indigo `--primary` in
      `index.css` so a re-`init` doesn't overwrite the theme with slate
- [ ] `vite.config.ts` proxies `/api` → `:8000` and `/copilotkit` → `:4000`
- [ ] Rebrand to **Rewind — Decision Black Box Recorder** (sidebar brand, TopBar title, favicon)

## 3. Provider stack (`App.tsx`)

- [ ] `QueryClientProvider` → `CopilotKit runtimeUrl="/copilotkit"` → `BrowserRouter`
- [ ] Mount routes inside `AppLayout`: `reconstruct` (main feature), `workflow`, `architecture`,
      `settings`; index redirect to the reconstruction page
- [ ] `CopilotSidebar` labelled for Rewind (compliance framing, no buy/sell language)

## 4. Sidebar navigation (required groups)

Author `frontend/src/components/layout/Sidebar.tsx`:

- [ ] **Reconstruction** group (feature): "New Inquiry", "Timeline" (the black box), "Dossier"
- [ ] **Architecture** group (required): "Workflow", "Architecture"
- [ ] **Settings** group (required): "Settings"
- [ ] `lucide-react` icons; shadcn tokens + `cn()`

## 5. Typed API client from OpenAPI (⚠ ARC-10/STK-10)

- [ ] Generate `frontend/src/types/api.ts` from `http://localhost:8000/openapi/v1.json` using
      `openapi-typescript` (or Kiota); add an npm script `gen:api` and run it in CI so schema drift
      breaks the build instead of the UI
- [ ] `frontend/src/lib/apiClient.ts` — typed fetch wrapper attaching the Entra token; TanStack Query
      hooks per endpoint (`useSubmitInquiry`, `useConfirmScope`, `useReconstruction`, `useDossier`)
- [ ] No `useEffect` fetching — all server state via TanStack Query

## 6. Settings page

Author `frontend/src/pages/SettingsPage.tsx`:

- [ ] Per-agent model selection (gpt-5 / gpt-5.4 / gpt-5-mini / gpt-4.1-mini) with the GPT-4.1 fallback
- [ ] Feature flags: MNPI/restricted-list rule on/off, control-decision run, live-vs-pre-rendered export
- [ ] API endpoint display (read-only) + health indicator (calls `/api/health`)
- [ ] Reconstruction defaults (observation-date framing note shown to the user)

## 7. Reconstruction entry page (thin for now; timeline is Plan 09)

- [ ] `frontend/src/pages/ReconstructPage.tsx` — inquiry form (issuer, instrument, decision date) +
      submit via `useSubmitInquiry`; shows status; hands off to the Timeline UI (Plan 09)

## Acceptance criteria

- `npm run dev` shows the **dark** Rewind shell (Inter font, grouped sidebar, TopBar, shadcn cards) —
  not a white serif page
- Sidebar shows Reconstruction + Architecture + Settings groups; routes resolve
- TS API types are generated from OpenAPI (not hand-written); a backend rename breaks `npm run build`
- Settings renders model selection + flags; health indicator reflects the API
- CopilotKit sidebar toggles and targets the Node sidecar

## Cut-lines

- Drop `recharts`-based extras first; the two-lane timeline (Plan 09) is the priority visual, not
  dashboards
