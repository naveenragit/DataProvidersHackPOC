# Adversarial Security & Compliance Review: Live Morningstar + Moody's MCP Integration (PLAN)

**Target:** `.copilot-tracking/planning/2026-07-07/live-provider-mcp-integration.md`
**Reviewer:** Fin Adversary Security · **Date:** 2026-07-07
**Scope:** Plan-only, pre-implementation. Reviewed for secrets lifecycle, OAuth flow, data-licensing/regulatory,
MCP transport trust, least-privilege, and PII hygiene against `architecturalPlan/00-core-principles.md` (P6),
`architecturalPlan/06-security-and-compliance.md`, and `.github/instructions/financial-domain/financial-domain.instructions.md`.

---

## VERDICT

**APPROVE-WITH-MANDATORY-CONTROLS** — do **not** provision or use any live Morningstar/Moody's credential until
every MUST-HAVE control in the last section is written into the plan. The plan is honest, flag-gated, and defers
implementation behind a Phase-0/D5 gate — but as a *security & compliance* gate it is **under-specified on the
controls that actually keep a bearer secret and a regulated, licensed data feed out of trouble**. Several of its
"recommend" statements (D4, D5, R3, R4) are aspirations, not enforceable controls. Ship the controls, not the intent.

**Severity counts:** 3 Critical · 5 High · 4 Medium · 2 Low (14 findings).

---

## Trust Boundary Map

| Crossing | What the plan says protects it | Gap |
|---|---|---|
| Ops laptop `mcp_login` → provider `/authorize` `/token` | §5 Phase 1 "OAuth 2.1 discovery + PKCE + refresh"; §1 notes PKCE S256 | `state`/CSRF **never mandated**; redirect_uri/localhost callback binding unspecified; captured token lands in **cleartext `data/oauth/<provider>.json` on the laptop** (§1, D4) with no shred-after-upload |
| Ops laptop → Azure Key Vault | D4 "store refresh_token + client_secret in Key Vault"; R4 "never disk in prod" | Write-identity (ops human) vs runtime read-identity conflated ("API/sidecar managed identity", Phase 4); no KV versioning/rotation/soft-delete/purge-protection stated |
| Prism C# API → MCP-broker sidecar (D2-a) | §4 "internal REST `/ratings/{prov}/{issuerId}`" | **No end-user identity** carried; **no authZ** on issuerId; sidecar calls providers with one privileged seat → confused-deputy |
| Sidecar/API → provider MCP server (`tools/call`, Bearer) | §4 diagram; §1 transport table | **Configurable MCP URL, no host allowlist** (SSRF regression vs `EdgarClient.AllowedHost`); no TLS pinning; no response schema/size validation before values enter the deterministic core |
| Provider MCP result → `Analysis/NotchLadder` (deterministic core) | §2 "map fields, no fabrication"; P2 core unchanged | **No bounds/enum validation** of provider-supplied `Letter`/notch; untrusted external data feeds the "honest" core |
| App → Azure (Cosmos/Search) via `DefaultAzureCredential` | P6; D3-a ingestion | Persisting + exporting **licensed, NRSRO/ESMA-regulated ratings**; D5 gate is advisory, not enforced; no retention/attribution/entitlement enforcement |

---

## Findings

