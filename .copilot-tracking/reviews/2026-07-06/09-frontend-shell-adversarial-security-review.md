# Adversarial Security Review: Prism Package 09 — Frontend Shell

**Target:** `frontend/` React SPA (browser trust boundary + npm supply chain)
**Reviewer:** Fin Adversary Security (red-team)
**Date:** 2026-07-06
**Scope:** `src/lib/{apiClient,settings,queryClient,utils}.ts`, `src/App.tsx`, `src/pages/*`, `src/hooks/*`, `src/components/**`, `index.html`, `vite.config.ts`, `package.json` + dependency tree.

---

## Trust Boundary Map

| Crossing | What protects it today | Gap |
|---|---|---|
| Browser → Vite dev proxy (:5173) → C# API `/api/v1` (:8000) | Relative `API_BASE='/api/v1'`; fail-loud `ApiError`; `encodeURIComponent` on the one path param | **No end-user identity on the wire** — no `Authorization`, no `credentials`, no CSRF header (apiClient.ts) |
| Browser → Vite proxy → CopilotKit Node sidecar `/copilotkit` (:4000) | `runtimeUrl="/copilotkit"` (same-origin via proxy) | No auth/identity on the copilot channel either; comment asserts the sidecar "forwards actions to `/api/v1`" with **no user principal** → confused-deputy seed (App.tsx:26) |
| Browser localStorage `prism.settings` | `try/catch` + shape guards + merge-over-defaults | Persisted values are **not whitelist-validated**; they will populate a `ReconciliationRequest` in pkg10 |
| Browser ← backend error envelope | Only `error.message` (a string) is rendered; `error.details` is **not** rendered; React auto-escapes | Raw backend `message` surfaced verbatim → info-leak risk in a real deployment |
| `npm install` → node_modules | Exact pins in `package.json`; `package-lock.json` committed; `.gitignore` excludes `.env*`/`node_modules` | 15 transitive advisories; CopilotKit has shipped broken tarballs (1.4.4/1.4.5) |

---

## What Is Actually Solid (attacked, held)

- **No secrets in the browser.** Grep across `frontend/src` for `import.meta.env|VITE_|API_KEY|secret|token|password|Bearer|connectionString|AZURE_|FOUNDRY|FRED_` → **zero matches**. `index.html`, `main.tsx`, and all pages carry no credentials. No `frontend/.env*` files exist. Endpoints shown in Settings are relative (`/api/v1`, `/copilotkit`) — genuinely non-secret. **Meets arch-06 (browser holds no credentials).**
- **No XSS sinks.** No `dangerouslySetInnerHTML`, `eval`, `new Function`, `innerHTML`, `document.write`, or `insertAdjacentHTML` anywhere in `src`. All rendering is JSX interpolation (auto-escaped). `WorkflowDetailPanel`/`WorkflowNode` render **static** `workflowData.ts` (not API/user data).
- **`VITE_` proxy vars are build-time only.** `VITE_BACKEND_URL`/`VITE_COPILOT_URL` are read in `vite.config.ts` (Node context) and never via `import.meta.env` in client code → not inlined into the bundle.
- **Deep-link param is encoded.** `IssuersPage` uses `encodeURIComponent(row.original.issuerId)`; `ReconciliationPage` reads `?issuer=` and renders it as **text**, never as a redirect target.
- **Supply-chain hygiene basics present:** `package-lock.json` committed (enables hash-verified `npm ci`), exact version pins, `.gitignore` correct.

---

## Findings

### [SEC-01] Unauthenticated client calling pattern baked into apiClient + CopilotKit — Severity: **High**
- **Boundary / Target:** `frontend/src/lib/apiClient.ts` L56-58 (fetch); `frontend/src/App.tsx` L26 (`<CopilotKit runtimeUrl="/copilotkit">`)
- **OWASP / Regulation:** A01 Broken Access Control, A07 Auth Failures / SEC-Reg-BI, GDPR (per-client data scoping)
- **Exploit:** `request()` sends only `Content-Type: application/json` — **no `Authorization`, no `credentials:'include'`, no anti-CSRF header.** The CopilotKit provider likewise carries no auth. The SPA therefore presents **zero end-user identity** on either channel. When the pkg07 sidecar + pkg08 API land, the sidecar becomes an over-privileged deputy: a browser (or a prompt-injected copilot action) can request `GET /api/v1/reconciliations/{anyId}` or drive a reconciliation for **any** `issuerId` with no client-side principal to bind object-level authorization to. The App.tsx comment (L8-9) explicitly says the sidecar "forwards actions to the C# `/api/v1` endpoints" — with **no mention of forwarding user identity**. That is the confused-deputy design, seeded on the client now.
- **Impact:** Every financial endpoint is callable with no user context; object-level authЗ is entirely dependent on a downstream control that does not yet exist. In a real deployment this is unauthorized cross-client data access.
- **Fix:** Decide the identity model **now**, on the client: (a) acquire a user token (MSAL / Entra) and attach `Authorization: Bearer` in `apiClient.request`, and forward that token through the sidecar to `/api/v1`; **or** (b) same-site session cookie + `credentials:'include'` + a CSRF token. Enforcement lands in pkg07/08, but the client contract must stop assuming "trust the proxy."

