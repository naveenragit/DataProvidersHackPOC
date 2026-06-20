---
description: "React + TypeScript + Tailwind frontend standards for financial services applications"
applyTo: "**/*.tsx, **/*.ts"
---

# React Frontend Standards — Financial Services

## Reference Design

The target look is a **modern, professional dark dashboard** (Inter font, `#0f1117`
background, rounded cards, indigo accent, left sidebar). Ready-to-copy design-system files
live in `templates/frontend-design-system/`. Use them verbatim — do not hand-roll the theme.

## Mandatory Tailwind Setup (do this FIRST — non-negotiable)

A page that renders as white background, serif fonts, and plain browser inputs means
**Tailwind was never wired up**. This is the #1 frontend failure. Before writing any page,
ensure ALL of the following exist, or the UI will be unstyled raw HTML:

1. Runtime deps installed: `npm install react-router-dom lucide-react clsx`
2. Dev deps installed: `npm install -D tailwindcss@^3 postcss autoprefixer @vitejs/plugin-react`
3. `frontend/index.html` includes `class="dark"`, Google Fonts (Inter + JetBrains Mono), and body classes `bg-surface text-gray-100 antialiased` (copy `templates/frontend-design-system/index.html`).
4. `frontend/vite.config.ts` includes `@` alias (`./src`) and `/api` proxy (copy `templates/frontend-design-system/vite.config.ts`).
5. `frontend/tailwind.config.js` exists with `content: ['./index.html', './src/**/*.{ts,tsx}']`
   and the theme tokens (copy `templates/frontend-design-system/tailwind.config.js`).
6. `frontend/postcss.config.js` exists with the `tailwindcss` + `autoprefixer` plugins.
7. `frontend/src/index.css` starts with `@tailwind base; @tailwind components; @tailwind utilities;`
   and defines the shared component classes (copy `templates/frontend-design-system/index.css`).
8. `frontend/src/main.tsx` contains `import './index.css'`.
9. `frontend/src/App.tsx` routes are mounted inside `AppLayout`.
10. `frontend/src/components/layout/AppLayout.tsx`, `Sidebar.tsx`, and `TopBar.tsx` are copied from templates.

**Verification gate:** run `npm run dev` and confirm a dark `#0f1117` background, Inter font,
a grouped left sidebar (`w-60`), a top bar (`h-16`), and rounded cards render. If not, fix the configuration above before proceeding.

## Technology Stack

- **React 18+** with functional components and hooks
- **TypeScript** (strict mode, `"strict": true`)
- **Vite** as build tool
- **Tailwind CSS v3** for all styling (configured per the mandatory setup above)
- **React Router v6** for navigation
- **lucide-react** for all icons
- **clsx** for conditional class names
- **axios** or native `fetch` for API calls
- **recharts** for financial charts and analytics

## Design System Color Tokens

Use the **semantic Tailwind tokens** defined in `tailwind.config.js` — not raw slate classes
and not inline hex. Prefer the component classes (`.card`, `.btn-primary`, `.input`, etc.).

| Token | Tailwind Class | Hex | Use |
|---|---|---|---|
| Background | `bg-surface-200` | `#0f1117` | App body, main area |
| Surface raised | `bg-surface-100` | `#141824` | Sidebar |
| Surface hover | `bg-surface-50` | `#1a1f2e` | Hover, inputs |
| Card | `bg-card` / `.card` | `#1a1f2e` | Cards, panels |
| Border | `border-border` | `#2a3040` | All borders |
| Accent | `bg-accent` / `text-accent` | `#6366f1` | CTA buttons, active nav |
| Accent hover | `accent-hover` | `#818cf8` | Hover/active accent |
| Success | `text-status-success` | `#22c55e` | Positive returns, active |
| Warning / gate | `text-status-warning` / `brand-gold` | `#f59e0b` | Human-in-the-loop gates |
| Danger | `text-status-error` | `#ef4444` | Errors, negative returns |
| Info / service | `text-status-info` | `#3b82f6` | Info, service nodes |
| Datastore | `brand-teal` | `#14b8a6` | Data store nodes |
| Primary text | `text-gray-100` | — | Headings, active labels |
| Secondary text | `text-gray-400` | — | Subtitles |
| Muted text | `text-gray-500` | — | Placeholders, metadata |

### Shared component classes (from `index.css`)

Use these instead of repeating utility chains: `.card`, `.btn-primary`, `.btn-secondary`,
`.btn-ghost`, `.input`, `.badge-success|warning|error|info|accent|gold`, `.section-title`,
`.stat-card`, `.stat-label`, `.stat-value`, `.stat-delta`.

## App Layout with Sidebar

Every app uses a two-column layout: fixed sidebar + scrollable main content.

### Sidebar Navigation Structure

```tsx
// src/components/layout/Sidebar.tsx
import { NavLink } from 'react-router-dom'
import { LucideIcon } from 'lucide-react'

interface NavGroup {
  label: string
  items: NavItem[]
}

interface NavItem {
  to: string
  icon: LucideIcon
  label: string
}

// Required navigation groups (always present in every app):
const ARCHITECTURE_GROUP: NavGroup = {
  label: 'Architecture',
  items: [
    { to: '/workflow', icon: GitBranch, label: 'Workflow' },
    { to: '/architecture', icon: Layers, label: 'Architecture' },
  ],
}

const SETTINGS_GROUP: NavGroup = {
  label: 'Settings',
  items: [
    { to: '/settings', icon: Settings, label: 'Settings' },
  ],
}
```

