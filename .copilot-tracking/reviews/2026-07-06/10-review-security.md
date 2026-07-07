# Adversarial Frontend-Security Review: Package 10 — Prism UI & Workflow Visualization

**Reviewer:** Fin Adversary Security (frontend red-team lens)
**Date:** 2026-07-07
**Scope:** `frontend/src/pages/*`, `frontend/src/components/prism/*`, `frontend/src/components/workflow/*`,
`ErrorBoundary.tsx`, `App.tsx`, `lib/apiClient.ts`, `lib/queryClient.ts`, `lib/prismFormat.ts`,
`lib/settings.ts`, `hooks/useReconciliation.ts`, `types/prism.ts`, `vite.config.ts`, `index.html`,
`frontend/.env.example`. Backend export/HTML encoding is pkg-08 (referenced, not re-reviewed).

---

## VERDICT

- **Demo: GO** — localhost + synthetic issuers + anonymous-by-design + copilot gated off. There is
  **zero HTML-injection sink in the client** (grep-verified: no `dangerouslySetInnerHTML`, `innerHTML`,
  `eval`, `document.write`, `insertAdjacentHTML`). Every model/data-derived string renders through JSX
  auto-escaping. No secret is bundled.
- **Production: NO-GO** — the dossier **export affordance is architecturally incompatible with the
  client's own bearer-token auth seam** (SEC-10-01), there is **no Content-Security-Policy anywhere**
  to contain the same-origin server-rendered export or a dependency compromise (SEC-10-02), and the
  CopilotKit **enabled path forwards no end-user identity** (SEC-10-05, confused-deputy seed).

**Severity count:** 0 Critical · 1 High · 2 Medium · 2 Low.

---

## Trust Boundary Map (client-side)

| Boundary | What protects it today | Gap |
|---|---|---|
| URL `?issuer=` → POST body | `encodeURIComponent` on deep-link; server re-authorizes issuerId (pkg08) | `asOf` `max=` is a soft HTML hint only; server must clamp (pkg08 SEC-06) |
| `localStorage` `prism.settings` → asOf/providers/models | `loadSettings` whitelist-validates every field | none material — solid |
| Server dossier strings → React render | JSX auto-escaping on **all** fields | no client output policy; refs/docIds safe **as text** only (SEC-10-04) |
| Export → `window.open('/api/v1/.../export')` | `noopener,noreferrer`; `encodeURIComponent(id)` | **no auth possible on a top-level GET nav**; runs same-origin, no CSP (SEC-10-01/02) |
| Google Fonts CDN → `index.html` | `crossorigin` on preconnect | no SRI, no CSP pinning (SEC-10-03) |
| `VITE_COPILOT_URL` → `CopilotKit runtimeUrl` | gated off unless env set; it's a URL not a secret | when enabled, no user token forwarded (SEC-10-05) |
| Bearer seam `setAuthTokenProvider` → `Authorization` | defaults `null`; token only if provider registered | seam **cannot** cover `window.open` export (SEC-10-01) |

---

## Findings

### [SEC-10-01] Dossier export bypasses the client's own auth seam → unauthenticated / CSRF-prone capability URL — Severity: **High** (prod) / demo-acceptable
- **Boundary / Target:** `frontend/src/components/prism/DossierPanel.tsx` L16-19
  (`window.open('/api/v1/reconciliations/${encodeURIComponent(dossier.id)}/export', '_blank', 'noopener,noreferrer')`).
- **OWASP:** A01 Broken Access Control (+ A07-adjacent). **Regulation:** audit-integrity — pkg08 logs
  `dossier_exported` on this GET.