### [SEC-01] Persisting and exporting licensed, NRSRO/ESMA-regulated ratings behind an advisory-only gate — Severity: **Critical**
- **Boundary / Target:** D3-a ingestion (§3, §4, §5 Phase 3) + D5 (§3) + "dossier export" (§8 Q5, R3).
- **OWASP / Regulation:** A04 Insecure Design · **SEC Rule 17g / NRSRO recordkeeping (Dodd-Frank, Credit Rating Agency Reform Act 2006)** · **EU CRA Regulation (EC) No 1060/2009 (ESMA)** · Morningstar/Moody's redistribution & display license terms · `financial-domain.instructions.md` "licensed-data handling / audit trail".
- **Exploit / breach path:** The plan's own D3-a **upserts `ratingCard` docs into Azure AI Search `prism-ratings`** (caching/storage) and the product **exports a dossier** (§8 Q5, ARC-10-04 fallback) that renders those ratings. Credit ratings from Moody's (an NRSRO) and Morningstar/DBRS are **licensed and regulated intellectual property**: caching, storage, redistribution, display outside the entitled seat, and export/print are typically **contractually restricted and separately regulated**. D5 "recommends confirm your entitlements … before persisting or exporting" — a **recommendation is not a control**. Nothing in the plan *enforces* the entitlement: no retention limit, no deterministic attribution stamp, no export-entitlement check, no "in-memory/session-only if no redistribution right" code path.
- **Impact:** Contractual breach (feed termination, damages) and a **regulatory recordkeeping/redistribution exposure** the moment one licensed rating is written to Search or printed into a PDF. This is the single biggest risk in the plan and it is gated by prose, not by the pipeline.
- **Fix (mandatory):** Convert D5 from "recommend" to a **hard, coded gate**: (1) a per-provider `Redistribution`/`Caching`/`Export` entitlement flag in `PrismOptions.ProviderApis`, default **deny**; ingestion into Search/Cosmos and dossier export **fail-closed** unless the entitlement is explicitly `true` with a recorded license reference; (2) if only "display, no redistribution," force the D3-b **session-only/in-memory** path (never persisted, never exported); (3) deterministic per-source **attribution + licensing notice** injected by `Analysis/`, not by the LLM narrator (P2/P4), on every surface and export; (4) a retention/TTL policy on any cached rating; (5) legal sign-off recorded as an artifact before the flag can be set. Keep synthetic as the default until this passes.

### [SEC-02] Confused-deputy: one privileged provider seat serves all (anonymous) users with no object-level authorization — Severity: **Critical**
- **Boundary / Target:** Browser → API → sidecar → provider (D2-a, §4); the internal `/ratings/{prov}/{issuerId}` contract; inherits the platform's existing `[AllowAnonymous]` posture (no auth added by this plan).
- **OWASP / Regulation:** **A01 Broken Access Control** · A04 Insecure Design · licensing (single-seat serving many principals) · `06-security-and-compliance.md` "confused-deputy rule … forward the end-user identity … re-authorize every LLM-provided argument."
- **Exploit:** The one-time login mints **one** ops-identity `refresh_token`. The sidecar then calls Moody's/Morningstar with **that single privileged seat for every request**, carrying **no end-user identity**. The plan adds no authentication or per-user/object-level authorization on the `issuerId` boundary. Any caller (today anonymous) who can reach `POST /reconciliations` — or, worse, drive it via a prompt-injected CopilotKit action (`getPortfolio`-style) with an arbitrary `issuerId` — makes the deputy pull **any issuer's licensed rating** through the privileged token. On the D3-b hot-path variant this is a live per-request confused-deputy; even on D3-a, an unbounded/attacker-influenced ingestion scope is the same abuse at batch scale.
- **Impact:** Unlicensed/unauthorized retrieval of regulated third-party ratings under one seat, no attribution of *who* pulled *what* (compounds SEC-08), and metered-cost abuse. The "confirm-scope" gate (P5) is decorative here (see also platform ARC-08-06 "Providers field dropped").
- **Fix (mandatory):** Enforce Entra JWT auth on the API **and** the sidecar `/copilotkit`/`/ratings` boundary; forward the **end-user bearer** to the API; API re-authorizes `issuerId` against a caller-scoped allowlist before any provider call; treat the LLM's `issuerId`/`provider`/`asOf` as hostile and re-validate (P6). Constrain ingestion to a **server-defined** issuer scope, never a client-supplied list.