### App Layout Component

```tsx
// src/components/layout/AppLayout.tsx
import { Outlet } from 'react-router-dom'
import Sidebar from './Sidebar'
import TopBar from './TopBar'

export default function AppLayout() {
  return (
    <div className="flex h-screen overflow-hidden bg-surface text-gray-100">
      <Sidebar />
      <div className="flex flex-col flex-1 min-w-0 overflow-hidden">
        <TopBar />
        <main className="flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
```

## Component Patterns

Prefer the shared classes from `index.css` over ad-hoc utility chains. This keeps every
screen visually consistent with the reference app:

- Cards/panels: use `.card`
- Inputs: use `.input`
- Buttons: use `.btn-primary`, `.btn-secondary`, `.btn-ghost`
- Badges: use `.badge` plus `.badge-success|warning|error|info|accent|gold`
- Section headers: use `.section-title`
- KPI tiles: use `.stat-card`, `.stat-label`, `.stat-value`, `.stat-delta`

### Canonical Dashboard Composition

Copy and adapt `templates/frontend-design-system/DashboardPage.tsx` as your baseline for
new pages. It establishes the exact composition that delivers the professional look:

1. `PageHeader` block with title + muted subtitle
2. KPI grid using `StatCard`
3. Two-column card row (`.card`) with list rows using hover surfaces (`bg-surface-50`)
4. Accent links/actions (`text-accent`, `hover:text-accent-hover`)

## Pages Architecture

### Required Pages (present in every app)

```
src/pages/
├── WorkflowPage.tsx          # System workflow visualization — ALWAYS PRESENT
├── ArchitecturePage.tsx      # Architecture diagram overview
└── SettingsPage.tsx          # App settings and configuration
```

### WorkflowPage Requirements

The Workflow page **must** be implemented as an interactive graph visualization:
- Dark background using design-system tokens (`bg-surface`, `bg-surface-100`, `border-border`)
- Nodes colored by type: Service/API (info blue), AI Agent (violet/purple), Human Gate (brand gold/amber), Data Store (brand teal), Outcome (success green)
- Clicking any node opens a right-side detail panel with: description, source files, responsibilities, data flow, technology tags
- Navigation tabs at the top for multiple workflow views (e.g., Meeting Intelligence, Portfolio Intelligence)
- Legend showing node type color coding
- Dashed lines for escalation/error paths, solid lines for happy path

See `.github/skills/workflow-visualization/SKILL.md` for the complete implementation guide.

### SettingsPage Structure

```tsx
// src/pages/SettingsPage.tsx
import PageHeader from '@/components/ui/PageHeader'

export default function SettingsPage() {
  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <PageHeader
        title="Settings"
        subtitle="Configure AI models, endpoints, and feature flags"
      />

      <div className="card">
        <div className="section-title">Azure AI Configuration</div>
        {/* Model selection, endpoint URLs */}
      </div>

      <div className="card">
        <div className="section-title">Feature Flags</div>
        {/* Toggle switches for experimental features */}
      </div>

      <div className="card">
        <div className="section-title">About</div>
        {/* Version, build info */}
      </div>
    </div>
  )
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

export interface ApiResponse<T> {
  data: T
  meta?: {
    total?: number
    page?: number
    pageSize?: number
  }
}

// Use discriminated unions for state
type AsyncState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'success'; data: T }
  | { status: 'error'; error: ApiError }
```

## API Client Pattern

```typescript
// src/utils/apiClient.ts
const BASE_URL = import.meta.env.VITE_BACKEND_URL ?? 'http://localhost:8000'

export async function apiGet<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE_URL}/api/v1${path}`)
  if (!res.ok) {
    const error = await res.json()
    throw new Error(error?.error?.message ?? 'Request failed')
  }
  return res.json()
}

export async function apiPost<T, B = unknown>(path: string, body: B): Promise<T> {
  const res = await fetch(`${BASE_URL}/api/v1${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) {
    const error = await res.json()
    throw new Error(error?.error?.message ?? 'Request failed')
  }
  return res.json()
}
```

## Financial Data Display

- Always show currency with explicit currency code: `$1,234.56 USD` or use `Intl.NumberFormat`
- Positive returns: `text-green-400`, negative returns: `text-red-400`
- Percentage changes: show sign explicitly (`+2.4%`, `-1.1%`)
- Large numbers: use compact notation for display (`1.2M`, `4.5B`)
- Dates: ISO format in data, locale format in display

```typescript
// src/utils/formatters.ts
export const formatCurrency = (value: number, currency = 'USD') =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency }).format(value)

export const formatPercent = (value: number, decimals = 2) => {
  const sign = value >= 0 ? '+' : ''
  return `${sign}${value.toFixed(decimals)}%`
}

export const formatCompactNumber = (value: number) =>
  new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(value)
```

## vite.config.ts

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_BACKEND_URL ?? 'http://localhost:8000',
        changeOrigin: true,
        ws: true,
      },
    },
  },
})
```

Use the exact file from `templates/frontend-design-system/vite.config.ts` unless the project
already defines equivalent alias/proxy behavior.