- **Exploit / structural defect:** The app ships a bearer-token auth seam — `apiClient.request()`
  attaches `Authorization: Bearer <token>` when `setAuthTokenProvider` is registered
  ([apiClient.ts](../../../frontend/src/lib/apiClient.ts) L79-82), and the codebase's stated plan is
  MSAL bearer for pkg07/08. **`window.open` is a top-level browser navigation and cannot carry an
  `Authorization` header.** So the export path is *structurally* outside the auth model:
  1. **Today:** export is anonymous (pkg08 SEC-01) — a bearer-less capability URL. `dossier.id` is
     `{issuerId}:{guid}` (122-bit guid ⇒ not enumerable, credit), but any leaked/observed id yields the
     printable dossier to anyone, no auth, no TTL, and each open is audit-logged as an export by
     `Actor:"system"`.
  2. **When MSAL lands (the plan):** every `apiClient` call carries the token but the export navigation
     carries **none** → export 401s for authenticated users. The tempting "fix" is to make `/export`
     **cookie-authenticated** while the rest stays bearer — which turns an **audit-logged GET** into a
     textbook **CSRF sink**: `<img src=".../{id}/export">` or a cross-site `window.open` forces
     victim-session exports (and audit noise) with no token check.
- **Impact:** unauthenticated dossier disclosure now; CSRF-driven export + audit pollution the moment
  auth is added the obvious way.
- **Fix:** decide the export auth model **before** real data. Preferred: fetch the export via
  `apiClient` (so the bearer seam applies) into a `Blob`, then `window.open(URL.createObjectURL(blob))`
  or print the blob — keeps auth, keeps `noopener`. If export must stay a direct navigation, gate it
  behind a short-lived, single-use signed download token issued by an authenticated `apiClient` call
  (never a bare cookie-GET), and enforce object-level authZ + TTL server-side.

---

### [SEC-10-02] No Content-Security-Policy anywhere → same-origin server-HTML export and any dependency compromise run uncontained — Severity: **Medium** (prod) / Low (demo)
- **Boundary / Target:** `frontend/index.html` (no CSP `<meta>`); no CSP header configured for the Vite
  dev server or the built app. Export HTML is opened **same-origin** (`/api/v1/...`,
  DossierPanel L17).
- **OWASP:** A05 Security Misconfiguration.
- **Exploit:** The client's XSS hygiene is genuinely clean (no injection sink found), so there is no
  *known* live XSS. CSP is the **defense-in-depth that is entirely absent**: because the export is
  server-rendered HTML executing in the **app's origin**, a regression in pkg08's `HtmlEncoder`
  (or the future "hyperlink every claim" feature, SEC-10-04) becomes **stored XSS in the app origin**
  with full access to `localStorage` (`prism.settings`) and any future auth cookie — and nothing
  contains it. Same exposure for a compromised transitive dep in the CopilotKit / recharts chain
  (pkg09 flagged 9 moderate advisories in that graph) or the font CDN (SEC-10-03).
- **Impact:** any single injection anywhere in the same origin → full-origin script execution, token/PII
  exfil, silent audit-logged exports.
- **Fix:** ship a strict CSP (header preferred over `<meta>`): `default-src 'self'`;
  `script-src 'self'`; `style-src 'self'` (self-host fonts, see SEC-10-03); `img-src 'self' data:`;
  `connect-src 'self'` (+ the copilot origin only when enabled); `frame-ancestors 'none'`;
  `base-uri 'none'`; `object-src 'none'`. Serve the export in a sandboxed context or a distinct origin
  if any untrusted narrative will ever be rendered there.

---

### [SEC-10-05] CopilotKit enabled path forwards no end-user identity (confused-deputy seed) — Severity: **Medium** (prod) / N-A (gated off in demo)
- **Boundary / Target:** `frontend/src/App.tsx` L32, L64-69 —
  `<CopilotKit runtimeUrl={COPILOT_RUNTIME_URL}>` mounts only `runtimeUrl`, no auth/token wiring.
- **OWASP:** A01 Broken Access Control / A08 (trust-boundary). **arch-06** "confused-deputy rule."
- **Exploit:** When `VITE_COPILOT_URL` is set, the browser→sidecar hop carries **no user token**; the
  sidecar then forwards actions to `/api/v1` (per arch). That is the over-privileged-deputy the security
  doc explicitly forbids: a browser can drive backend reconciliation/export actions with no user
  context, and object-level authZ (issuerId) is enforced nowhere on this path. Prompt-injection into the
  copilot ("export dossier for issuer X") reaches tool calls unauthenticated.
