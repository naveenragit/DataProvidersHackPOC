# 13 — Live rating-provider MCP connectors (Morningstar + Moody's)

**Depends on:** 02, 03, 04 (the `IProviderRatingsSource` seam), 08 (Search corpus).
**Source of truth (read first):** [`.copilot-tracking/planning/2026-07-07/live-provider-mcp-integration.md`](../.copilot-tracking/planning/2026-07-07/live-provider-mcp-integration.md)
and its three reviews in `.copilot-tracking/reviews/2026-07-07/live-provider-mcp-plan-review-{architecture,security,stack}.md`.

**Purpose:** connect Prism to the **live hosted MCP servers** for Morningstar (`https://mcp.morningstar.com/mcp`)
and Moody's (`https://api.moodys.com/genai-ready-data/m1/mcp`) — both Streamable-HTTP MCP over OAuth 2.1
(authorization_code + PKCE + refresh_token) — and feed live ratings through the existing connector seam
**without changing the deterministic core**, with the synthetic corpus kept as a permanent fallback.

## Confirmed decisions (user, 2026-07-07)
Native **C#** using the official .NET MCP SDK · **pre-registered** client_id/secret (→ Key Vault) ·
**ingest** live ratings into Azure AI Search · **Morningstar first** · **local dev only** (Entra auth deferred
but mandatory before any non-local exposure — SEC-02) · **full** redistribution rights (persist/export allowed) ·
**synthetic corpus = permanent, non-negotiable fallback** (planning doc §11).

## Non-negotiable guardrails (apply to every phase)
- **Synthetic stays the default + fallback.** All provider flags default `Enabled=false`; with flags off the app
  behaves **exactly** as today. Live is layered on top; synthetic docs are never deleted. (Planning §11.)
- **Keep all existing backend tests green** (currently 188) and the NordStar money-moment working.
- **No fabrication (P1):** a mapper that cannot find a valid letter grade + rating-action date + mappable id
  returns `MISSING_COVERAGE` — never an invented value. `Factors`/`Notch`/`RatingActionDate` use the STK-02
  sentinels (`Array.Empty<RatingFactor>()`, guarded `TryToNotch`→null, missing date → `MISSING_COVERAGE`).
- **SSRF (SEC-03):** `https`-only **exact-host allowlist** for all MCP + OAuth discovery + token URLs (mirror the
  `EdgarClient` host guard); reject IP/loopback/link-local/off-allowlist hosts.
- **Secrets (P6):** client_secret + refresh_token via **Azure Key Vault** (local dev: a git-ignored token file);
  never logged, never committed. Log ids + counts only.
- **Deterministic core untouched (P2);** `CancellationToken` plumbed (P7).

---

## Round 1 — credential-independent foundation (THIS run; fully offline-testable)

Nothing here touches `SearchCorpus`, `DossierAssembler`, or the reconciliation read path.

1. **Config** — add `PrismOptions.Providers` (`Morningstar`, `Moodys`), each with `Enabled`, `McpUrl`,
   `ClientId`, `ClientSecret`, `RedirectUri`, bound from the `Prism__Providers__*` keys already present in
   `.env` / `.env.example`. Validate each URL against the host allowlist at bind/first-use (fail loud, P1).
2. **OAuth broker (native C#)** — use the **official .NET MCP SDK**. FIRST verify the actual package name +
   version + API surface against a real `dotnet restore` (do **not** code against an unverified/hallucinated
   API — the review's `ClientOAuthProvider`/`HttpClientTransport` names must be confirmed against the installed
   package). Provide RFC 8414/9728 discovery + PKCE S256 + refresh-with-rotation + `offline_access`, and an
   `ITokenCache` abstraction (local git-ignored file now; a Key Vault implementation is a documented seam).
   Pin the exact NuGet version; run restore to confirm graph coherence (STK-07).
3. **MCP client wrapper** — `initialize` → `notifications/initialized` → `tools/list` → `tools/call` over
   Streamable HTTP (handle JSON + SSE); SSRF-guarded endpoint; `Mcp-Session-Id`; do not hand-pin the protocol
   version (let the SDK negotiate — STK-04); `CancellationToken` throughout; logs ids/counts only.
4. **Phase-0 CLI** (a small `tools/ProviderDiscovery` console or a backend admin command) — runs the **one-time
   interactive browser login** (redirect `http://localhost:8765/callback`), captures the refresh token into the
   token cache, then calls `tools/list` (+ a sample `tools/call`) and writes a **findings note** (tool names +
   JSON schemas + a suggested field → `ProviderRatingRecord` mapping). This is the tool the **user** runs with
   their creds to clear the Phase-0 go/no-go gate. Without creds it must fail loud with a clear message (P1).
5. **Unit tests** (fake OAuth server + fake MCP transport — **no live calls in CI**): config binding, host-allowlist
   SSRF rejection, token refresh + rotation + skew, MCP `initialize`/`tools/list`/`tools/call` parse (JSON + SSE),
   cancellation.

**Round-1 acceptance:** solution builds; **all existing tests green + new unit tests pass**; the discovery CLI
runs (fails loud without creds; with creds it logs in + lists tools); **no behavior change to the running app**
(synthetic default, all flags off); adversarial review (3 lenses) on the Round-1 code + corrections; `architecturalPlan/TASKS.md` updated.

---

## Round 2 — DEFERRED (do NOT start in this run)

Begins only **after** the user runs the Phase-0 discovery CLI and the real tool schema + go/no-go is confirmed:
- Provider → `ProviderRatingRecord` **mappers** (real field bindings) wired into `MoodysRatingsClient` /
  `MorningstarDbrsRatingsClient` (replace the `null` returns), with result validation (SEC-06) before mapping.
- **Ingestion** into `prism-ratings` + **ARC-01** read-time precedence in `SearchCorpus` (live > synthetic, one
  card per provider) + `DossierAssembler` duplicate-`Provider` fail-loud guard + a `source` field + the §11
  fail-soft-to-synthetic behavior + `IAuditService` entries for fetch/ingest/export (SEC-08).
- **Live smoke validation** against each provider (kept out of CI; needs the creds + interactive login).
