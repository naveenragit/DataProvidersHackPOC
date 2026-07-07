# Adversarial Security Review: Prism pkg 13 Round 1 — live-provider MCP foundation (OAuth broker + MCP client + discovery CLI)

**Reviewer:** Fin Adversary Security
**Date:** 2026-07-07
**Scope:** `backend/FinancialServices.Api/Connectors/Mcp/*`, `Infrastructure/PrismOptions.cs` (Providers) + `ServiceCollectionExtensions.cs` (`AddProviderMcp`) + `Infrastructure/Errors/ReloginRequiredException.cs`, `tools/ProviderDiscovery/*`.
**Context honored:** credential-independent FOUNDATION only (NOT wired to the reconciliation read path / provider rating clients — that is Round 2); providers default `Enabled=false` (synthetic stays default); official `ModelContextProtocol.Core` 1.4.0 SDK; P1 fail-loud, P6 no secrets in code/logs; local-dev only. Repo is **public** (`gh repo create DataProvidersHackPOC --public`).

---

## VERDICT: **fix-first** — 2 must-fix before the first live discovery login; foundation is otherwise sound.

The SSRF guard's *actual* protection (exact-host allowlist + leading-dot registrable-domain `EndsWith`) is correct and the classic bypasses are rejected **and tested**. Scope minimization (SEC-07), the typed headless `ReloginRequiredException`, state/PKCE-via-SDK, no-TLS-bypass, and genuinely-inert DI all hold. But the **discovery CLI writes live `tools/call` output to a non-git-ignored, committable path in a public repo** (SEC-13R1-01, High), and the **cleartext refresh token is written world-readable** (SEC-13R1-02, Medium). Both are exactly the "does the CLI/report leak secrets/PII" and "FileTokenCache world-readable?" vectors called out. Fix those two before any live credential touches this code; the rest are hardening.

**Severity counts:** 1 High · 4 Medium · 3 Low · 0 Critical.

---

## Trust Boundary Map

| Boundary crossing | What protects it today | Gap |
|---|---|---|
| Operator config `Prism:Providers:*:McpUrl` → outbound socket | `EnsureAllowedMcpUrl`: https + exact-host allowlist + IP/loopback/link-local reject; called in **both** `McpToolSessionFactory` connect paths before a transport is built | IP helper incomplete (masked by exact host today) — SEC-13R1-05 |
| Provider RFC 9728/8414 discovery → OAuth authorization server | `AuthServerSelector` = `GuardedAuthServerSelector` → `IsAllowedAuthServer` (https + registrable domain, leading-dot `EndsWith`) | `token_endpoint`/`authorization_endpoint`/`registration_endpoint` **inside** the selected AS metadata not re-validated — SEC-13R1-06 |
| Provider AS → browser → **loopback callback** (`http://localhost:8765/`) | `HttpListener` bound to `localhost` only (not `+`/`*`); `state` compared Ordinal; timeout-bounded; single-shot | `state` check degrades open when expected state is empty; any path accepted — SEC-13R1-04 |
| OAuth token (refresh + access) → **disk at rest** | git-ignored `.prism/tokens/*.json`; secrets never logged (booleans/ints only); atomic temp+move | no `0600`/ACL/DPAPI → world-readable on Unix; fixed-name `.tmp` — SEC-13R1-02 |
| Compromised/hostile MCP server → client memory | TLS + host-allowlist (must be the real provider) | no response-size cap, no explicit read timeout; full-materialize `GetRawText()`/text-join — SEC-13R1-03 |
| Live provider data → **repo artifact** | report is auto-generated Phase-0 note | written to `.copilot-tracking/discovery/` which is **committed** (public repo) — SEC-13R1-01 |
| DI registration at boot | `AddProviderMcp` registers one `ILoggerFactory`-only factory singleton; nothing on hot path resolves it; `Enabled=false` default | none — inert, verified |

---

## Findings