- **Mitigation credit:** the provider is **gated off by default** (honest degradation, P1) — App.tsx
  only mounts `CopilotKit`/`CopilotSidebar` when `VITE_COPILOT_URL` is set. So this is a **prod blocker,
  not a demo issue**. `VITE_COPILOT_URL` itself is a URL, not a secret (correctly public).
- **Fix:** before enabling the copilot, authenticate `/copilotkit` (reject anonymous), forward the
  end-user bearer to the C# API on every tool call, and re-authorize issuerId server-side. Do not enable
  `VITE_COPILOT_URL` against real data until the sidecar propagates identity.

---

### [SEC-10-03] Third-party font CDN with no SRI / no CSP pinning → CSS-injection & integrity risk — Severity: **Low**
- **Boundary / Target:** `frontend/index.html` L10-16 — `fonts.googleapis.com` stylesheet +
  `fonts.gstatic.com`, no `integrity=`, no CSP. (Carry-over of pkg09 SEC-04, still open.)
- **OWASP:** A08 Software & Data Integrity / A05.
- **Exploit:** A compromised or MITM'd font origin can inject arbitrary CSS. CSS is not script, but it is
  enough to (a) exfiltrate DOM attribute values via attribute-selector + `background-image` requests, and
  (b) **restyle the very "we didn't rig it" rule text and red-flag banner** — directly attacking the
  demo's integrity thesis (the deterministic rule is supposed to be trustworthy on screen). SRI on the
  Google Fonts *CSS* URL is impractical (dynamic), so the real fix removes the third-party origin.
- **Fix:** self-host Inter + JetBrains Mono (`@fontsource/*`), drop the external `<link>`s, and cover
  `style-src 'self'` under SEC-10-02's CSP.

---

### [SEC-10-04] Latent: evidence refs / methodology doc ids are safe **as text** today, but the spec's "hyperlink every claim" will create a `javascript:` / open-redirect sink — Severity: **Low** (preventive)
- **Boundary / Target:** `RuleModal.tsx` L64-69 (`<li>{ref}</li>`), `RedFlagPanel.tsx` L58-66
  (`<span>{ref}</span>`), `ProviderVerdictCard.tsx` L45-47 (`methodologyDocId` as text). All are
  model-/data-derived and **currently rendered as escaped text → no live vuln (correct, credited).**