### [SEC-02] Persisted localStorage settings are not whitelist-validated (crosses to server in pkg10) — Severity: **Medium**
- **Boundary / Target:** `frontend/src/lib/settings.ts` L74-88 (`loadSettings` merge), L96-98 (`saveSettings`)
- **OWASP / Regulation:** A03 Injection / A04 Insecure Design (client-supplied state trusted server-side)
- **Exploit:** `loadSettings` validates `providers` only as `Array.isArray && length > 0` — individual elements are **not** constrained to the `Provider` union; `models` is a raw spread `...(parsed.models ?? {})` accepting arbitrary keys/values; `defaultAsOf` is accepted on `typeof === 'string'` with no `yyyy-mm-dd` check. Anyone with devtools, a shared machine, or a future XSS can write `localStorage['prism.settings'] = {"providers":["../evil","<script>"],"models":{"x":"y"},"defaultAsOf":"junk"}`. Per `useReconciliation.ts`, settings will populate `ReconciliationRequest` → `POST /api/v1/reconciliations` in pkg10, sending tampered values to the backend.
- **Impact:** Today **latent** — no stored value is yet used to build a URL or request, and proxy targets + `API_BASE` are constants, so the open-redirect/SSRF-via-proxy path was checked and is **not** exploitable through localStorage. The gap becomes real when pkg10 wires settings into the request body.
- **Fix:** Whitelist on load — filter `providers` against `ALL_PROVIDERS`, constrain `models` values to `MODEL_OPTIONS`, regex-validate `defaultAsOf`. Backend must re-validate the request body server-side (pkg08) as defense-in-depth.

### [SEC-03] Raw backend error message rendered verbatim in the UI — Severity: **Medium**
- **Boundary / Target:** `frontend/src/pages/IssuersPage.tsx` ~L113 (`{error instanceof Error ? error.message : 'Unknown error'}`); source at `frontend/src/lib/apiClient.ts` L37-48 (`toApiError`)
- **OWASP / Regulation:** A05 Security Misconfiguration, A09 Logging/Monitoring Failures (info disclosure)
- **Exploit:** `toApiError` lifts `error.message` straight from the backend `{error:{message}}` envelope; `IssuersPage` prints it into the DOM. React escapes it (**no XSS**), and `error.details` is correctly **not** rendered — but a real backend that leaks stack frames, SQL text, internal hostnames, or upstream URLs in `message` will surface them to any browser.
- **Impact:** Disclosure of backend internals to unauthenticated clients in production.
- **Fix:** Render a generic string + the stable `ApiError.code` (already available), not raw `message`; ensure pkg08 sanitizes `message`. Add a top-level `ErrorBoundary` so an unexpected render throw doesn't expose the React dev overlay/stack.

### [SEC-04] No Content-Security-Policy; third-party fonts from Google CDN without SRI — Severity: **Low**
- **Boundary / Target:** `frontend/index.html` L11-16
- **OWASP / Regulation:** A05 Security Misconfiguration / data-residency (regulated FS)
- **Exploit:** The SPA ships no CSP (meta or header) and loads Inter/JetBrains Mono from `fonts.googleapis.com`/`fonts.gstatic.com` with no Subresource Integrity. Any script that ever lands (future dependency XSS, the prismjs path in SEC-supply below) executes unconstrained; Google's CDN observes every user's IP/referer.
- **Impact:** No defense-in-depth against injected script; minor third-party data leakage.
- **Fix:** Add a strict CSP (`default-src 'self'; connect-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'`) — the API and sidecar are same-origin via the proxy so `'self'` suffices. Self-host fonts (or accept the CDN with documented rationale + SRI). CSP is served by the host (pkg08/11); declare the intended policy now.

### [SEC-05] Vite dev proxy `changeOrigin:true` + `ws:true`; internal ports disclosed — Severity: **Low**
- **Boundary / Target:** `frontend/vite.config.ts` L17-31; `frontend/src/pages/SettingsPage.tsx` (Endpoints card + comment)
- **OWASP / Regulation:** A05 Security Misconfiguration (dev only)
- **Exploit:** The dev proxy tunnels `/api`→:8000 (**with `ws:true`** websocket upgrade) and `/copilotkit`→:4000. Combined with the Vite dev-server advisories (see supply chain), a developer on a hostile network or visiting a malicious site during `npm run dev` could reach the proxied backends. Ports :8000/:4000 are disclosed in the UI and comments.
- **Impact:** Dev-machine only; **not** shipped to production (Vite is a build tool).
- **Fix:** Serve production as static assets behind the real API gateway (no Vite proxy in prod — pkg11); bind the dev server to `localhost`; drop `ws:true` unless the API needs websockets.