### [SEC-13R1-01] Discovery report writes live `tools/call` output to a committable (public-repo) path — Severity: **High**
- **Boundary / Target:** [tools/ProviderDiscovery/DiscoveryReportWriter.cs](../../../tools/ProviderDiscovery/DiscoveryReportWriter.cs#L66-L84) (verbatim `call.Result.StructuredJson` + `call.Result.Text` dump); default out-dir chosen in [tools/ProviderDiscovery/Program.cs](../../../tools/ProviderDiscovery/Program.cs#L108-L112) = `.copilot-tracking/discovery/<date>/<provider>-tools.md`.
- **OWASP / Regulation:** A09 Security Logging & Monitoring Failures (sensitive data written to a persisted artifact); A01 (licensed-data redistribution); P6; financial-domain licensed-data + attribution.
- **Exploit:** `.gitignore` excludes `.prism/` and `.env*` but **not** `.copilot-tracking/` (this repo commits reviews/plans there — verified). The repo is public. An operator running `--provider morningstar --call <ratingTool> --args '{"issuer":"..."}'` writes the tool's **real** structured + text response (licensed rating content, possibly issuer PII) into a tracked markdown file. A routine `git add . && git commit && git push` publishes NRSRO/ESMA-regulated rating data to a public GitHub repo. No redaction, no size cap, no "do not commit" guard on the output.
- **Impact:** Licensed provider data / PII disclosed publicly; licensing + SEC 17g / EU CRA 1060/2009 breach; P6 violation (report is content, not "ids + counts").
- **Fix:** Default the report to a **git-ignored** location — reuse the existing `.prism/` root (e.g. `.prism/discovery/<date>/…`) so it inherits the `.prism/` ignore; and/or omit the sample-call body by default (schemas only) with an explicit `--include-sample-output` opt-in plus an attribution/"do not commit — licensed data" banner in the note header. Do **not** rely on the operator remembering `--out`.

### [SEC-13R1-02] Cleartext refresh token written world-readable (no restrictive file mode) — Severity: **Medium**
- **Boundary / Target:** [backend/FinancialServices.Api/Connectors/Mcp/FileTokenCache.cs](../../../backend/FinancialServices.Api/Connectors/Mcp/FileTokenCache.cs#L84-L113) — `File.WriteAllTextAsync(temp, json)` + `File.Move(temp, _path, overwrite: true)`; temp = `_path + ".tmp"` (fixed name).
- **OWASP / Regulation:** A02 Cryptographic/At-Rest Failures; A04 Insecure Design (long-lived credential on disk); P6.
- **Exploit:** The persisted `PersistedToken` holds `RefreshToken` + `AccessToken` in cleartext (by necessity for dev). On Linux/macOS `File.WriteAllText` creates the file with default perms (umask → typically **0644**), so **any local user** can read the refresh token — a long-lived credential that mints access tokens headlessly. The fixed-name `.tmp` sibling has the same perms and **lingers** if the process dies between write and move (also cross-process there is no lock — the in-proc `_gate` semaphore does not serialize two CLI runs). Git-ignored (`.prism/`) closes the VCS leak but not local-user exposure.
- **Impact:** Local privilege boundary crossing → refresh-token theft → silent headless access to licensed provider data as the operator.
- **Fix:** Create the file with `UnixFileMode.UserRead | UnixFileMode.UserWrite` (0600) — e.g. `File.WriteAllText` then `File.SetUnixFileMode`, or open with a `FileStreamOptions { UnixCreateMode = ... }`. On Windows, prefer `ProtectedData.Protect(…, DataProtectionScope.CurrentUser)` (DPAPI) so the on-disk blob is not plaintext. Randomize the temp name and delete it in a `finally`. (Key Vault remains the production seam — this only hardens the dev store.)

### [SEC-13R1-03] No response-size cap / explicit read timeout on the MCP client — Severity: **Medium**
- **Boundary / Target:** [backend/FinancialServices.Api/Connectors/Mcp/McpToolSessionFactory.cs](../../../backend/FinancialServices.Api/Connectors/Mcp/McpToolSessionFactory.cs#L42-L47) (`HttpClientTransportOptions` sets no `ConnectionTimeout`; SDK owns the `HttpClient` with no `MaxResponseContentBufferSize`); full-materialization in [McpToolSession.cs](../../../backend/FinancialServices.Api/Connectors/Mcp/McpToolSession.cs#L14-L50) — `t.JsonSchema.GetRawText()`, `string.Join("\n", …TextContentBlock.Text)`, `result.StructuredContent?.GetRawText()`.
- **OWASP / Regulation:** A05 Security Misconfiguration / A06 (vulnerable-by-omission resource limits); DoS.
- **Exploit:** A compromised or hostile MCP server (must first defeat TLS + host-allowlist, so this needs *provider* compromise, not an arbitrary attacker) can stream an unbounded `tools/list` schema or `tools/call` SSE/JSON body. The wrapper reads every text block and raw-gets structured content fully into memory before logging length / returning, with no cap → unbounded allocation / OOM. The SDK-owned `HttpClient` default 100 s timeout may not bound a slow-drip SSE stream.
- **Impact:** Client memory exhaustion / hang from a hostile upstream; no backpressure.
- **Fix:** Set `HttpClientTransportOptions.ConnectionTimeout`; on the `ConnectAsync` path inject an `HttpClient` with `MaxResponseContentBufferSize` set; wrap each `ListToolsAsync`/`CallToolAsync` in a per-call `CancellationTokenSource` timeout; cap `text.Length` / structured size (truncate with a marker) before persisting or logging.

### [SEC-13R1-04] Callback CSRF check degrades open when `state` is absent; listener accepts any path — Severity: **Medium**
- **Boundary / Target:** [backend/FinancialServices.Api/Connectors/Mcp/LoopbackAuthorizationHandler.cs](../../../backend/FinancialServices.Api/Connectors/Mcp/LoopbackAuthorizationHandler.cs#L33-L35) (`expectedState` = `""` if the auth URL carried no `state`) and [L100-L105](../../../backend/FinancialServices.Api/Connectors/Mcp/LoopbackAuthorizationHandler.cs#L100-L105) (`string.Equals(returnedState, expectedState, Ordinal)`); path not checked at [L63](../../../backend/FinancialServices.Api/Connectors/Mcp/LoopbackAuthorizationHandler.cs#L63).
- **OWASP / Regulation:** A01 Broken Access Control (CSRF on the OAuth callback).
- **Exploit:** If the SDK's authorization URL ever lacks a `state`, `expectedState=""`; a forged callback to `http://localhost:8765/?code=ATTACKER` with no `state` yields `returnedState=""` and `Equals("","")==true` → **the CSRF gate passes with no state at all**. Not exploitable *today* (ModelContextProtocol 1.4.0 emits state alongside PKCE S256), but the invariant is never asserted — a silent open-fail one SDK change away. Separately, the listener consumes the **first** request on **any** path (not just `/callback`) and is single-shot, so a browser `/favicon.ico` prefetch can consume the one callback and fail the login (reliability, and a local page could deliberately burn the listener).
- **Impact:** Latent authorization-code injection / CSRF; login DoS.
- **Fix:** `if (string.IsNullOrEmpty(expectedState)) { error = "missing expected state"; return false; }` at the top of `TryExtractCode`. Filter on `context.Request.Url?.AbsolutePath == redirectUri.AbsolutePath` and loop `GetContextAsync` until the callback path arrives (still timeout-bounded) instead of accepting the first arbitrary request.

### [SEC-13R1-05] `IsIpLoopbackOrLinkLocal` is incomplete — real protection is the host/domain string checks — Severity: **Medium**
- **Boundary / Target:** [backend/FinancialServices.Api/Connectors/Mcp/ProviderMcpEndpointGuard.cs](../../../backend/FinancialServices.Api/Connectors/Mcp/ProviderMcpEndpointGuard.cs#L156-L188) — `IsIpLoopbackOrLinkLocal` + `IsIPv4LinkLocal`.
- **OWASP / Regulation:** A10 SSRF.
- **Exploit (all currently MASKED, none live today):** The IP helper misses (a) **IPv4-mapped IPv6** — `[::ffff:169.254.169.254]` (cloud metadata) is not caught because `IsIPv6LinkLocal` is false for mapped addrs and `IsIPv4LinkLocal` early-returns when `AddressFamily == InterNetworkV6`; (b) **RFC1918** `10/8`,`172.16/12`,`192.168/16`, **CGNAT** `100.64/10`, **ULA** `fc00::/7` — not checked at all (only loopback + link-local are). Every one of these is nonetheless rejected today because `EnsureAllowedMcpUrl` demands an **exact** host (`mcp.morningstar.com`/`api.moodys.com`) and `IsAllowedAuthServer` demands the leading-dot registrable domain — a bare IP literal matches neither. (Decimal/octal/hex encodings like `https://2130706433/` are moot in .NET: `Uri`/socket do not fold them to `127.0.0.1` the way libcurl does — verified by the parsing model, not just assumed.) The finding is **false confidence**: the helper reads like the SSRF control but isn't; the moment it is reused for a configurable/broader host rule or a third provider with a domain-only rule, `::ffff:169.254.169.254` and `10.0.0.5` become live SSRF.
- **Impact:** Latent metadata/internal-network SSRF if the guard is relaxed or reused; misleading defense-in-depth.
- **Fix:** If keeping an IP guard, parse to `IPAddress`, call `MapToIPv4()` for mapped addrs, and reject loopback ∪ link-local ∪ private ∪ CGNAT ∪ ULA — or, since every allowed host is an exact DNS name, simply **reject all IP literals** (`HostNameType is IPv4 or IPv6 ⇒ throw`) and drop the range logic. Add tests for `[::ffff:169.254.169.254]`, `[::ffff:127.0.0.1]`, `10.0.0.5`, `192.168.1.1`, and a trailing-dot host (`mcp.morningstar.com.`) on both `EnsureAllowedMcpUrl` and `IsAllowedAuthServer`.

### [SEC-13R1-06] Discovered token/authorization/registration endpoints not independently host-re-validated — Severity: **Low**
- **Boundary / Target:** [backend/FinancialServices.Api/Connectors/Mcp/ProviderOAuth.cs](../../../backend/FinancialServices.Api/Connectors/Mcp/ProviderOAuth.cs#L67-L86) — `GuardedAuthServerSelector` validates the AS *issuer* URLs only.
- **OWASP / Regulation:** A10 SSRF (defense-in-depth).
- **Exploit:** `AuthServerSelector` bounds the RFC 9728 authorization-server list to the provider domain, but the `token_endpoint` / `authorization_endpoint` / `registration_endpoint` inside each selected AS metadata document are consumed by the SDK as-is. Because the issuer is already domain-bounded, exploitation requires an in-domain (`*.morningstar.com`) compromise or subdomain takeover — the residual trust is "the provider's own metadata is honest."
- **Impact:** Token/code sent to an in-domain-but-hostile endpoint only under provider-side compromise.
- **Fix:** If the SDK exposes a per-endpoint validation hook, re-run `IsAllowedAuthServer` on each endpoint; otherwise document the residual trust explicitly and pin the AS host set once discovery has run.

### [SEC-13R1-07] CLI outer catch logs `ex.Message` — not guaranteed secret-free — Severity: **Low**
- **Boundary / Target:** [tools/ProviderDiscovery/Program.cs](../../../tools/ProviderDiscovery/Program.cs#L133-L137) — `catch (Exception ex) … {Type}: {Message}`; benign twin in [LoopbackAuthorizationHandler.OpenBrowser](../../../backend/FinancialServices.Api/Connectors/Mcp/LoopbackAuthorizationHandler.cs#L122-L133).
- **OWASP / Regulation:** A09; P6.
- **Exploit:** OAuth secrets travel in headers/bodies, not exception messages, so this is low-likelihood — but an SDK/`HttpClient` exception message *could* embed a request URI or fragment. The catch-all echoes `ex.Message` to stdout.
- **Fix:** Log `ex.GetType().Name` for the catch-all (drop `Message`, or scrub it) to guarantee no fragment reaches the console/CI log.

### [SEC-13R1-08] Redirect delegate binds `HttpListener` to the SDK-supplied URI without re-asserting loopback — Severity: **Low**
- **Boundary / Target:** [backend/FinancialServices.Api/Connectors/Mcp/LoopbackAuthorizationHandler.cs](../../../backend/FinancialServices.Api/Connectors/Mcp/LoopbackAuthorizationHandler.cs#L38-L41) — `prefix` built from `redirectUri` and `listener.Start()` with no re-check.
- **OWASP / Regulation:** A04 Insecure Design (defense-in-depth).
- **Exploit:** `ProviderOAuth.BuildOptions` validates the redirect via `EnsureLoopbackRedirect` and the SDK echoes that configured URI back to the delegate, so binding is loopback today. But the delegate is a public entry point; a future caller supplying a non-loopback `redirectUri` would bind `HttpListener` to a routable interface.
- **Fix:** Call `ProviderMcpEndpointGuard.EnsureLoopbackRedirect`-equivalent (or assert `IPAddress.IsLoopback`/`localhost`) on `redirectUri` before `listener.Start()`.

---

## What's solid (verified, do-not-regress)

- **SSRF core is correct and tested.** `EnsureAllowedMcpUrl` = https + **exact-host** allowlist + IP/loopback/link-local reject; `IsAllowedAuthServer` uses `EndsWith("." + domain)` with the **leading dot**, so the classic suffix spoofs (`mcp.morningstar.com.evil.com`, `api.moodys.com.evil.com`, `notmorningstar.com`) and foreign-domain / non-https / IP forms are all rejected — and `ProviderMcpEndpointGuardTests` pins these. `userinfo@host` (`https://mcp.morningstar.com@evil.com`) is handled because `Uri.Host` extracts the real authority (`evil.com` → rejected).
- **Guard is on every outbound path.** Both `McpToolSessionFactory.ConnectAsync` and `ConnectWithHttpClientAsync` call `EnsureAllowedMcpUrl` before building a transport; the AS selector is wired into `ClientOAuthOptions.AuthServerSelector`.
- **Scope minimization (SEC-07 honored):** `ProviderOAuth.Scopes = ["offline_access"]` only — no `openid`/`email`/`profile`, so no `id_token`/PII is requested. Empty `ClientSecret` → `null` (public client), never an empty-string secret.
- **State + PKCE present:** the callback parses `state` from the auth URL and compares Ordinal (CSRF defense); PKCE S256 is delegated to the SDK's `ClientOAuthProvider` default (verified).
- **Headless never wedges (STK-03):** `HeadlessRedirect` throws typed `ReloginRequiredException` instead of the SDK's `Console.ReadLine`; the exception message carries only the provider name + runbook text — no secrets, no internals.
- **Secret hygiene in logs (P6):** `FileTokenCache` logs `hasRefreshToken` (bool) + `expiresInSeconds` (int) only; parse failure logs no contents and never rethrows raw JSON; `McpToolSession` logs tool name + counts + lengths, never arguments or result text.
- **No TLS bypass anywhere:** grep-verified — no `ServerCertificateCustomValidationCallback`, `DangerousAcceptAnyServerCertificateValidator`, or `ServicePointManager` weakening.
- **DI genuinely inert with flags off:** `AddProviderMcp` registers one `ILoggerFactory`-only factory singleton; nothing on the reconciliation hot path resolves it; `Enabled=false` default keeps the synthetic corpus; no `ValidateOnStart`, so a misconfigured provider fails **loud at first use** (`ConfigurationException` naming the setting), never at boot — matches P1 and planning §11.
- **`.gitignore` covers the token cache + env:** `.prism/`, `**/.prism/`, `.env`, `.env.*` (with `!.env.example`) all excluded. (The gap is `.copilot-tracking/` — SEC-13R1-01, not the token store.)
- **Atomic token write:** temp-sibling + `File.Move(overwrite)` avoids a half-written token file (the perms/temp-name hardening in SEC-13R1-02 is the only outstanding item).

---

## Assumptions that would change the verdict

- If the discovery report default moved under `.prism/` (git-ignored) **or** the sample-call body were opt-in, **SEC-13R1-01 drops to Low** and the verdict becomes "ship Round-1."
- If the token file were created `0600` / DPAPI, **SEC-13R1-02 drops to Low** (dev-only, git-ignored already).
- SEC-13R1-04/05/06 are **latent** — they assume a future SDK change, guard reuse, or provider-side compromise. None is live against the current call sites; they are cheap pre-emptive hardening.
- All of the above assume the Round-1 scope statement holds: **nothing here is on the reconciliation read path.** The moment a Round-2 connector calls `CallToolAsync` on the hot path, SEC-13R1-03 (flood) and the confused-deputy/authZ concerns from the plan review (SEC-02) become live and must be re-reviewed.

---

## Top 3 must-fix before any real data (public repo + live credentials)

1. **SEC-13R1-01** — Default the discovery report to `.prism/discovery/…` (git-ignored) and make the sample-call body opt-in with a "licensed — do not commit" banner. *A public repo + a committed live rating dump is the sharpest exposure here.*
2. **SEC-13R1-02** — Write the token file `0600` (`File.SetUnixFileMode` / `FileStreamOptions.UnixCreateMode`) and DPAPI-wrap on Windows; randomize + `finally`-delete the temp file. *Real refresh token, real disk, world-readable on Unix.*
3. **SEC-13R1-04** — Reject an empty `expectedState` in `TryExtractCode` and match the callback path. *One-line assertion that turns a latent open-fail CSRF gate into a closed one.*
