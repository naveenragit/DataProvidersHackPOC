---
description: "React 18 + Vite + shadcn/ui + TanStack + CopilotKit + Tailwind frontend standards for financial services applications"
applyTo: "**/*.tsx, **/*.ts"
---

# React Frontend Standards — Financial Services

## Technology Stack (non-negotiable)

- **React 18** with functional components and hooks only
- **Vite** as the build tool and dev server
- **TypeScript** strict mode (`"strict": true`)
- **shadcn/ui** (Radix primitives + Tailwind) for every UI primitive — never hand-roll buttons, dialogs, inputs, or tables
- **TanStack Query** (`@tanstack/react-query`) for all server state
- **TanStack Table** (`@tanstack/react-table`) for all data grids
- **CopilotKit** (`@copilotkit/react-core`, `@copilotkit/react-ui`) for the AI copilot surface, wired to a Node runtime sidecar
- **Tailwind CSS** for all styling
- **lucide-react** for all icons
- **React Router v6** for navigation
- **@microsoft/signalr** for real-time features (SignalR hubs on the C# API) — add only when a page needs streaming/live updates

## Reference Design

The target look is a **modern, professional dark dashboard** (Inter font, near-black
background, rounded cards, indigo/violet accent, left sidebar). Ready-to-copy design-system
files live in `templates/frontend-design-system/`. Use them verbatim — do not hand-roll the theme.

## Mandatory Setup (do this FIRST — non-negotiable)

A page that renders as white background, serif fonts, and plain browser inputs means
**Tailwind + shadcn were never wired up**. This is the #1 frontend failure. Before writing any
page, ensure ALL of the following exist:

1. Runtime deps installed:
   `npm install react-router-dom lucide-react class-variance-authority clsx tailwind-merge @tanstack/react-query @tanstack/react-table @copilotkit/react-core @copilotkit/react-ui`
2. Dev deps installed: `npm install -D tailwindcss@^3 postcss autoprefixer tailwindcss-animate @vitejs/plugin-react`
3. shadcn/ui initialized: `npx shadcn@latest init` (dark base color, CSS variables enabled) — produces `components.json` and `src/lib/utils.ts` with the `cn()` helper.
4. `frontend/index.html` includes `class="dark"` on `<html>` and Google Fonts (Inter + JetBrains Mono).
5. `frontend/vite.config.ts` includes the `@` alias (`./src`), an `/api` proxy to the C# backend, and a `/copilotkit` proxy to the Node runtime sidecar (copy `templates/frontend-design-system/vite.config.ts`).
6. `frontend/tailwind.config.js` exists with `darkMode: ['class']`, `content: ['./index.html', './src/**/*.{ts,tsx}']`, and the shadcn theme extension (copy `templates/frontend-design-system/tailwind.config.js`).
7. `frontend/postcss.config.js` exists with the `tailwindcss` + `autoprefixer` plugins.
8. `frontend/src/index.css` starts with `@tailwind base; @tailwind components; @tailwind utilities;` and defines the shadcn CSS variables (`--background`, `--card`, `--primary`, `--border`, ...) under `:root` and `.dark` (copy `templates/frontend-design-system/index.css`).
9. `frontend/src/main.tsx` contains `import './index.css'`.
10. `frontend/src/App.tsx` wraps routes in `CopilotKit` + `QueryClientProvider` and mounts them inside `AppLayout` (copy `templates/frontend-design-system/App.tsx`).

**Verification gate:** run `npm run dev` and confirm a dark background, Inter font, a grouped
left sidebar (`w-60`), a top bar (`h-16`), and rounded shadcn cards render. If not, fix the
configuration above before proceeding.

## Design System — shadcn CSS Variable Tokens

Style with the **semantic shadcn tokens** (backed by CSS variables), not raw slate classes and
not inline hex. The dark theme is the default.

| Purpose | Tailwind Class | Use |
|---|---|---|
| App background | `bg-background` | Body, main scroll area |
| Card / panel | `bg-card` | Cards, panels, sidebar surfaces |
| Popover | `bg-popover` | Dropdowns, detail panels |
| Border | `border-border` | All borders and dividers |
| Primary / accent | `bg-primary` / `text-primary` | CTAs, active nav, highlights |
| Muted surface | `bg-muted` | Hover rows, inputs |
| Primary text | `text-foreground` | Headings, active labels |
| Secondary text | `text-muted-foreground` | Subtitles, metadata |
| Destructive | `text-destructive` / `bg-destructive` | Errors, negative actions |

For financial semantics not covered by shadcn tokens, add these to the Tailwind theme and use
them consistently: `text-success` / `text-danger` (returns), `text-warning` / `brand-gold`
(human-in-the-loop gates), `text-info` (services), `brand-teal` (data stores).

## App Layout with Sidebar

Every app uses a two-column layout: fixed sidebar + scrollable main content, composed from
shadcn primitives.

```tsx
// src/components/layout/AppLayout.tsx
import { Outlet } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { TopBar } from './TopBar'

export function AppLayout() {
  return (
    <div className="flex h-screen overflow-hidden bg-background text-foreground">
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        <TopBar />
        <main className="flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
```

### Required Sidebar Navigation Groups (always present)

```tsx
import { GitBranch, Layers, Settings, type LucideIcon } from 'lucide-react'

interface NavItem { to: string; icon: LucideIcon; label: string }
interface NavGroup { label: string; items: NavItem[] }

const ARCHITECTURE_GROUP: NavGroup = {
  label: 'Architecture',
  items: [
    { to: '/workflow', icon: GitBranch, label: 'Workflow' },
    { to: '/architecture', icon: Layers, label: 'Architecture' },
  ],
}

const SETTINGS_GROUP: NavGroup = {
  label: 'Settings',
  items: [{ to: '/settings', icon: Settings, label: 'Settings' }],
}
```

## App Root — Providers (`App.tsx`)

Wrap the app in `CopilotKit` (pointing at the Node runtime sidecar) and `QueryClientProvider`.

```tsx
import { CopilotKit } from '@copilotkit/react-core'
import { CopilotSidebar } from '@copilotkit/react-ui'
import '@copilotkit/react-ui/styles.css'
import { QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { queryClient } from '@/lib/queryClient'
import { AppLayout } from '@/components/layout/AppLayout'

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      {/* runtimeUrl proxies to the CopilotKit Node sidecar, which forwards to the C# API + Azure OpenAI */}
      <CopilotKit runtimeUrl="/copilotkit">
        <BrowserRouter>
          <Routes>
            <Route element={<AppLayout />}>
              {/* feature + Architecture + Settings routes */}
            </Route>
          </Routes>
        </BrowserRouter>
        <CopilotSidebar labels={{ title: 'Financial Copilot' }} />
      </CopilotKit>
    </QueryClientProvider>
  )
}
```

## Server State — TanStack Query

Never fetch with `useEffect`. Every server interaction goes through a typed query/mutation hook.

```tsx
// src/lib/queryClient.ts
import { QueryClient } from '@tanstack/react-query'

export const queryClient = new QueryClient({
  defaultOptions: { queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false } },
})
```

```tsx
// src/hooks/usePortfolio.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPost } from '@/lib/apiClient'
import type { PortfolioSummary, RebalanceRequest } from '@/types'

export const portfolioKeys = {
  all: ['portfolios'] as const,
  detail: (id: string) => [...portfolioKeys.all, id] as const,
}

export function usePortfolio(portfolioId: string) {
  return useQuery({
    queryKey: portfolioKeys.detail(portfolioId),
    queryFn: () => apiGet<PortfolioSummary>(`/portfolios/${portfolioId}`),
  })
}

export function useRebalance(portfolioId: string) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (body: RebalanceRequest) =>
      apiPost(`/portfolios/${portfolioId}/rebalance`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: portfolioKeys.detail(portfolioId) }),
  })
}
```

Handle `isPending` / `isError` states in the UI for every data-fetching component.

## Data Grids — TanStack Table

All tabular financial data (positions, trades, claims, transactions) uses TanStack Table
rendered inside a shadcn `Table`.

```tsx
import { flexRender, getCoreRowModel, useReactTable, type ColumnDef } from '@tanstack/react-table'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatCurrency, formatPercent } from '@/lib/formatters'
import type { PortfolioPosition } from '@/types'

const columns: ColumnDef<PortfolioPosition>[] = [
  { accessorKey: 'instrumentId', header: 'Instrument' },
  { accessorKey: 'marketValue', header: 'Market Value',
    cell: ({ getValue }) => formatCurrency(getValue<number>()) },
  { accessorKey: 'weight', header: 'Weight',
    cell: ({ getValue }) => formatPercent(getValue<number>() * 100) },
]

export function PositionsTable({ data }: { data: PortfolioPosition[] }) {
  const table = useReactTable({ data, columns, getCoreRowModel: getCoreRowModel() })
  return (
    <Table>
      <TableHeader>
        {table.getHeaderGroups().map((hg) => (
          <TableRow key={hg.id}>
            {hg.headers.map((h) => (
              <TableHead key={h.id}>{flexRender(h.column.columnDef.header, h.getContext())}</TableHead>
            ))}
          </TableRow>
        ))}
      </TableHeader>
      <TableBody>
        {table.getRowModel().rows.map((row) => (
          <TableRow key={row.id}>
            {row.getVisibleCells().map((cell) => (
              <TableCell key={cell.id}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</TableCell>
            ))}
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
```

## AI Copilot — CopilotKit

- The frontend never calls Azure OpenAI directly. It talks to the **CopilotKit Node runtime
  sidecar** (see `copilot-runtime/`), which forwards actions to the C# `/api/v1/` endpoints and
  streams completions from Azure OpenAI.
- Expose backend capabilities to the copilot with `useCopilotAction`; share read context with
  `useCopilotReadable`.
- Financial guardrails apply to copilot output: include rationale + source attribution on
  recommendations, and route consequential actions (trades, rebalances) through a human gate.

```tsx
import { useCopilotAction, useCopilotReadable } from '@copilotkit/react-core'

useCopilotReadable({ description: 'Current portfolio', value: portfolio })

useCopilotAction({
  name: 'proposeRebalance',
  description: 'Draft a rebalance proposal for advisor review (requires human approval).',
  parameters: [{ name: 'rationale', type: 'string', required: true }],
  handler: async ({ rationale }) => rebalance.mutateAsync({ rationale /* ... */ }),
})
```

## API Client (`src/lib/apiClient.ts`)

```typescript
const BASE_URL = import.meta.env.VITE_BACKEND_URL ?? '' // '' → use Vite proxy in dev

export async function apiGet<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE_URL}/api/v1${path}`)
  if (!res.ok) throw new Error((await res.json())?.error?.message ?? 'Request failed')
  return res.json()
}

