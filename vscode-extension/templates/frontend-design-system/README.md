# Frontend Design System (shadcn/ui + TanStack + CopilotKit)

Known-good, copy-paste files that produce the **modern, professional dark financial UI** on the
required stack: **React 18 + Vite + shadcn/ui + TanStack Query/Table + CopilotKit + Tailwind CSS**.
Use them **verbatim** — they are the difference between a polished dashboard and unstyled raw HTML.

## What the result looks like

- Full-height app shell: fixed **sidebar (`w-60`)** + **top bar (`h-16`)** + scrollable content
- Dark theme by default via shadcn CSS variables (background ~`#0f1117`, card ~`#1a1f2e`,
  border ~`#2a3040`, **indigo/violet `primary`**)
- **Inter** UI font, **JetBrains Mono** for numbers/code
- Pages built from shadcn `Card`, `Badge`, `Table`, `Button`, `Input` primitives + `StatCard`

## Setup checklist (do this BEFORE building any page)

1. Install runtime deps:
   ```
   npm install react-router-dom lucide-react class-variance-authority clsx tailwind-merge \
     @tanstack/react-query @tanstack/react-table @copilotkit/react-core @copilotkit/react-ui
   ```
2. Install dev deps: `npm install -D tailwindcss@^3 postcss autoprefixer @vitejs/plugin-react tailwindcss-animate`
3. Initialize shadcn/ui (dark base color, CSS variables): `npx shadcn@latest init`
4. Add the primitives used by the templates:
   `npx shadcn@latest add card badge button input textarea label table dialog`
5. Copy `index.html`, `vite.config.ts`, `tailwind.config.js`, `postcss.config.js`, `components.json` to `frontend/`
6. Copy `index.css`, `main.tsx`, `App.tsx` to `frontend/src/`; copy `lib/` (`utils.ts`, `queryClient.ts`) to `frontend/src/lib/`
7. Copy the `layout/` (`AppLayout`, `Sidebar`, `TopBar`) and `ui/` (`PageHeader`, `StatCard`) components into `frontend/src/components/`
8. Copy `DashboardPage.tsx` into `frontend/src/pages/` and route to it
9. Run `npm run dev` and **visually verify**: dark background, Inter font, grouped left sidebar,
   top bar, rounded shadcn cards, and the CopilotKit sidebar toggle. If you see a **white page
   with serif fonts and plain inputs**, Tailwind/shadcn is not wired — fix steps 2–6 first.

## Files — copy ALL of them

| File | Copy to | Purpose |
|---|---|---|
| `index.html` | `frontend/index.html` | `class="dark"` + Inter/JetBrains font links |
| `vite.config.ts` | `frontend/vite.config.ts` | `@`→`./src` alias + `/api` (C# backend) and `/copilotkit` (Node sidecar) proxies |
| `tailwind.config.js` | `frontend/tailwind.config.js` | shadcn dark theme + financial semantic tokens |
| `postcss.config.js` | `frontend/postcss.config.js` | Lets Tailwind compile (required) |
| `components.json` | `frontend/components.json` | shadcn/ui config |
| `index.css` | `frontend/src/index.css` | `@tailwind` directives + shadcn CSS variables (`:root` / `.dark`) |
| `lib/utils.ts` | `frontend/src/lib/utils.ts` | shadcn `cn()` helper |
| `lib/queryClient.ts` | `frontend/src/lib/queryClient.ts` | TanStack Query client |
| `main.tsx` | `frontend/src/main.tsx` | Imports `index.css` (without this, no styles load) |
| `App.tsx` | `frontend/src/App.tsx` | `QueryClientProvider` + `CopilotKit` + Router + `AppLayout` |
| `AppLayout.tsx` | `frontend/src/components/layout/AppLayout.tsx` | Sidebar + TopBar + scrollable `<main>` |
| `Sidebar.tsx` | `frontend/src/components/layout/Sidebar.tsx` | Grouped sidebar; feature + required Architecture + Settings |
| `TopBar.tsx` | `frontend/src/components/layout/TopBar.tsx` | Page title + platform badge + status pill |
| `PageHeader.tsx` | `frontend/src/components/ui/PageHeader.tsx` | Standard page title + subtitle block |
| `StatCard.tsx` | `frontend/src/components/ui/StatCard.tsx` | KPI tile (shadcn `Card`) for dashboard grids |
| `DashboardPage.tsx` | `frontend/src/pages/DashboardPage.tsx` | Canonical page composition to copy |
| `ExampleFormPage.tsx` | reference only | Correct use of shadcn `Card`/`Input`/`Button` + a TanStack mutation |

## Design tokens (use the shadcn semantic classes, not raw hex)

- Background: `bg-background` · Card/sidebar/top bar: `bg-card` · Popover/detail: `bg-popover`
- Borders: `border-border` · Muted surface/hover: `bg-muted` · Muted text: `text-muted-foreground`
- Primary/accent: `bg-primary` / `text-primary` (indigo/violet)
- Financial semantics: `text-success` / `text-danger` (returns), `text-warning` / `brand-gold`
  (human gates), `text-info` (services), `brand-teal` (data stores)
- Text: `text-foreground` primary, `text-muted-foreground` secondary
- Fonts: `font-sans` (Inter), `font-mono` (JetBrains Mono)

## Stack conventions

- **shadcn/ui** for every primitive — do not hand-roll buttons, dialogs, inputs, or tables.
- **TanStack Query** for all server state (no `useEffect` fetching); **TanStack Table** for grids.
- **CopilotKit** wraps the app in `App.tsx` and targets the Node runtime sidecar via
  `runtimeUrl="/copilotkit"` (proxied by Vite). The browser never calls Azure OpenAI directly.

## Page recipe (every page follows this)

```tsx
import { PageHeader } from '@/components/ui/PageHeader'
import { StatCard } from '@/components/ui/StatCard'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

<div className="max-w-7xl space-y-6">
  <PageHeader title="..." subtitle="..." />
  <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">{/* StatCards */}</div>
  <Card>
    <CardHeader><CardTitle>Section</CardTitle></CardHeader>
    <CardContent>{/* content */}</CardContent>
  </Card>
</div>
```
