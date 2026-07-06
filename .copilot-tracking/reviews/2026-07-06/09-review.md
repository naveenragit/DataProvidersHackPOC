# Package 09 — Adversarial Review (consolidated)
*2026-07-06 · Prism automated build workflow*

Reviewers: **Prism Standards Adversary**, **Fin Adversary Stack Critic**, **Fin Adversary Security**
(full attack surface for a browser SPA + supply chain). Architect deferred to pkg 07/10 (UI/orchestration topology).

## Verdict
**PASS.** `npm run build` (strict `tsc --noEmit` + `vite build`) green, **12 vitest passing**, runtime
`npm audit` **0 critical / 0 high**. No Critical/High core violations: P1 (no mock, fail-loud) and P4
(no buy/sell language) are genuinely clean; required nav + rebrand + dark-theme shell all hold.

## Findings & resolution

| Sev | Finding | Resolution |
|---|---|---|
| High | `TooltipProvider` never mounted → first `<Tooltip>` in pkg 10 crashes at runtime (tsc can't catch) | **Fixed** — wrapped once in `App.tsx` |
| Medium | No top-level ErrorBoundary (arch-10) | **Fixed** — `ErrorBoundary.tsx` wraps `<Routes>` |
| Medium | `runtimeUrl="/copilotkit"` hardcoded relative — breaks in prod (no proxy) | **Fixed** — `VITE_COPILOT_URL ?? '/copilotkit'` + `.env.example` + `vite-env.d.ts` |
| High (seed) | apiClient sends no identity (confused-deputy seed, SEC-01) + header-spread order drops Content-Type | **Fixed (seam)** — `setAuthTokenProvider` injection point + header order; full MSAL wiring → pkg 07/08 |
| Medium | `loadSettings` not whitelist-validated (SEC-02) | **Fixed** — providers/models/date whitelisted on load |
| Medium | IssuersPage rendered raw backend `error.message` (SEC-03) | **Fixed** — shows `error.code` + generic text |
| High (deps) | 3 runtime-high advisories (react-router redirect/XSS chain) | **Fixed** — `react-router-dom` 6.26.2 → 6.30.4 → 0 runtime high/critical |
| Low | TopBar `brand-gold` hex token; SettingsPage a11y label→button; legend advertised a dashed edge the data lacks | **Fixed** — primary tokens, aria-labels, legend entry removed |
| Low | Thin tests (arch-11) | **Fixed** — settings roundtrip/corrupt/tamper + apiClient non-JSON/204/apiPost (4→12 tests) |
| Info | arch-01 + implementationPlan/00 still printed `Bloomberg` | **Fixed** — → `Moodys` |

## Residual risks (deferred — tracked)

| Item | Target |
|---|---|
| `types/prism.ts` sub-DTOs inferred (pkg-08 `PrismDtos.cs` unbuilt) — contract drift | pkg 08 (re-sync + OpenAPI/contract test) |
| **pkg-08 must register `JsonStringEnumConverter`** so `Provider` serializes as `"Moodys"` (Severity is a plain string — already matches) | pkg 08 |
| Full auth (MSAL bearer / session+CSRF) — seam in place, not wired | pkg 07/08 |
| CSP + font SRI/self-host | pkg 11 |
| 9 moderate advisories (CopilotKit prismjs/react-syntax-highlighter chain) — not exercised until the sidecar is live | pkg 07 / pre-demo `npm audit` gate |
| Workflow tab hand-rolled tabs + raw-slate tokens; bundle lazy-load | pkg 10 |

## Gate
`npm run build` ✅ 0 TS errors · `npm run test` ✅ 12 passing · runtime audit ✅ 0 critical/0 high.
