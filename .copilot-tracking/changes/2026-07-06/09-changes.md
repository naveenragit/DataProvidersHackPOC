<!-- markdownlint-disable-file -->
# Implementation Changes: Package 09 — Frontend Shell & Navigation

**Plan:** `.copilot-tracking/plans/2026-07-06/09-plan.md`
**Package:** `implementationPlan/09-frontend-shell.md`
**Date:** 2026-07-06

Scaffolds the `frontend/` Vite app (React 18 + shadcn/ui + TanStack + CopilotKit + Tailwind) with
Prism navigation, the data layer, pages, the workflow visualization, and a vitest smoke harness.
Conforms to architecturalPlan 00 (P1 real API / honest states, P4 no trading vocab), 01, 02, 09, 10, 11.

## Repo-state deviation (pre-existing)
- The plan assumed **no `frontend/` folder existed**. In reality a **stale partial scaffold** was present:
  no `package.json`/lockfile, empty `src/**` subdirs, a leftover `node_modules` (107 entries, no manifest),
  and two `tsbuildinfo` files. Removed the stale `node_modules` + `tsbuildinfo` + the two unused empty dirs
  (`src/data`, `src/utils`) so `npm install` produces a clean, reproducible tree. All new files are net-new.

## Files Created (config — Phase 0)
| File | Summary |
|---|---|
| `frontend/package.json` | Pinned deps + scripts (dev/build/typecheck/preview/test/test:watch). |
| `frontend/tsconfig.json` | Strict app config; `@/*`→`./src`; `noEmit`; vitest/jest-dom/node types. |
| `frontend/tsconfig.node.json` | Composite config for `vite.config.ts` + `vitest.config.ts`. |
| `frontend/vite.config.ts` | Verbatim template — `@` alias + `/api`→:8000 + `/copilotkit`→:4000 proxies. |
| `frontend/tailwind.config.js` | Verbatim template — shadcn dark theme + financial tokens. |
| `frontend/postcss.config.js` | Verbatim template. |
| `frontend/components.json` | Verbatim template — shadcn new-york config. |
| `frontend/index.html` | Copied + `<title>` → `Prism · Rating Reconciliation`; kept `class="dark"`. |
| `frontend/.gitignore` | node_modules/dist/coverage/.env/*.tsbuildinfo. |
| `frontend/vitest.config.ts` | Separate jsdom config (globals, setup file); template `vite.config.ts` kept pristine. |
| `frontend/src/test/setup.ts` | `import '@testing-library/jest-dom'`. |

## Files Created (data layer — Phase 5)
| File | Summary |
|---|---|
| `frontend/src/types/prism.ts` | Inferred DTO mirror (camelCase): `Provider`/`Bucket`/`RedFlagCode`/`Severity`, `IssuerListItem`, `ProviderVerdictDto`, `BucketAttributionDto`, `PairDivergenceDto`, `RedFlagDto`, `DossierResponse`, `ReconciliationRequest`, `ApiErrorBody`. Re-sync when pkg08 `PrismDtos.cs` lands. |
| `frontend/src/lib/apiClient.ts` | Typed fetch on `/api/v1`; `ApiError` class; parses `{error:{code,message,details}}` and throws on non-2xx (no fabricated fallback — P1). |
| `frontend/src/hooks/useIssuers.ts` | `useQuery(['issuers'])` → real `GET /api/v1/issuers`. |
| `frontend/src/hooks/useReconciliation.ts` | `useReconciliationRun()` mutation + `useDossier(id?)` query (disabled until id). |

## Files Created (shadcn primitives — Phase 3, hand-authored, no CLI)
| File | Summary |
|---|---|
| `frontend/src/components/ui/button.tsx` | `Button` + `buttonVariants` (cva, `asChild` via Radix Slot). |
| `frontend/src/components/ui/card.tsx` | `Card`/`CardHeader`/`CardTitle`/`CardDescription`/`CardContent`/`CardFooter`. |
| `frontend/src/components/ui/badge.tsx` | `Badge` + `badgeVariants`. |
| `frontend/src/components/ui/input.tsx` | `Input` (forwardRef). |
| `frontend/src/components/ui/label.tsx` | `Label` on `@radix-ui/react-label`. |
| `frontend/src/components/ui/table.tsx` | `Table`/`TableHeader`/`TableBody`/`TableFooter`/`TableRow`/`TableHead`/`TableCell`/`TableCaption`. |
| `frontend/src/components/ui/dialog.tsx` | shadcn Dialog on `@radix-ui/react-dialog` (lucide `X`). |
| `frontend/src/components/ui/tabs.tsx` | shadcn Tabs on `@radix-ui/react-tabs`. |
| `frontend/src/components/ui/tooltip.tsx` | shadcn Tooltip on `@radix-ui/react-tooltip`. |
| `frontend/src/components/ui/select.tsx` | shadcn Select on `@radix-ui/react-select` (lucide `Check`/`ChevronDown`/`ChevronUp`). |

## Files Created (layout/lib/providers — Phases 6 & 4)
| File | Summary |
|---|---|
| `frontend/src/main.tsx` | Verbatim — mounts `<App/>`, imports `./index.css`. |
| `frontend/src/index.css` | Verbatim — Tailwind directives + shadcn dark tokens. |
| `frontend/src/lib/utils.ts` | Verbatim — `cn()`. |
| `frontend/src/lib/queryClient.ts` | Verbatim — TanStack Query client. |
| `frontend/src/lib/settings.ts` | `PrismSettings` + `loadSettings`/`saveSettings` (localStorage), agent/provider/model catalogs. |
| `frontend/src/components/ui/PageHeader.tsx` | Verbatim. |
| `frontend/src/components/ui/StatCard.tsx` | Verbatim. |
| `frontend/src/components/layout/AppLayout.tsx` | Verbatim — Sidebar + TopBar + `<Outlet/>`. |
| `frontend/src/components/layout/Sidebar.tsx` | Edited — Prism `NAV_GROUPS` (Prism/Architecture/Settings), brand "Prism/Reconciliation/P", lucide `Building2`/`Scale`/`GitBranch`/`Network`/`Settings`. |
| `frontend/src/components/layout/TopBar.tsx` | Edited — Prism `TITLES` map, default `Prism`, badge "Prism · Azure AI Foundry". |
| `frontend/src/App.tsx` | Authored — `QueryClientProvider`→`CopilotKit`→`BrowserRouter`; 6 routes; index→`/issuers`; CopilotSidebar rebranded "Prism Copilot". |

## Files Created (pages — Phase 7)
| File | Summary |
|---|---|
| `frontend/src/pages/IssuersPage.tsx` | `useIssuers()` + **TanStack Table**; explicit loading/error/empty/success (no fabricated rows); "Reconcile" deep-link. |
| `frontend/src/pages/ReconciliationPage.tsx` | Placeholder shell reading `?issuer=`; no mock dossier (full UI = pkg10). |
| `frontend/src/pages/WorkflowPage.tsx` | Copied + 3 imports fixed → `@/components/workflow/{WorkflowDiagram,workflowData,workflowTypes}`. |
| `frontend/src/pages/ArchitecturePage.tsx` | Static summary (agents, deterministic engines, Azure services, data sources); P2/P4 framing. |
| `frontend/src/pages/SettingsPage.tsx` | Model-per-agent (Select), provider toggles, default as-of (`Input type=date`), read-only endpoints, feature flag; persists to localStorage. |

## Files Created (workflow visualization — Phase 8)
| File | Summary |
|---|---|
| `frontend/src/components/workflow/workflowTypes.ts` | Verbatim. |
| `frontend/src/components/workflow/WorkflowNode.tsx` | Verbatim. |
| `frontend/src/components/workflow/WorkflowDetailPanel.tsx` | Verbatim. |
| `frontend/src/components/workflow/WorkflowDiagram.tsx` | Verbatim. |
| `frontend/src/components/workflow/workflowData.ts` | **Rewritten** to a minimal Prism `rating-reconciliation` tab — 8 P4-clean nodes (issuer-selected → reconciliation-orchestrator → provider-explainer-agent + fundamentals-agent → divergence-decomposer → red-flag-engine → confirm-scope-gate → dossier-ready), 8 edges. Sample "meeting-intelligence"/"recommendation" content removed. |

## Files Created (tests — Phase 11)
| File | Summary |
|---|---|
| `frontend/src/components/layout/Sidebar.test.tsx` | Renders 3 nav groups + 5 nav links (fails if a group is dropped). |
| `frontend/src/lib/apiClient.test.ts` | 2xx returns typed body; non-2xx throws `ApiError` with `.code==='NOT_FOUND'` + `.status`. |

**Totals:** ~40 files created; **0 pre-existing files modified**; workflow nodes added: **8** (1 minimal Prism tab).

## Dependency-pin adjustment (approved D1 — patch bump only)
- **`@copilotkit/react-core` and `@copilotkit/react-ui`: `1.4.4` → `1.4.8`.**
  Root cause: the published `1.4.4` (and `1.4.5`) tarballs are **broken — they ship only `src/`, no `dist/`**
  (no compiled JS / `.d.ts`), while `package.json` points `main`/`types` at `./dist/*`. This is a hard blocker
  (not a peer conflict): `tsc` → `TS2307 Cannot find module '@copilotkit/react-core'`, and `vite` can't resolve
  the runtime entry. Probed nearest patches via `npm pack --dry-run`: `1.4.5` broken; **`1.4.8` ships `dist`**
  (as do 1.5.x/1.6.x/1.8.x). Chose `1.4.8` = nearest working patch, same 1.4.x minor (minimal drift).
- **No `--legacy-peer-deps` needed** — `npm install` completed with zero peer-dependency errors.
- All other pins resolved **exactly** as specified.

## Acceptance gate — exact results (from `C:\GitHub\DataProvidersHackPOC\frontend`)
1. **`npm install`** → OK. `added 502 packages` (clean tree after 1.4.8 bump). No peer-dep error; no `--legacy-peer-deps`.
   Deprecation warnings only (recharts 2.x, uuid 10, whatwg-encoding). `npm audit`: 15 vulns (10 mod/4 high/1 crit) in transitive deps — not part of the build/test gate.
2. **`npm run build`** (`tsc --noEmit && vite build`) → **GREEN**. Zero TS errors under strict mode.
   `✓ 3518 modules transformed`; `dist/assets/index-*.js 1,388.89 kB` (gzip 482 kB); `built in 7.19s`.
   Benign warnings only: node-fetch Node-builtin externalization (from CopilotKit transitive deps) + chunk-size > 500 kB hint.
3. **`npm run test`** (`vitest run`) → **GREEN**. `Test Files 2 passed (2)`, `Tests 4 passed (4)` (apiClient 2 + Sidebar 2). Duration 1.77s.

## Final resolved versions (top-level)
react@18.3.1 · react-dom@18.3.1 · react-router-dom@6.26.2 · @tanstack/react-query@5.59.15 ·
@tanstack/react-table@8.20.5 · **@copilotkit/react-core@1.4.8** · **@copilotkit/react-ui@1.4.8** ·
lucide-react@0.451.0 · recharts@2.12.7 · @radix-ui/react-{slot@1.1.0,label@2.1.0,dialog@1.1.2,tabs@1.1.1,tooltip@1.1.3,select@2.1.2} ·
vite@5.4.9 · @vitejs/plugin-react@4.3.2 · typescript@5.6.3 · tailwindcss@3.4.14 · vitest@2.1.3 · @testing-library/react@16.0.1.

## Deviations / residual risks (deferred, honest)
- **CopilotKit 1.4.8 patch bump** (above) — the only dependency change vs the plan's pins.
- **`types/prism.ts` is INFERRED** (pkg08 `PrismDtos.cs` not built) — must re-sync names/shapes when pkg08 lands (contract-sync rule, arch-09).
- **Hooks call the real `/api/v1`**, which pkg08 has not implemented yet → the Issuers grid will render its honest **error** state at runtime (by design — P1, no mock data). Visual/`npm run dev` verification is out of the automatable gate.
- **CopilotKit sidebar** is present + rebranded but the `/copilotkit` sidecar (:4000, pkg07) is absent → the copilot panel won't respond at runtime; build/test unaffected (D3).
- **Workflow tab is the minimal seed** (D4) — pkg10 enriches node details/positions and the full annotated pipeline.
- **`recharts` pinned but unused** in pkg09 (D10) — pkg10 waterfall consumes it.
- **Transitive-dependency vulnerabilities** (15) from CopilotKit's dep graph (e.g. node-fetch) — not remediated here to avoid destabilizing the pinned tree; revisit alongside pkg10.

---

## Corrections (post-adversarial-review) — 2026-07-06

Surgical fixes applied to the pkg09 shell after the adversarial security/architecture review.
No mock data introduced (P1); no P4 vocabulary. `npm run build` (strict `tsc --noEmit` + `vite build`)
and `npm run test` remained GREEN throughout.

| # | Fix | Severity | Files | Status |
|---|---|---|---|---|
| 1 | Mount `<TooltipProvider delayDuration={200}>` once inside `<CopilotKit>` (wraps `<BrowserRouter>` + sidebar) — Radix Tooltip throws without a provider ancestor (prevents a pkg-10 runtime crash). | High | `src/App.tsx` | Done |
| 2 | Top-level `ErrorBoundary` (class; `getDerivedStateFromError` + `componentDidCatch`) wrapping `<Routes>`; shadcn fallback card + reload action; raw error/stack logged to console only, never rendered (arch-10 / SEC-03). | Medium | `src/components/ErrorBoundary.tsx` (new), `src/App.tsx` | Done |
| 3 | CopilotKit `runtimeUrl` env-aware: `import.meta.env.VITE_COPILOT_URL ?? '/copilotkit'`; documented `VITE_COPILOT_URL` in a new `.env.example`; added `src/vite-env.d.ts` typing the var (tsconfig pins `types`, so `vite/client` wasn't otherwise included). | Medium | `src/App.tsx`, `frontend/.env.example` (new), `src/vite-env.d.ts` (new) | Done |
| 4 | `apiClient` hardening: (a) header merge now spreads `...init` FIRST and sets `headers` LAST (via `Headers`, defaulting `Content-Type` only when absent) so a caller can't drop it; (b) module-level `setAuthTokenProvider(fn)` seam injecting `Authorization: Bearer <token>` only when a provider returns one — no hardcoded token; default = no auth (AllowAnonymous reads). Comment marks pkg 07/08 MSAL wiring point (SEC-01 seed). | High/Low | `src/lib/apiClient.ts` | Done |
| 5 | `loadSettings` whitelist validation: `providers` subset of {Moodys, MorningstarDbrs, Msci} (canonical order, tampered dropped); `models` keys constrained to known agent keys AND value in MODEL_OPTIONS; `defaultAsOf` a real ISO `yyyy-mm-dd` date (else default); unknown/invalid fields dropped per-field; corrupt-JSON catch kept (SEC-02). | Medium | `src/lib/settings.ts` | Done |
| 6 | `IssuersPage` error state renders the stable `error.code` (via `instanceof ApiError`, else `UNKNOWN`) + a generic "Couldn't load issuers." message — never the raw `error.message`; `error.details` never rendered (SEC-03). | Medium | `src/pages/IssuersPage.tsx` | Done |
| 7 | TopBar Prism badge uses theme tokens `border-primary/40 text-primary` (was leftover `brand-gold`). | Low | `src/components/layout/TopBar.tsx` | Done |
| 8 | SettingsPage a11y: where `<Label htmlFor>` targeted a non-labelable Radix `SelectTrigger` / shadcn `Button`, switched to `aria-labelledby` (Label carries the `id`) so the association is valid. Native `<Input>` labels left as `htmlFor`. | Low | `src/pages/SettingsPage.tsx` | Done |
| 9 | Removed the dashed "Escalation path" legend entry in `WorkflowPage` (the pkg-09 Prism edges have no `dashed:true`); pkg 10 re-adds it with a real escalation edge. | Low | `src/pages/WorkflowPage.tsx` | Done |
| 10 | Bumped `react-router-dom` `6.26.2` -> `6.30.4` (pinned exact) — clears all 3 runtime **high** advisories (@remix-run/router / react-router / react-router-dom chain). Build + test stayed green. | Security (runtime high) | `frontend/package.json`, `package-lock.json` | Done |
| 11 | Tests: new `settings.test.ts` (valid roundtrip; corrupt-JSON->defaults; tampered-provider rejected; unknown model key / invalid model / bad as-of dropped) + apiClient tests (non-JSON error body -> `UNKNOWN`; 204 no-content -> `undefined`; `apiPost` POSTs body + returns typed). | arch-11 | `src/lib/settings.test.ts` (new), `src/lib/apiClient.test.ts` | Done |

**Verification (from `C:\GitHub\DataProvidersHackPOC\frontend`):**
- `npm run build` (`tsc --noEmit && vite build`) -> **GREEN** (exit 0). Zero TS errors under strict mode; only benign node-fetch browser-externalize + chunk-size > 500 kB warnings.
- `npm run test` (`vitest run`) -> **GREEN** (exit 0). **Test Files 3 passed; Tests 12 passed** (apiClient 5 + settings 5 + Sidebar 2; up from 4). React Router v7 future-flag console warnings appear (informational only; tests pass).
- `react-router-dom` final version: **6.30.4**.
- `npm audit --omit=dev` (runtime) after bump: **0 critical / 0 high**, 9 moderate remaining (CopilotKit `prismjs`/`react-syntax-highlighter` DOM-clobbering chain — only executes when the pkg07 sidecar is live). Was 3 high pre-bump.

## Residual risks (deferred)
- **Full MSAL auth wiring (pkg 07/08).** `setAuthTokenProvider` seam is in place but no provider is registered -> reads go out AllowAnonymous by design. pkg 07/08 registers the real MSAL access-token accessor here.
- **CSP / SRI / self-hosted fonts (pkg 11).** No Content-Security-Policy header and Google Fonts loaded without SRI — deferred to the deployment package.
- **Workflow-tab shadcn `Tabs` rewrite + raw-slate tokens (pkg 10).** `WorkflowPage` still uses hand-rolled tab buttons and `slate-*` utility classes rather than shadcn `Tabs` + theme tokens.
- **Bundle lazy-load (pkg 10).** Single ~1.4 MB JS chunk (CopilotKit-dominated); route-level `React.lazy` / `manualChunks` deferred.
- **`types/prism.ts` DTO re-sync + `JsonStringEnumConverter` requirement (pkg 08).** DTOs remain INFERRED; must re-sync to `PrismDtos.cs` and confirm the backend serializes `Provider`/enum values as strings.
- **React Router v7 future-flag warnings.** 6.30.x emits `v7_startTransition` / `v7_relativeSplatPath` console warnings; opting into the flags is a routing-behavior change deferred to pkg 10.
- **9 moderate transitive advisories** remain from CopilotKit's `prismjs`/`react-syntax-highlighter` chain (sidecar-gated); dev-only vitest (critical) / vite (high) / esbuild advisories intentionally left per scope.
