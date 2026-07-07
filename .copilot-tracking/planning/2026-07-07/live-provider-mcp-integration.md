# Plan — Live Morningstar + Moody's rating connectors via MCP

**Status:** DRAFT for review · **Do not implement until approved.**
**Revision:** rev 2 — adversarial review folded in (§9); recommendations D2 + D3 changed; decisions recorded (§10).
**Author:** GitHub Copilot · **Date:** 2026-07-07
**Extends:** implementationPlan/04-real-data-connectors.md · the `IProviderRatingsSource` seam
**Reference sample:** [akshata29/wealthgen](https://github.com/akshata29/wealthgen) — `backend/scripts/mcp_login.py`,
`backend/app/services/{mcp_oauth,mcp_client,lseg_mcp}.py`, `backend/app/infra/settings.py`

---

## 0. TL;DR

Prism today serves **labeled‑synthetic** rating cards from Azure AI Search. This plan wires in the
**live** Morningstar and Moody's ratings using the OAuth + MCP pattern proven by the wealthgen sample,
feeding them through Prism's existing honest connector seam (`MoodysRatingsClient` /
`MorningstarDbrsRatingsClient`) **without changing the deterministic reconciliation core**.

**The single most important correction to the framing:** the sample does **not** "build MCP servers to
front the APIs." It **consumes provider‑hosted MCP servers** (Morningstar already runs one at
`https://mcp.morningstar.com/mcp`) as an OAuth‑authenticated **MCP client**. So the primary build is an
**MCP client + OAuth broker**, not a home‑grown server. We only build a thin MCP *server* wrapper if a
provider (possibly Moody's) offers **REST only** and you specifically want a uniform MCP interface.

**Nothing ships until a Phase‑0 discovery spike confirms these providers actually return corporate‑bond
issuer credit ratings** (letter grade + rating‑action date, ideally factor weights) — which is not yet
verified and is the biggest risk to the whole idea.

---

## 1. What the sample actually does (verified, grounded)

| Fact | Evidence |
|---|---|
| Providers host **remote MCP servers**; the app is an MCP **client** | `mcp_client.py` comment: "e.g. Morningstar's `https://mcp.morningstar.com/mcp`" |
| Morningstar runs a **real OAuth 2.1 authorization server** | Fetched `https://mcp.morningstar.com/.well-known/oauth-authorization-server` → `authorize`/`token`/`register`, scopes `offline_access openid email profile`, grants `authorization_code`+`refresh_token`, PKCE `S256` |
| **No headless grant** (no client_credentials) → a **one‑time interactive browser login** is required | `mcp_oauth.py` docstring; Morningstar metadata `grant_types_supported` |
| One‑time login captures a **refresh_token** (`offline_access`); runtime swaps it for short‑lived access tokens | `mcp_login.py` + `mcp_oauth.get_access_token()` (handles rotating refresh tokens, 60s skew) |
| Transport = **MCP Streamable HTTP**, JSON‑RPC 2.0: `initialize` → `notifications/initialized` → `tools/list` → `tools/call`; supports SSE + JSON; `Mcp-Session-Id`, `MCP-Protocol-Version: 2025-06-18` | `mcp_client.py` |
| Discovery: RFC 8414 (`.well-known/oauth-authorization-server`, Morningstar) or RFC 9728 (401 `WWW-Authenticate` → protected‑resource metadata, LSEG) | `mcp_oauth.discover()` |
| Config: `morningstar_mcp_url` (default set), `moody_mcp_url` (**no default — user supplies**), `lseg_mcp_url`; per‑provider `{PROVIDER}_CLIENT_ID/SECRET`; tokens stored at `data/oauth/<provider>.json` (**gitignored, dev‑only**) | `settings.py`, `mcp_login.py`, `mcp_oauth.py` |
| Providers can **also** be registered as **Foundry project connections** (RemoteTool/MCP) so a Foundry agent calls them as tools | `settings.py` `*_connection_name` |

**Moody's specifically:** the sample has `moody_mcp_url` + `moody_connection_name` settings but **no default
URL**. Whether Moody's exposes an **MCP** endpoint or only a **REST** API (e.g. Moody's Ratings / RDS) is
**unknown to us** and must come from your Moody's onboarding pack. The plan branches on this.