---

## Supply-Chain Triage — 15 npm Advisories

Measured with `npm audit` vs `npm audit --omit=dev` on the committed lockfile.

| Bucket | Critical | High | Moderate | Total |
|---|---|---|---|---|
| **All (dev + prod)** | 1 | 4 | 10 | **15** |
| **Runtime (`--omit=dev`)** | **0** | 3 | 9 | **12** |
| **Dev-only (difference)** | 1 | 1 | 1 | **3** |

**By package / advisory:**

- 🔴 **`vitest` (critical) — DEV-ONLY.** *"RCE when accessing a malicious website while the Vitest API server is listening"* + *"arbitrary file read/exec when the Vitest UI server is listening."* Exposure only during `vitest` watch/UI; `npm run test` runs one-shot `vitest run` (no persistent server) → small window. **Not in the shipped bundle.** `fixAvailable` → bump.
- 🟠 **`react-router-dom` / `react-router` / `@remix-run/router` (3× high) — RUNTIME.** *"React Router vulnerable to XSS via Open Redirects"*, *"unexpected external redirect via untrusted paths"*, *"protocol-relative `//` open redirect."* Ships in the bundle. **Not presently reachable** — Prism never redirects on untrusted input (`<Navigate>` targets are static; `?issuer=` is rendered as text). `fixAvailable=True` on `react-router-dom` → one patch bump clears all three.
- 🟠 **`vite` (high) — DEV-ONLY** (*"server.fs.deny bypass on Windows alternate paths"*) + its moderate cluster (*"websites can send any request to the dev server and read the response"*, multiple `server.fs.deny` bypasses, `launch-editor` NTLMv2-hash disclosure on Windows). Matters only while `npm run dev` runs. Bump vite.
- 🟡 **Runtime moderates (9)** — dominated by the **CopilotKit rendering chain**: `prismjs` (**DOM Clobbering**, XSS-class), `refractor`, `react-syntax-highlighter`, `uuid`, `@copilotkit/react-core|react-ui|runtime-client-gql|shared`, plus `postcss` (build-time XSS via unescaped `</style>`). The prismjs DOM-clobbering path only executes once `CopilotSidebar` renders live LLM/markdown output — which requires the **pkg07 sidecar (absent today)** — so it is **not exercised in pkg09**, but becomes a real prompt-injection→XSS surface when the copilot goes live. Several show nested/partial `fixAvailable` (gated on CopilotKit upstream).

**Supply-chain integrity smell:** CopilotKit published **broken tarballs at 1.4.4 and 1.4.5** (empty `dist/`, `main`/`types` pointing at missing files) and is the single largest source of these advisories (~9 of 15). Treat CopilotKit bumps as reviewed events: keep exact pins (done — 1.4.8), keep `package-lock.json` committed (✓), enforce `npm ci` in CI (hash-verified installs), and gate CI on `npm audit --omit=dev` (fail on high+) so dev-only noise (vitest/vite) doesn't block while **runtime highs do**.

**Demo-blocking?** **No.** The one critical is dev-only and one-shot; the three runtime highs aren't reachable through current routes; the moderate CopilotKit chain isn't exercised until the sidecar lands. **Recommended pre-demo (cheap, patch/minor bumps, all have fixes):** `react-router-dom` (clears 3 runtime highs), `vitest` (clears the critical), `vite` (clears the dev high).

---

## Assumptions That Would Change the Verdict

- If an upstream gateway / App Service EasyAuth enforces authN **and the pkg07 sidecar forwards a verified user principal** to `/api/v1`, **SEC-01** drops to Medium (client still needs to send/relay identity).
- If pkg08 guarantees `error.message` is sanitized server-side, **SEC-03** drops to Low.
- If pkg10 wires settings→request **and** pkg08 validates the body server-side, **SEC-02**'s server impact is contained (client-side whitelist still recommended).

---

## Top 3 Must-Fix Before Any Real Data

1. **Establish the client identity model (SEC-01):** attach a user token (or same-site session + CSRF) in `apiClient`, and forward end-user identity through the sidecar — before the copilot can drive `/api/v1` actions. Otherwise every reconciliation/dossier call is an unauthenticated confused-deputy request.
2. **Patch the runtime/critical deps:** bump `react-router-dom` (3 runtime highs), `vitest` (dev critical), `vite` (dev high); gate CI on `npm audit --omit=dev` (fail on high+).
3. **Close the client hardening gaps:** whitelist-validate persisted settings on load (SEC-02); stop rendering raw backend `error.message` — show `code` + generic text and add an `ErrorBoundary` (SEC-03); add a baseline CSP (SEC-04).

---

## Verdict

- **Hackathon demo — GO.** No secrets in the browser, no XSS sinks, **no runtime critical**, and no advisory is reachable through the current routes/UI.
- **Production — NO-GO** until SEC-01 (client identity / confused-deputy) is resolved, the runtime-high + critical dependency bumps are applied, and SEC-02/03/04 are closed.