- **OWASP:** A03 Injection (open-redirect / DOM-XSS), latent.
- **Exploit path (not yet present):** pkg10 spec §A.6 ("click → opens the card content") and §C ("every
  claim **hyperlinks** to its evidence") direct a future author to render these as links. Building
  `href={ref}` / `href={methodologyDocId}` / `href={evidenceRef}` from untrusted values yields
  `javascript:...` execution or an open redirect the instant a corpus/LLM value contains such a scheme —
  and on the real-data pivot these values are provider-derived, not team-authored.
- **Fix (guardrail now):** when the hyperlinks land, route every evidence/doc ref through a single
  `safeHref()` that allows **only** `http(s)` scheme and an explicit same-origin/known-host allowlist,
  and always set `rel="noopener noreferrer"` + `target="_blank"`. Add a unit test that
  `javascript:alert(1)` and `//evil.example` are rejected.

---

## What's solid (verified — do not regress)

- **Zero HTML-injection sinks (grep-verified across `frontend/src`):** no `dangerouslySetInnerHTML`,
  `innerHTML`, `eval`, `new Function`, `document.write`, `insertAdjacentHTML`, `.outerHTML`, or
  `setAttribute`. The **only** `window.open` is the export (server nav, `encodeURIComponent`,
  `noopener,noreferrer`). No client code ever injects server HTML into the DOM.
- **All model/data-derived strings render through JSX auto-escaping:** `flag.rule`, `flag.narrative`,
  `bucket.explanation`, `evidenceRefs[]`, `verdict.letter`, `verdict.methodologyDocId`,
  `consensusSummary`, `issuerId`, provider labels — every one is `{value}` in JSX, none is a URL/HTML
  sink today.
- **Recharts is clean:** `DecompositionWaterfall` feeds only numeric `notches` + enum bucket names from
  static maps; axis ticks/`label` are numeric/styling props — **no custom HTML label or tooltip
  renderer**, no narrative text piped into the chart.
- **Citations are text, not links** → no `javascript:` / open-redirect today (see SEC-10-04 for the
  guardrail before that changes).
- **No secrets in the bundle (P6):** only `VITE_COPILOT_URL` (a URL) is read; no API key/token/bearer is
  hardcoded. `apiClient` attaches a bearer only if a provider is registered (defaults `null`).
  `.env` / `.env.*` are gitignored; only `.env.example` is committed and it carries an explicit
  "NEVER put secrets here" warning. **`frontend/dist/` is gitignored — verified not committed**
  (`git ls-files frontend/dist` → empty).
- **`apiClient` posture:** relative same-origin base `/api/v1`; `fetch` default `credentials:'same-origin'`
  (no `include`, no wildcard); parses the `{error:{code,message,details}}` envelope and **throws** rather
  than fabricating data (P1); pages render `error.code` + generic copy, **never** the raw `message` or
  `details` (no info-leak); `204` handled.
- **`ErrorBoundary`** logs `error` + component stack to the **console only** — never to the DOM (no stack
  leak, arch-10).
- **`localStorage` settings are whitelist-validated** (`sanitizeModels`/`sanitizeProviders`/`sanitizeAsOf`/
  `sanitizeFlags`): unknown agent keys, non-enum providers, non-catalog models, and non-ISO dates are all
  dropped to defaults → a tampered `prism.settings` cannot inject arbitrary values into a
  `ReconciliationRequest`.
- **P4 compliance CLEAN:** grep for buy/sell/hold/recommend/allocate/trade/alpha/signal/overweight/
  underweight/position-sizing finds **no violation**; every "trading/investment" occurrence is a
  **negation** ("reconciliation, not trading", "makes no investment decisions", ScopeNotice + workflow
  gate copy). `encodeURIComponent` guards the `?issuer=` deep-link and the export id.

---

## Demo-acceptable residual vs. prod-blocker

**Demo-acceptable (localhost, synthetic, copilot gated off):**
- SEC-10-01 (export anonymous by design on localhost/synthetic; 122-bit guid id not enumerable).
- SEC-10-03 (font CDN — cosmetic/integrity only, no live script).
- SEC-10-04 (latent; no hyperlinks shipped yet).
- SEC-10-05 (CopilotKit gated off by default).

**Prod-blocker (must fix before any real data / non-localhost):**
1. **SEC-10-01** — resolve the export auth model (blob-via-apiClient or signed short-TTL token); never a
   bare cookie-GET.
2. **SEC-10-02** — ship a strict CSP; the same-origin server-HTML export has no containment today.
3. **SEC-10-05** — authenticate `/copilotkit` and forward end-user identity before enabling the copilot.

**Assumptions that would change the verdict:**
- If an upstream API gateway enforces auth + CSP headers on `/api/v1` and `/`, SEC-10-01 drops to Medium
  and SEC-10-02 to Low.
- If prod serves the API cross-origin, the `apiClient` same-origin assumption breaks; watch for anyone
  adding `credentials:'include'` + a permissive `Access-Control-Allow-Origin` (would introduce a CORS/
  CSRF finding not present today).

---

## Top 3 must-fix before any real data
1. **SEC-10-01** — export must go through the authenticated `apiClient` (blob + `createObjectURL`) or a
   signed, short-lived, single-use download token — not a bearer-less `window.open`.
2. **SEC-10-02** — add a strict Content-Security-Policy (`default-src 'self'`, `object-src 'none'`,
   `base-uri 'none'`, `frame-ancestors 'none'`), self-host fonts.
3. **SEC-10-05** — before setting `VITE_COPILOT_URL` against real data, authenticate the copilot runtime
   and propagate the end-user bearer to `/api/v1` (kill the confused deputy).