---

## 2. How this maps onto Prism (and what stays untouched)

Prism already has the exact seam for this — it was built as an honest placeholder:

- `Connectors/IProviderRatingsSource.cs` → `GetRatingAsOfAsync(issuerId, asOf, ct) : ProviderRatingRecord?`
- `Connectors/MoodysRatingsClient.cs`, `Connectors/MorningstarDbrsRatingsClient.cs` — currently return `null`.
- `Infrastructure/PrismOptions.cs` → `ProviderApis` (nullable `MoodysApi` / `MorningstarApi`).

**Deterministic core is out of scope and must not change:** `Analysis/NotchLadder`, `DivergenceDecomposer`,
`RedFlagEngine` stay pure C# (P2). Live data only changes the **inputs** (the `ProviderRatingRecord`s).

`ProviderRatingRecord` needs: `Letter`, `Notch` (derived via `NotchLadder.ToNotch`), `RatingActionDate`,
and — for the divergence **decomposition** — `Factors[]` = (name, **weight**, **score**, sourceRef). Whether
the providers return per‑factor weights/scores is a **Phase‑0 unknown** (see §6, Risk R1).

---

## 3. Decisions for you to make (each has a recommendation)

> These are the choices that change the shape of the work. I've recommended one per row; the adversarial
> review (§8) stress‑tests them.

**D1 — Consume vs build MCP.**
Morningstar hosts an MCP server → **consume it** (build an MCP client + OAuth broker). For Moody's: **consume**
if they host MCP; if REST‑only, either call REST directly or wrap it in a thin MCP server.
→ **Recommend:** consume where hosted; decide Moody's after Phase‑0 discovery. Do **not** build MCP servers
for providers that already host one.

**D2 — Runtime shape** (where the OAuth + MCP logic lives):
- **(a) Python MCP‑broker sidecar** reusing wealthgen's proven `mcp_oauth`/`mcp_client`, exposing a narrow
  internal HTTP contract (e.g. `GET /ratings/{provider}/{issuerId}?asOf=…`) to the C# connectors. Literal
  "front the APIs," isolates OAuth/secret complexity, minimal C#. Cost: a polyglot service + run script + deploy unit.
- **(b) Native C# OAuth + MCP client** inside the existing connectors (implement RFC 8414/9728 discovery,
  PKCE, refresh, JSON‑RPC over Streamable HTTP in .NET). No polyglot; single deploy. Cost: reimplement a flow
  the sample gives us for free; more C# to test.
- **(c) Foundry MCP project connections** + a Foundry agent tool. Most Azure‑native, but the **user‑delegated**
  `authorization_code` OAuth doesn't fit a headless agent connection cleanly unless Foundry brokers the token.
→ **Recommend (revised after review — STK‑01): (b) native C#** using the official `modelcontextprotocol/csharp-sdk`
(stable; `HttpClientTransport` Streamable HTTP + `ClientOAuthProvider` with RFC 8414/9728 discovery, PKCE S256,
refresh‑with‑rotation, `offline_access`, and CIMD). The Stack review verified the SDK ships the whole OAuth+MCP
flow, so native C# is now **lower** effort than the Python sidecar and avoids a third runtime. Keep (a) only as a
fallback if you want the OAuth/secret blast‑radius in a separate process.

**D3 — Data flow into reconciliation:**
- **(a) Scheduled ingestion** → a refresh job calls the providers and **upserts `ratingCard` docs into Azure
  AI Search `prism-ratings`**. Reconciliation path is unchanged, stays offline‑fast + deterministic, keeps
  as‑of history + caching, and the money‑moment demo never blocks on a provider call.
- **(b) On‑hot‑path live calls** → reconciliation calls the providers per request. Freshest, but couples the
  deterministic core to provider latency/availability/rate‑limits and licensing‑per‑view.