### [SEC-03] SSRF: configurable MCP URL with no host allowlist — regression against the codebase's own SSRF guard — Severity: **Critical**
- **Boundary / Target:** Sidecar/API → provider MCP endpoint. §1/§3 config: `morningstar_mcp_url` (default set), **`moody_mcp_url` (no default — user supplies)**, `lseg_mcp_url`; §5 Phase 2 MCP client; §5 Phase 4 "MCP URLs" in `PrismOptions`.
- **OWASP / Regulation:** **A10 Server-Side Request Forgery** · `06-security-and-compliance.md` "connectors only call the fixed EDGAR/FRED/Treasury hosts — validate/allowlist URLs; never fetch a model-provided URL."
- **Exploit:** The existing `EdgarClient.cs` (L23) hard-pins `AllowedHost = "data.sec.gov"` and calls `EnsureAllowedHost()` on every request — the codebase's established SSRF control. The plan instead makes the MCP base URL a **free-form config value** with an explicit "user supplies" for Moody's, and the OAuth **discovery** step follows server-advertised `authorize`/`token`/`register` URLs (RFC 8414/9728) — i.e., it *fetches metadata-provided URLs*. A tampered config value, a compromised discovery document, or a Key-Vault/config write gives an attacker an outbound request primitive from a managed-identity-bearing egress point (Azure IMDS `169.254.169.254`, internal ACA endpoints, `localhost` services). The plan is **silent on host allowlisting** for the new hosts.
- **Impact:** SSRF to cloud metadata / internal services from an identity that holds provider bearer tokens and `DefaultAzureCredential`; potential credential/metadata theft.
- **Fix (mandatory):** Extend the `EdgarClient`-style allowlist to the exact provider hosts (`mcp.morningstar.com`, the confirmed Moody's host) — scheme `https` only, allowlisted host set, reject `authorize`/`token`/`register`/resource-metadata URLs whose host is not in the same allowlisted registrable domain; forbid literal IPs, `localhost`, and link-local. No model- or discovery-provided host is dialed without passing the allowlist.

### [SEC-04] One-time-login secret-capture window: cleartext token on the ops laptop, callback/`state`/redirect_uri unbound — Severity: **High**
- **Boundary / Target:** Ops laptop `mcp_login` (§4 diagram, §5 Phase 1, D4); local token store `data/oauth/<provider>.json`.
- **OWASP / Regulation:** **A02 Cryptographic Failures / CWE-522 Insufficiently Protected Credentials** · A07 (CSRF on `authorization_code`) · P6.
- **Exploit:** (a) The `authorization_code` is delivered to a **localhost callback**; the plan **never mandates the `state` CSRF check** and **never pins `redirect_uri`** to a single localhost port — an OAuth authorization-code injection / login-CSRF window. (b) Even in the Key-Vault path, the sample's flow first writes the **refresh_token (offline_access, long-lived) + id_token in cleartext JSON to the laptop disk**; the plan says "local: disk, gitignored" for the login tool but does **not** require shredding that file after upload, nor forbid it landing in backups/OneDrive/`git stash`. A high-value bearer secret sits in cleartext on an endpoint that is not a hardened HSM.
- **Impact:** Theft of a long-lived refresh_token = durable, headless access to a licensed regulated feed under the ops seat, plus id-token PII (SEC-07).
- **Fix (mandatory):** Mandate PKCE **and** `state` verification and a fixed `redirect_uri=http://127.0.0.1:<fixed-port>/callback` (loopback, exact match); run the capture on a managed/hardened workstation; write the token **straight to Key Vault** and **shred** any transient disk copy (or never touch disk — pipe in memory); document that the local disk store is dev/synthetic-only and is `.gitignore`d **and** excluded from backup sync.

### [SEC-05] Rotation & revocation story is a runbook stub, not a control — Severity: **High**
- **Boundary / Target:** D4 token lifecycle; R2/R4 ("Refresh-token expiry/revocation → alert + re-login runbook").
- **OWASP / Regulation:** **A07 Identification & Authentication Failures / CWE-613** · secret-management hygiene (P6).
- **Exploit:** The plan handles *rotating refresh tokens on refresh* (good) but has **no proactive rotation, no compromise-response, and no revocation propagation**. If Key Vault, the sidecar, or the laptop leaks the refresh_token, there is no documented "revoke at the provider `/token` or console + purge KV version + re-login" playbook, no KV secret **versioning/soft-delete/purge-protection** requirement, and no expiry alerting SLO. "offline_access" long-lived tokens with no rotation policy are exactly the secret you must be able to kill in minutes.
- **Impact:** A leaked bearer stays valid until it happens to expire; no clean kill-switch.
- **Fix:** Require KV soft-delete + purge-protection + versioning; a documented **revoke-and-rotate playbook** (provider-side revocation first, then KV new version, then re-login); expiry/refresh-failure alerting; and a periodic forced re-login cadence rather than relying on natural expiry.

### [SEC-06] The deterministic "honest core" ingests unvalidated external MCP tool results — Severity: **High**
- **Boundary / Target:** Provider MCP result → mapper → `ProviderRatingRecord` → `Analysis/NotchLadder`/`DivergenceDecomposer` (§2, §5 Phase 2).
- **OWASP / Regulation:** **A08 Software & Data Integrity Failures** · A03 Injection · P2 (the core must stay trustworthy) · P3 provenance.
- **Exploit:** P2's whole point is that the notch/gap/red-flag math is trustworthy. But the plan feeds it **values from an external server it does not control** with only "map fields, no fabrication." There is **no schema validation, no enum/letter allowlist, no notch bounds check, no numeric sanity on factor weights**, and **no response-size/DoS cap** before `NotchLadder.ToNotch` and the decomposer consume them. A buggy/poisoned/man-in-the-middled MCP response (an out-of-ladder letter, a NaN/overflow weight, a 500 MB SSE stream) can corrupt a red-flag trigger or DoS the ingestion — laundering an attacker value through the "we didn't rig it" deterministic surface.
- **Impact:** Integrity compromise of the exact component the demo relies on to be incorruptible; memory-exhaustion DoS via unbounded SSE/JSON.
- **Fix (mandatory):** Validate every provider field at the mapper boundary — letter against the known ratings enum, notch within ladder range, weights/scores finite and in `[0,1]`/expected range, `ratingActionDate <= asOf` (already required by `IProviderRatingsSource`) — and **fail-closed to `MISSING_COVERAGE`** on any violation (never coerce). Enforce a max response size and read timeout on the MCP transport; pin TLS to the provider cert/CA where feasible.

### [SEC-07] OAuth over-scoping pulls user PII (`openid email profile`) with no stated destination or minimization — Severity: **High**
- **Boundary / Target:** OAuth scopes `offline_access openid email profile` (§1, verbatim from sample); id_token handling; §5 Phase 1 "log ids/counts only (P6)."
- **OWASP / Regulation:** A04 Insecure Design · **GDPR/CCPA data minimization (GDPR Art. 5(1)(c))** · P6 "never log/store PII; never place PII in prompts or telemetry" · `financial-domain.instructions.md` data-privacy (no names in logs).
- **Exploit:** Prism only needs `offline_access` (the refresh token). The plan copies `openid email profile` verbatim, so the id_token carries the **ops user's email and profile**. The plan never says where the id_token goes — if the whole token JSON is persisted to disk/Key Vault or logged during the "log ids/counts" step, PII lands in a secret store, telemetry, or a `git`-adjacent file, violating P6 and data-minimization.
- **Impact:** Avoidable PII sprawl into Key Vault/logs/backups; GDPR minimization finding; needless breach blast-radius.
- **Fix:** Request the **minimum scope** required (drop `email profile` unless a hard requirement is documented); persist **only the refresh_token/client_secret**, never the id_token; assert in code + tests that no token claim, email, or name is ever logged (P6) or written to telemetry.

### [SEC-08] No audit trail for consuming, ingesting, or exporting regulated third-party ratings — Severity: **High**
- **Boundary / Target:** D3-a ingestion, dossier export, one-time login, token refresh — none are audited by the plan (§5 Phase 3/5).
- **OWASP / Regulation:** **A09 Security Logging & Monitoring Failures** · **SEC 17g / NRSRO recordkeeping** · `06-security-and-compliance.md` "audit_events for every consequential action" · `financial-domain.instructions.md` "every financial data mutation must log an AuditEvent."
- **Exploit:** The platform already under-audits (prior ARC-08-03: `scope_confirmed` never written; SEC-02 `Actor:"system"`). This plan adds **new consequential, regulated actions** — pulling a licensed Moody's rating, upserting it into the corpus, exporting it — and specifies **zero audit records** for them. There is no who/what/when for third-party regulated-data consumption, which is precisely what NRSRO/redistribution audits demand.
- **Impact:** No provenance of which licensed rating was consumed by which principal at which as-of; cannot answer a licensor/regulator "prove your usage" request; compounds SEC-01/SEC-02.
- **Fix:** Emit `audit_events` (real actor from SEC-02's forwarded identity, not `"system"`) for: one-time login, each token refresh (id/count only, no token material), each provider `tools/call` (provider, issuerId, asOf, entitlement flag state), each ingestion upsert, each dossier export — ids/counts only, no rating values or PII (P6). Make export/ingestion audit **atomic** with the mutation (don't repeat ARC-08-02's non-atomic write).

### [SEC-09] Least-privilege conflation and missing egress allow-listing — Severity: **Medium**
- **Boundary / Target:** §5 Phase 4 "Key Vault + RBAC (Secrets User) for the API/sidecar managed identity"; ACA egress.
- **OWASP / Regulation:** A01 / A04 · least-privilege (P6).
- **Exploit:** "Secrets User" (read) for the runtime is correct — but the **one-time login must *write*** the secret, and the plan attaches that to the same "API/sidecar managed identity," implying a broader grant or a shared identity for two very different privilege levels. Separately, the plan never constrains **network egress**: an ACA app that can reach the whole internet is a bigger SSRF/exfil target once it holds provider bearers + `DefaultAzureCredential`.
- **Fix:** Split identities — a human/ops principal with `Key Vault Secrets Officer` scoped to the specific secrets for the *login* op only; a runtime managed identity with `Secrets User` (read) only. Scope RBAC to the specific Key Vault (not subscription/RG). Add ACA **egress allow-listing to only the provider hosts + Azure service endpoints**; deny all other outbound.

### [SEC-10] In-memory secret lifetime and the Python sidecar's expanded attack surface — Severity: **Medium**
- **Boundary / Target:** D2-a Python MCP-broker sidecar (§3, §4, R6).
- **OWASP / Regulation:** **A06 Vulnerable & Outdated Components** · A08 · CWE-316 (cleartext secret in memory).
- **Exploit:** Choosing D2-a adds a **second language runtime + its dependency tree** (the sample's `mcp_oauth`/`mcp_client`/`httpx`/etc.) that must hold the refresh_token and access_token **in process memory**. The plan doesn't address in-memory secret handling, dependency pinning/scanning for the sidecar, or the fact that a sidecar RCE (larger dep surface) yields the privileged provider bearer and can drive the confused-deputy (SEC-02) directly. R6 weighs "polyglot footprint" only as a *deploy* cost, not a *security* cost.
- **Fix:** Prefer D2-b (native C#, single runtime, one dependency graph already scanned) **for security** unless faithful-reuse speed genuinely outweighs it; if D2-a, pin + scan the sidecar deps (SCA in CI), minimize token in-memory lifetime, run the sidecar as non-root with a read-only FS and the egress allowlist from SEC-09, and never expose its `/ratings` port beyond the API.

### [SEC-11] Provenance mixing: a live-intended issuer can silently resolve to a stale synthetic card — Severity: **Medium**
- **Boundary / Target:** §5 Phase 3 "upsert … preserves synthetic docs behind a `source` field"; R3/R5.
- **OWASP / Regulation:** A04 Insecure Design · **P3 provenance / P1 no silent fallback**.
- **Exploit:** Ingestion co-locates live and synthetic `ratingCard` docs in the same `prism-ratings` index, distinguished only by a `source` field. If a live provider outage (R7) or an id-mapping miss (R8) means no live doc is written for an issuer, the reconciliation query can still match the **synthetic** doc and present it as a current rating — a silent substitution of fabricated-labeled data for a real feed, exactly the "silent fallback" P1 forbids, and a provenance break (P3).
- **Fix:** Make the reconciliation query **source-explicit** when live mode is on for an issuer: if live is expected and absent, return `MISSING_COVERAGE` (honest), never fall through to the synthetic doc. Stamp every card's `source`/provenance into the dossier so the UI can never conflate synthetic and licensed data.

### [SEC-12] Dynamic client registration (RFC 7591) lane left open on a public `/register` — Severity: **Medium**
- **Boundary / Target:** §8 Open Question 4 ("use dynamic registration (Morningstar's `/register` supports it)").
- **OWASP / Regulation:** A07 · A04 Insecure Design · CWE-522.
- **Exploit:** Dynamic registration self-mints a **new long-lived `client_secret`** from a public endpoint. The plan leaves it as an open question with no lockdown: no `redirect_uri` allowlist constraint (loopback only), no handling/rotation plan for the dynamically issued secret, and no decision between DCR vs pre-registered `client_id` vs the metadata notes' `client_id_metadata_document_supported=true`. A permissive DCR + unbound redirect_uri is a spoofing/redirect surface.
- **Fix:** Prefer **pre-registered** `client_id/secret` per provider (fewer moving secrets); if DCR is used, pin `redirect_uri` to the loopback exact-match, treat the issued `client_secret` as a Key-Vault secret with the SEC-05 rotation policy, and register the **minimum** grant/scope. Decide the lane in the plan, don't defer it to implementation.

### [SEC-13] Local disk token store and container-image/CI secret leakage not fully closed — Severity: **Low**
- **Boundary / Target:** `data/oauth/<provider>.json` (dev), §5 Phase 4 ".env.example (no secrets committed)", container build.
- **OWASP / Regulation:** A02 · P6/`05-configuration-and-secrets.md`.
- **Exploit:** `.gitignore` covers the disk store and `.env` (consistent with `05`), but the plan doesn't assert that (a) no token/secret is baked into a **container image layer** or printed in **CI logs**, and (b) the dev disk store is never populated with a *production/live* token "just to test."
- **Fix:** Add a check that image layers and CI logs contain no secret; forbid live tokens in the disk store; keep the `.env.example`-only committed rule (already in `05`).

### [SEC-14] Feature flag exists but there is no kill-switch runbook tied to revocation — Severity: **Low**
- **Boundary / Target:** §5 Phase 3 flag `Prism:LiveProviders:{Provider}:Enabled` (default false).
- **OWASP / Regulation:** A09 / operational safety.
- **Exploit:** Default-false is good, but the plan doesn't connect the flag to an incident action: on suspected leak/over-use, what is the **one step** to stop live consumption? The flag and the SEC-05 revocation playbook aren't linked.
- **Fix:** Document "flip flag → false, revoke provider token, rotate KV" as a single ordered kill-switch runbook; verify flag-off truly stops all provider egress (not merely hides the UI).

---

## What's Sound (do not regress these)

- **Deferred implementation behind Phase-0 + D5 gates** — nothing ships until the data is proven and licensing is
  confirmed (§0, §5 Phase 0, §7). Correct posture for a regulated feed; this is why the verdict is not *reject*.
- **Key-Vault-backed token store, not disk in prod (D4/R4)** and **`DefaultAzureCredential`** — aligns with P6 and
  `05`. The intent is right; SEC-04/05/09 only harden *how* it's done.
- **D3-a ingestion over D3-b hot-path** genuinely reduces the confused-deputy and DoS blast radius vs per-request
  live calls, and preserves the deterministic core's offline reproducibility (P1/P2). Keep it (with SEC-11's fix).
- **Deterministic core stays pure (P2)**; live data changes only inputs (§2). The right architectural boundary —
  SEC-06 just insists the inputs be validated at the seam.
- **"No fabrication" honesty** — absent factors degrade to an "attribution unavailable" bucket, unmapped issuers →
  `MISSING_COVERAGE`, no invented tool names/fields (§2, §6 R1, §7). Consistent with P1/P3.
- **PKCE S256 acknowledged** and refresh-token rotation + skew handled (§1) — the OAuth *mechanics* are on the right
  track; SEC-04/12 close the CSRF/redirect/registration gaps around them.
- **Explicit `.env.example`-only, gitignored secrets** (§5 Phase 4) — matches `05`.
- **Synthetic corpus stays the default demo path** — the safe fallback is honest and flag-gated, not silent.

---

## MUST-HAVE Controls Before Any Live Credential Is Provisioned or Used

1. **[SEC-01] Coded, fail-closed licensing gate.** Per-provider `Caching`/`Redistribution`/`Export` entitlement flags
   default-deny; ingestion into Search/Cosmos and dossier export are blocked unless the entitlement is explicitly set
   with a recorded license reference and legal sign-off. Deterministic attribution + licensing notice on every surface
   and export. If no redistribution right → session-only/in-memory path, never persisted, never exported.
2. **[SEC-02] AuthN + object-level authZ on the API and sidecar.** Entra JWT enforced; end-user identity forwarded to
   the API; `issuerId`/`provider`/`asOf` re-validated and re-authorized server-side; ingestion scope is server-defined,
   never client-supplied. No anonymous access over live licensed data.
3. **[SEC-03] SSRF host allowlist** for the MCP + discovery + token/authorize/register URLs — `https`-only, exact
   allowlisted provider hosts, reject IP/loopback/link-local and any off-allowlist discovered host — mirroring
   `EdgarClient.AllowedHost`.
4. **[SEC-06] Validate every provider field at the mapper seam** (letter enum, notch bounds, finite weights,
   `ratingActionDate <= asOf`) and **fail-closed to `MISSING_COVERAGE`**; enforce max response size + read timeout on
   the MCP transport.
5. **[SEC-04] Harden the one-time login:** PKCE **+ `state`** verification, exact-match loopback `redirect_uri`,
   token written straight to Key Vault, transient disk copy shredded (or never written); run on a hardened workstation.
6. **[SEC-05] Revocation + rotation:** KV soft-delete/purge-protection/versioning, a documented revoke-and-rotate
   playbook (provider-side revoke first), refresh-failure alerting, and a forced re-login cadence.
7. **[SEC-08] Audit every regulated action** (login, token refresh, `tools/call`, ingestion upsert, export) with a
   real actor, ids/counts only, atomic with the mutation.
8. **[SEC-07] Minimize scope + PII:** request only `offline_access` unless `email/profile` is justified; persist only
   the refresh_token/client_secret, never the id_token; assert no token claim/PII is ever logged (P6).
9. **[SEC-09] Split least-privilege identities** (ops write vs runtime read), Key-Vault-scoped RBAC, and ACA **egress
   allow-listing** to provider hosts only.
10. **[SEC-11] Source-explicit reconciliation** so a live-intended issuer never silently resolves to a synthetic card.

## Assumptions That Would Change the Verdict

- If an **upstream API gateway** already enforces Entra auth + rate limiting + egress control in front of the API and
  sidecar, SEC-02 drops to High and part of SEC-09 is satisfied — but the plan/templates do not state one, so it is a gap.
- If the providers' contracts explicitly **grant caching, storage, and export redistribution rights** for the seat in
  use, SEC-01 drops from Critical to Medium (attribution + audit still required). The plan does not assert this; D5
  treats it as unknown, so it stands Critical.
- If D2-b (native C#) is chosen, SEC-10 largely resolves.
