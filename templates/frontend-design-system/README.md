# Frontend Design System (required — derived from a production reference app)

Known-good, copy-paste files that produce the **modern, professional dark financial UI**.
These were distilled from a real production wealth-advisor platform. Use them **verbatim** —
they are the difference between a polished dashboard and unstyled raw HTML.

## What the result looks like

- Full-height app shell: fixed dark **sidebar (`w-60`)** + **top bar (`h-16`)** + scrollable content
- Background `#0f1117`, cards `#1a1f2e`, borders `#2a3040`, **indigo accent `#6366f1`**
- **Inter** UI font, **JetBrains Mono** for code/numbers
- Pages built from `stat-card` KPI grids, `.card` panels with `.section-title`, and `.badge-*` pills

## Files — copy ALL of them

| File | Copy to | Purpose |
|---|---|---|
| `index.html` | `frontend/index.html` | `class="dark"` + Inter/JetBrains font links + `bg-surface` body |
| `vite.config.ts` | `frontend/vite.config.ts` | `@` → `./src` alias + `/api` proxy to backend |
| `tailwind.config.js` | `frontend/tailwind.config.js` | Theme tokens (surface, card, border, accent, brand, status) + fonts |
| `postcss.config.js` | `frontend/postcss.config.js` | Lets Tailwind compile (required) |
| `index.css` | `frontend/src/index.css` | `@tailwind` directives + base + component classes |
| `main.tsx` | `frontend/src/main.tsx` | Imports `index.css` (without this, no styles load) |
| `App.tsx` | `frontend/src/App.tsx` | Routes mounted inside `AppLayout` |
| `AppLayout.tsx` | `frontend/src/components/layout/AppLayout.tsx` | Sidebar + TopBar + scrollable `<main>` |
| `Sidebar.tsx` | `frontend/src/components/layout/Sidebar.tsx` | Dark grouped sidebar; feature + required Architecture + Settings groups |
| `TopBar.tsx` | `frontend/src/components/layout/TopBar.tsx` | Page title + platform badge + status pills |
| `PageHeader.tsx` | `frontend/src/components/ui/PageHeader.tsx` | Standard page title + subtitle block |
| `StatCard.tsx` | `frontend/src/components/ui/StatCard.tsx` | KPI tile for dashboard grids |
| `DashboardPage.tsx` | `frontend/src/pages/DashboardPage.tsx` | Canonical page layout to copy the look from |
| `ExampleFormPage.tsx` | reference only | Correct use of `.card`, `.input`, `.btn-primary` |

## Setup checklist (do this BEFORE building any page)

1. `npm install react-router-dom lucide-react clsx`
2. `npm install -D tailwindcss@^3 postcss autoprefixer @vitejs/plugin-react`
3. Copy `index.html`, `vite.config.ts`, `tailwind.config.js`, `postcss.config.js` to `frontend/`
4. Copy `index.css` + `main.tsx` + `App.tsx` to `frontend/src/`
5. Copy the `layout/` and `ui/` components into `frontend/src/components/`
6. Copy `DashboardPage.tsx` into `frontend/src/pages/` and route to it
7. Run `npm run dev` and **visually verify**: dark `#0f1117` background, Inter font, a left
   sidebar with grouped nav, a top bar, and rounded cards. If you see a **white page with
   serif fonts and plain inputs**, Tailwind is not wired — fix steps 2–4 before continuing.

## Design tokens (use the semantic classes, not raw hex)

- Background: `bg-surface` / `bg-surface-200` (#0f1117) · Raised: `bg-surface-100` (#141824)
- Cards: `bg-card` / `.card` (#1a1f2e) · Hover/inputs: `bg-surface-50` (#1a1f2e)
- Borders: `border-border` (#2a3040)
- Accent: `bg-accent` / `text-accent` (indigo #6366f1, hover `accent-hover` #818cf8)
- Brand: `brand-gold` (#f59e0b), `brand-teal` (#14b8a6)
- Status: `text-status-success|warning|error|info`
- Text: `text-gray-100` primary, `text-gray-300/400` secondary, `text-gray-500/600` muted
- Fonts: `font-sans` (Inter), `font-mono` (JetBrains Mono)

## Component classes (from `index.css`) — prefer these over utility chains

`.card`, `.btn-primary`, `.btn-secondary`, `.btn-ghost`, `.input`, `.section-title`,
`.badge` + `.badge-success|warning|error|info|accent|gold`,
`.stat-card`, `.stat-label`, `.stat-value`, `.stat-delta`.

## Page recipe (every page follows this)

```tsx
<div className="space-y-6 max-w-7xl">
  <PageHeader title="..." subtitle="..." />
  <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">{/* StatCards */}</div>
  <div className="card">
    <div className="section-title">Section</div>
    {/* content */}
  </div>
</div>
```