→ **Recommend (revised after review — SEC‑01/ARC‑03): default to (b) session‑only, NO persistence** until
redistribution/caching rights are confirmed in writing (D5). Ingestion (a) is the better *engineering* choice and
stays the target once rights are confirmed — built as a config flag (default off). Do **not** persist or export
licensed ratings on the unconfirmed path. If we ingest, ARC‑01 requires read‑time precedence (live > synthetic, one
card per provider) in `SearchCorpus` + a `DossierAssembler` duplicate‑`Provider` fail‑loud guard.

**D4 — Token lifecycle & secrets:** one‑time interactive login on a **dev/ops machine** → store the
`refresh_token` + `client_secret` in **Azure Key Vault** (not `data/oauth/*.json` like the sample, which is
dev‑only). The deployed service (ACA, no browser) reads + refreshes from Key Vault via `DefaultAzureCredential`.
→ **Recommend:** Key Vault‑backed token store; disk store allowed for local dev only. (P6.)

**D5 — Data licensing / compliance (blocking):** Morningstar and Moody's data is **licensed**. Redistribution,
caching, persistence, and **PDF/dossier export** of their ratings likely have contractual limits, and credit
ratings carry regulatory attribution requirements.
→ **Recommend:** confirm your entitlements/redistribution rights **before** persisting or exporting live data;
gate all live data behind a feature flag; keep the synthetic corpus as the default demo path; add per‑source
attribution + a licensing notice to any surface that shows live ratings.

**D6 — Scope of providers now:** Morningstar is confirmed‑hostable and lowest‑friction; Moody's is unconfirmed.
→ **Recommend:** land **Morningstar first** end‑to‑end (proves the pattern), then Moody's once its offering is confirmed.

---

## 4. Proposed architecture (recommended path: D2‑a sidecar + D3‑a ingestion)

```
                          one-time (human, browser)
  ┌─────────────┐   OAuth authorization_code + PKCE   ┌──────────────────────┐
  │  ops laptop │ ─────────────────────────────────▶ │  Morningstar / Moody  │
  │ mcp_login   │ ◀───────── refresh_token ────────── │   hosted MCP server   │
  └──────┬──────┘                                     └──────────┬───────────┘
         │ store refresh_token + client_secret                   │ MCP JSON-RPC
         ▼ (Azure Key Vault)                                     │ tools/call (Bearer)
  ┌───────────────────────────┐   internal REST    ┌─────────────┴────────────┐
  │  Prism C# API (existing)  │ ─────────────────▶ │  MCP-broker sidecar       │
  │  IProviderRatingsSource   │  /ratings/{prov}/  │  (OAuth refresh + MCP      │
  │  Moodys/MorningstarDbrs   │ ◀───────────────── │   client, per wealthgen)   │
  └──────────┬────────────────┘  normalized card   └───────────────────────────┘
             │ upsert ratingCard docs (refresh job)
             ▼
     Azure AI Search  ── prism-ratings ──▶  deterministic reconciliation (UNCHANGED)
```