export async function apiPost<T, B = unknown>(path: string, body: B): Promise<T> {
  const res = await fetch(`${BASE_URL}/api/v1${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error((await res.json())?.error?.message ?? 'Request failed')
  return res.json()
}
```

## TypeScript Conventions

```typescript
// src/types/index.ts — all shared types in one place
export interface ApiError {
  code: string
  message: string
  details?: Record<string, unknown>
}

// Use discriminated unions for local state machines
type AsyncState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'success'; data: T }
  | { status: 'error'; error: ApiError }
```

No `any`. No `@ts-ignore` without a justifying comment.

## Financial Data Display (`src/lib/formatters.ts`)

- Always show currency with an explicit code via `Intl.NumberFormat`.
- Positive returns use `text-success`; negative use `text-danger`. Show the sign explicitly.
- Large numbers use compact notation for display (`1.2M`, `4.5B`).
- Dates: ISO in data, locale format in display.

```typescript
export const formatCurrency = (value: number, currency = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(value)

export const formatPercent = (value: number, decimals = 2) =>
  `${value >= 0 ? '+' : ''}${value.toFixed(decimals)}%`

export const formatCompactNumber = (value: number) =>
  new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(value)
```

## Required Pages (present in every app)

```
src/pages/
├── WorkflowPage.tsx          # System workflow visualization — ALWAYS PRESENT
├── ArchitecturePage.tsx      # Architecture diagram overview
└── SettingsPage.tsx          # App settings and configuration
```

### WorkflowPage Requirements

Implemented as an interactive graph visualization:
- Dark background using design-system tokens (`bg-background`, `bg-card`, `border-border`)
- Nodes colored by type: Service/API (info blue), AI Agent (violet/purple), Human Gate (gold/amber), Data Store (teal), Outcome (success green)
- Clicking any node opens a right-side detail panel: description, source files, responsibilities, data flow, technology tags
- Top navigation tabs for multiple workflow views; a legend for node color coding
- Dashed lines for escalation/error paths, solid lines for the happy path

See `.github/skills/workflow-visualization/SKILL.md` for the complete implementation guide.

## vite.config.ts

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: { alias: { '@': path.resolve(__dirname, './src') } },
  server: {
    port: 5173,
    proxy: {
      // REST API → C# ASP.NET Core backend
      '/api': { target: process.env.VITE_BACKEND_URL ?? 'http://localhost:8000', changeOrigin: true, ws: true },
      // CopilotKit → Node runtime sidecar
      '/copilotkit': { target: process.env.VITE_COPILOT_URL ?? 'http://localhost:4000', changeOrigin: true },
    },
  },
})
```

Use the exact file from `templates/frontend-design-system/vite.config.ts` unless the project
already defines equivalent alias/proxy behavior.