If you pick **D2‑b (native C#)**, delete the sidecar box and move the OAuth+MCP logic into the connectors;
everything else (Key Vault token store, ingestion into Search, deterministic core) is identical.

---

## 5. Phased work breakdown

### Phase 0 — Discovery spike (½–1 day) — **prerequisite; may change everything**
Goal: prove the data exists and capture the exact contract before building anything.
- Run the one‑time login against **Morningstar** using your credentials (reuse `mcp_login.py` or a tiny C#/CLI
  equivalent) → obtain a refresh token.
- `tools/list` on Morningstar MCP; capture tool names + JSON schemas. **Confirm** there is a tool returning
  **corporate‑bond issuer credit ratings** (letter grade + rating‑action date; ideally factor weights/scores),
  keyed by an id Prism can map (issuer/CUSIP/ISIN). Morningstar MCP may be fund/equity research — **if it does
  not expose DBRS‑style credit ratings, this provider is out and we say so.**
- Get **Moody's** MCP/REST URL + auth from your pack; repeat the discovery; record whether it's MCP or REST.
- **Deliverable:** a short findings note (real tool names, sample payloads, field → `ProviderRatingRecord`
  mapping, gaps). **Go/No‑Go decision point.**

### Phase 1 — Auth + token broker (1–2 days)
- Implement/port the OAuth 2.1 discovery + PKCE + refresh flow (sidecar reuses wealthgen; native‑C# writes it once).
- One‑time login tool (CLI) that captures the refresh token and writes it to **Key Vault** (local: disk, gitignored).
- Runtime `get_access_token(provider)` with refresh + rotation + skew; secrets via `DefaultAzureCredential`; log ids/counts only (P6).

### Phase 2 — MCP client + provider mappers (1–2 days)
- MCP Streamable‑HTTP JSON‑RPC client (`initialize`/`tools/list`/`tools/call`, SSE+JSON, session header).
- `MorningstarRatingsClient` mapper: MCP tool result → `ProviderRatingRecord` (Letter, RatingActionDate, Factors…).
  **No fabrication:** any field the provider doesn't return is left null; if factor weights are absent, record
  that honestly (see R1 handling). `SourceRef` prefixed e.g. `morningstar:mcp:…` for provenance.
- Moody's mapper per Phase‑0 findings (MCP or REST).
- Wire into the existing `IProviderRatingsSource` implementations (replace the `null` return).

### Phase 3 — Ingestion into the corpus (1 day) — (if D3‑a)
- A refresh job/endpoint that, per issuer in scope, calls the connectors and **upserts `ratingCard` docs** into
  `prism-ratings` (idempotent by (issuerId, provider, asOf); preserves synthetic docs behind a `source` field).
- Feature flag `Prism:LiveProviders:{Morningstar,Moodys}:Enabled` (default **false** → synthetic).
- Fail‑loud on misconfig (P1); provider outage → that provider degrades to `MISSING_COVERAGE`, never fabricated.

### Phase 4 — Config, secrets, deploy (½–1 day)
- `PrismOptions.ProviderApis` extended with MCP URLs + Key Vault references; `.env.example` documented (no secrets committed).
- Key Vault + RBAC (Secrets User) for the API/sidecar managed identity; ACA wiring if sidecar.

### Phase 5 — Tests + compliance gate (1 day)
- Unit tests with a **fake MCP transport** (no live calls in CI) covering: token refresh/rotation, `tools/call`
  parse (JSON + SSE), mapper field‑by‑field (incl. missing‑factor path), ingestion idempotency, flag‑off = synthetic.
- One **manual/integration** live smoke against each provider (kept out of CI; needs the token).
- Licensing/attribution review sign‑off (D5) before any live data is persisted or exported.

**Rough total (Morningstar only):** ~4–6 focused days after a green Phase 0. Moody's adds ~1–2 days depending on MCP vs REST.

---

## 6. Risks & how the plan handles them

- **R1 (critical, RE‑DIAGNOSED after review — ARC‑02/STK‑02) — the real gate is ratings + action date + a
  mappable id, NOT factor weights.** The deterministic core does **not** require per‑factor weights: today
  `SearchCorpusMapper` sets `Factors: Array.Empty`, `DossierAssembler` passes null inputs, so the decomposition
  already runs **letter‑only / residual‑dominated**. Live letter‑only data is therefore fine for the notch gap;
  the Phase‑0 exit criteria are **(1) a real letter grade, (2) a rating‑action date, (3) an id Prism can map**.
  Factor weights are a *bonus* that would restore a real attribution waterfall. **Type safety (STK‑02):** `Factors`
  is non‑nullable → use `Array.Empty<RatingFactor>()` (never null → NRE); `Notch`/`RatingActionDate` are value
  types → a missing action date must map to `MISSING_COVERAGE`, never `default` (which would fabricate a STALE
  flag); `NotchLadder.ToNotch` throws on non‑ladder labels → add a guarded `TryToNotch` → null. If a provider
  returns no credit ratings at all, it is dropped and we say so (P1).
- **R2 — headless auth on ACA.** No browser in the cloud. → one‑time login on a dev/ops box; refresh token in
  Key Vault; service refreshes headlessly (D4). Refresh‑token expiry/revocation → alert + re‑login runbook.
- **R3 — data licensing / redistribution (compliance).** Caching in Search/Cosmos + dossier export may breach
  license. → D5 gate; feature‑flagged; attribution + licensing notice; synthetic stays default.
- **R4 — secrets sprawl.** client_secret + refresh_token are high‑value. → Key Vault only; never disk in prod;
  never logged; `.gitignore` the local token store.
- **R5 — determinism / caching.** Live calls on the hot path would make the demo flaky + non‑reproducible. →
  D3‑a ingestion keeps reconciliation offline‑fast and reproducible.
- **R6 — polyglot footprint (if D2‑a).** A Python sidecar adds a deploy unit + run script. → acceptable for
  faithful reuse + secret isolation; choose D2‑b if you want a single runtime.
- **R7 — rate limits / cost / entitlement scope.** Metered, licensed calls. → ingestion (not per‑view), backoff,
  and only issuers in scope.
- **R8 — id mapping.** Prism issuer ids vs provider ids (CUSIP/ISIN/entity id). → captured in Phase 0; a mapping
  table; unmapped issuer → `MISSING_COVERAGE`, not a guess.

---

## 7. What we are explicitly NOT doing

- Not changing the deterministic notch/gap/red‑flag math (P2).
- **Not removing or degrading the synthetic corpus** — it stays the permanent **fallback**: the default
  when live is off, and the automatic fallback when a live provider is disabled, unhealthy, or returns no
  usable rating. See **§11 (non‑negotiable)**.
- Not inventing provider tool names, endpoints, or fields — Phase 0 discovers them from the live servers + your docs.
- Not persisting or exporting live licensed data until the D5 compliance gate passes.
- Not implementing anything in this task — this is a plan for your review.

---

## 8. Open questions for you

1. **D2:** Python MCP‑broker sidecar (faithful reuse) or native C# in the connectors (single runtime)?
2. **D3:** ingestion into Search (recommended) or live on the hot path?
3. **Moody's:** does your pack point to an **MCP** endpoint or a **REST** API? Please share the URL + auth type.
4. **Credentials:** pre‑registered `client_id`/`client_secret` per provider, or should we use dynamic
   registration (Morningstar's `/register` supports it)?
5. **Licensing (D5):** do we have redistribution/caching/export rights for Morningstar + Moody's ratings, or must
   live data stay in‑memory/session‑only?
6. **Scope:** Morningstar‑first then Moody's (recommended), or both together?

---

## 9. Adversarial review outcomes (2026-07-07) — folded into this plan

Three lenses reviewed this plan (see `.copilot-tracking/reviews/2026-07-07/live-provider-mcp-plan-review-{architecture,security,stack}.md`).

**Verdicts:** Architecture = *approve‑with‑changes* (2 Critical, 6 High); Security = *approve‑with‑mandatory‑controls* (3 Critical, 5 High); Stack = *buildable‑with‑corrections* (1 Critical, 3 High).

| # | Finding | Resolution now in this plan |
|---|---|---|
| ARC‑01 (Crit) | Ingestion = silent dual‑source‑of‑truth: live + synthetic card for one issuer → two same‑provider ratings → skewed confidence/consensus/outlier + "Moodys vs Moodys" pairing. | If we ever ingest: read‑time precedence (live > synthetic, one per provider) in `SearchCorpus` + `DossierAssembler` duplicate‑`Provider` fail‑loud guard. Default is session‑only until licensing confirmed. |
| ARC‑02 (Crit) | R1 misdiagnosed — core doesn't need factor weights; already letter‑only. | R1 rewritten (§6): gate = letter + action date + mappable id. |
| STK‑02 (Crit) | "Leave fields null" won't type‑check: `Factors` non‑nullable (NRE), `Notch`/`RatingActionDate` value types, `ToNotch` throws. | Sentinels (§6/Phase 2): `Array.Empty`, guarded `TryToNotch`→null, missing date → `MISSING_COVERAGE`. |
| SEC‑01 (Crit) | Licensing gate is prose, not code; ingest + export of NRSRO/ESMA‑regulated IP. | Coded **fail‑closed** per‑provider entitlement flags (`Caching`/`Redistribution`/`Export`, default‑deny) + recorded legal sign‑off; default session‑only; Phase‑0 hard gate. |
| SEC‑02 (Crit) | Confused‑deputy: one privileged token behind `[AllowAnonymous]`; injected `issuerId` pulls any issuer's data. | Live data requires **Entra auth + server‑side per‑issuer authorization**; never enabled on anonymous endpoints. |
| SEC‑03 (Crit) | SSRF: configurable/discovered MCP URLs with no allowlist (contradicts `EdgarClient` host guard). | `https`‑only exact‑host **allowlist** for all MCP + discovery + token URLs; reject IP/loopback/link‑local. |
| ARC‑03 (High) | Licensing gate sequenced after the architecture it can veto. | Moved into Phase 0 as a hard gate with a documented no‑persist branch. |
| SEC‑06 (High) | Unvalidated MCP tool results feed the deterministic core (poisoning). | Validate every result (enum/bounds/size) before it becomes a `ProviderRatingRecord`; reject → `MISSING_COVERAGE`. |
| SEC‑04 (High) | One‑time login leaves a cleartext long‑lived token on the ops box; `state` not mandated. | Mandate OAuth `state` + redirect_uri allow‑listing; capture into Key Vault immediately + wipe local; prefer CIMD. |
| SEC‑08 (High) | No audit trail for consuming/ingesting/exporting regulated ratings. | Reuse `IAuditService`: audit every live fetch/ingest/export (provider + issuer + source id, ids only, P6). |
| STK‑01 (High) | D2‑b under‑scored — official .NET MCP SDK ships the flow. | D2 flipped to native C# (§3). |
| STK‑03 (High) | Headless refresh durability on ACA (multi‑replica rotation race). | Shared, ETag‑safe **Key Vault `ITokenCache`** with rotated‑token write‑back + throwing (non‑interactive) redirect → `MISSING_COVERAGE` + alert. |
| STK‑04 (Med) | Protocol version `2025-06-18` is stale (current 2025‑11‑25). | Let the SDK negotiate the version; don't hand‑pin. |

**Agreed sound:** the `IProviderRatingsSource` → `MISSING_COVERAGE` seam; pure deterministic core (P2); Key Vault over disk; the "consume‑not‑build" correction; the Phase‑0 go/no‑go gate. (`NotchLadder` already maps Moody's + DBRS scales but throws on `NR`/`WR`/watch/outlook — handle in the mapper.)

---

## 10. Recorded decisions — CONFIRMED by user (2026-07-07)

You answered **1c, 2a, 3c, 4a, 5a, 6a, 7a, 8a**. This records your review. **Still nothing implemented** — code begins only after the Phase‑0 discovery gate.

| # | Question | Your decision | Effect on the plan |
|---|---|---|---|
| 1 | Entitlement (data has corp‑bond credit ratings?) | **Not sure → confirm in Phase‑0** | Phase‑0 stays a **hard go/no‑go gate**; no integration code until it passes. |
| 2 | Licensing / redistribution | **Full rights: cache + persist + export** | **Unblocks ingestion (D3‑a).** Still record the entitlement in config + attribution on display/export, and audit every fetch/ingest/export (SEC‑08). |
| 3 | Moody's offering | **RESOLVED: MCP** — `https://api.moodys.com/genai-ready-data/m1/mcp` (Streamable HTTP, OAuth, "GenAI‑Ready Data": credit ratings + risk) | Same integration as Morningstar (no REST branch needed). Env placeholders added to `.env` + `.env.example`. `tools/list` still confirms the exact corporate‑bond rating tool in Phase‑0. |
| 4 | Credentials / registration | **Pre‑registered client_id/secret** | Use your client_id/secret (no dynamic/CIMD). Both go in **Key Vault**; never logged/committed. |
| 5 | Runtime shape | **Native C#** (official .NET MCP SDK) | OAuth + MCP client lives in the C# connectors; no Python sidecar. |
| 6 | Data flow | **Ingest into Azure AI Search** | Live ratings upserted into `prism-ratings`; reconciliation stays offline‑fast. **ARC‑01 guards apply:** read‑time precedence (live > synthetic, one card per provider) + `DossierAssembler` duplicate‑`Provider` fail‑loud guard. |
| 7 | Scope / sequencing | **Morningstar first**, then Moody's | Morningstar end‑to‑end first; Moody's after its Phase‑0. |
| 8 | Deploy + auth | **Local dev only** | Runs locally for now. ⚠️ You did **not** select "Entra auth + per‑issuer authZ" — fine while local‑only, but SEC‑02 (confused‑deputy) makes it **mandatory before any non‑local exposure**; recorded as a blocker on ACA/shared deploy. |

**Confirmed critical path:** native C# → pre‑registered creds in Key Vault → **ingest** into Search (with ARC‑01 guards) → Morningstar first → local‑only (auth required before deploy).

### What Phase‑0 needs from you (credential‑gated + interactive by design)
The one‑time Morningstar login is **interactive** (browser; no headless grant), so it can't be run unattended. To execute Phase‑0:
1. Your Morningstar **client_id + client_secret** + the **redirect URI** they allow‑listed (into a git‑ignored `.env`; I'll wire Key Vault).
2. You complete the **browser sign‑in** once when the discovery CLI opens it.
3. The **Moody's** endpoint + auth docs (for its Phase‑0).

Then Phase‑0 = one‑time login → `tools/list` → confirm a corporate‑bond credit‑rating tool (letter + action date + mappable id) → **go/no‑go** before any integration code.

---

## 11. Synthetic fallback guarantee (non‑negotiable)

The labeled‑synthetic corpus is **never deleted or replaced** — live ratings are layered *on top* of it, so
Prism always has a working data path. This is a hard acceptance criterion for the live‑provider work:
**disabling live, or any live failure, must return the app to today's synthetic behavior with zero code change.**

**Resolution order per (issuer, provider):**
1. **Live** — used only when the provider's `Enabled=true`, the OAuth token is valid, and the tool returned a
   usable rating that passes validation (SEC‑06) + mapping (letter + action date + id).
2. **Synthetic** — used whenever live is off, misconfigured, unauthenticated, rate‑limited, unhealthy, or
   returned an unusable/blocked result.
3. **`MISSING_COVERAGE`** — only if neither exists for that issuer/provider (already how the core reports absence).

**How it's guaranteed:**
- **Both** doc sets live in `prism-ratings` with a `source` field (`synthetic` | `live:morningstar` |
  `live:moodys`). Ingestion **upserts live cards; it never deletes synthetic ones.** `SearchCorpus` applies
  read‑time precedence (live > synthetic, one card per provider — the ARC‑01 guard); if the live card is
  absent/stale/disabled, the synthetic card is what's read.
- **Flag = instant revert:** setting `Prism__Providers__{Name}__Enabled=false` (or clearing its ClientId) makes
  the reader ignore live cards → pure synthetic, no redeploy.
- **Fail‑soft on the hot path:** a live fetch/refresh fault degrades that provider to its synthetic card (or
  `MISSING_COVERAGE`), logged + audited — it never throws the reconciliation (mirrors the pkg‑06/07 best‑effort
  narration pattern). A *permanent misconfiguration* still fails loud at the ingestion / one‑time‑login boundary
  (P1), never on the demo hot path.
- **Offline/demo mode always works:** with all provider flags off (the committed default), Prism runs exactly
  as it does today on the synthetic cast — the money‑moment demo is never blocked on a provider call.
