# Plan — Package 13 Round 1: Live rating-provider MCP connectors (credential-independent foundation)

**Scope:** Round 1 ONLY (implementationPlan/13 §"Round 1"). No reconciliation / SearchCorpus / mapper /
DossierAssembler changes (that is Round 2, gated on the user's Phase-0 discovery run). Synthetic stays the
default + permanent fallback; all provider flags default `Enabled=false`; the running app behaves exactly as
today. Keep the 188 existing tests green. `net9.0`, `Nullable` + `TreatWarningsAsErrors` on.

## Verified foundation (done before planning)
- **SDK pinned:** `ModelContextProtocol.Core` **1.4.0** (stable; 2.0.0 is prerelease-only). Added to
  `FinancialServices.Api.csproj`; clean `dotnet restore` + full-solution `dotnet build` = **0 warn / 0 err**
  (no NU1605 — STK-07 satisfied). Apache-2.0.
- **Real API surface confirmed** (shipped XML doc + reflection on the net9.0 dll — no hallucinated names):
  `ModelContextProtocol.Client.{HttpClientTransport, HttpClientTransportOptions(.Endpoint/.TransportMode/.OAuth/.AdditionalHeaders), HttpTransportMode.StreamableHttp, McpClient.CreateAsync/.ListToolsAsync/.CallToolAsync, McpClientTool, McpClientOptions(.ProtocolVersion nullable→SDK negotiates = STK-04)}`;
  `ModelContextProtocol.Authentication.{ClientOAuthOptions(.ClientId/.ClientSecret/.RedirectUri/.Scopes/.AuthorizationRedirectDelegate/.TokenCache/.ClientMetadataDocumentUri), AuthorizationRedirectDelegate = Task<string> Invoke(Uri authorizationUri, Uri redirectUri, ct), ITokenCache(GetTokensAsync/StoreTokensAsync), TokenContainer(TokenType/AccessToken/RefreshToken/ExpiresIn?/Scope/ObtainedAt), InMemoryTokenCache}`.

## Files (create unless noted EDIT)

### Backend — `FinancialServices.Api`
1. **EDIT** `Infrastructure/PrismOptions.cs` — add `Providers : ProvidersOptions { Morningstar, Moodys : ProviderMcpOptions { bool Enabled; string? McpUrl; string? ClientId; string? ClientSecret; string? RedirectUri } }`. Nullable, no boot gate (fail loud at first use). Keep the existing `ProviderApis` placeholders untouched.
2. `Connectors/Mcp/ProviderMcpEndpointGuard.cs` — SSRF guard (SEC-03), mirrors `EdgarClient.EnsureAllowedHost`:
   - `EnsureAllowedMcpUrl(providerKey, url)` — `https` only + **exact-host allowlist** (`morningstar`→`mcp.morningstar.com`, `moodys`→`api.moodys.com`); reject IP literal / loopback / link-local / off-allowlist → `ConfigurationException`.
   - `EnsureLoopbackRedirect(url)` — redirect URI MUST be loopback `http` (`localhost`/`127.0.0.1`/`[::1]`) — the one loopback exception (native OAuth). Reject anything else.
   - `IsAllowedAuthServer(providerKey, uri)` — for the discovered AS urls the SDK follows (RFC 8414/9728): `https` + not loopback/IP/link-local + **same registrable domain** as the MCP host (`*.morningstar.com` / `*.moodys.com`). Wired via `ClientOAuthOptions.AuthServerSelector`.
3. `Connectors/Mcp/FileTokenCache.cs` — `ITokenCache` file impl: serialize `TokenContainer` (STJ) to a **git-ignored** path (default `%dir%/.prism/tokens/{provider}.json`); atomic write (temp + `File.Move` overwrite); read → `null` when absent. Never logs secret values (P6). XML-doc the Key Vault seam (it is just another `ITokenCache`). SDK calls `StoreTokensAsync` on every refresh ⇒ rotated refresh token persists automatically (STK-03 write-back).
4. `Connectors/Mcp/TokenFreshness.cs` — pure helper: `SecondsUntilExpiry(TokenContainer, now)` + `NeedsRefresh(TokenContainer, now, skew)`. Owns the deterministic skew logic used for CLI reporting/health (the SDK owns the actual HTTP refresh). Directly unit-tested for the "skew" acceptance.
5. `Infrastructure/Errors/ReloginRequiredException.cs` — typed fail-loud error (code `RELOGIN_REQUIRED`) for the headless (non-interactive) redirect path and an empty token cache at runtime — never `Console.ReadLine` (STK-03).
6. `Connectors/Mcp/ProviderOAuthOptionsFactory.cs` — builds `ClientOAuthOptions` from `ProviderMcpOptions`: sets ClientId/ClientSecret/RedirectUri, `Scopes = ["offline_access"]` only (SEC-07 — no openid/email/profile PII), `TokenCache`, `AuthServerSelector` (guard #2), and the chosen `AuthorizationRedirectDelegate`. Two delegates:
   - `Connectors/Mcp/LoopbackAuthorizationHandler.cs` — interactive (CLI): opens the browser to `authorizationUri`, runs an `HttpListener` on the loopback redirect, **validates returned `state` == the `state` in `authorizationUri`** (SEC-04 CSRF), returns `code`. Extract the pure parse/validate into a testable static (`TryExtractCode(expectedState, redirectQuery, out code, out error)`).
   - `HeadlessReloginRequired` (in the factory) — throws `ReloginRequiredException` (STK-03).
7. `Connectors/Mcp/McpToolSession.cs` (+ `IMcpToolSession`, `McpToolInfo`, `McpToolCallResult`, `McpToolSessionFactory`):
   - Factory validates the host (guard #1), builds `HttpClientTransportOptions { Endpoint, TransportMode = StreamableHttp, OAuth? }`, constructs `HttpClientTransport` (optionally over an injected `HttpClient` for tests), `McpClient.CreateAsync` (handshake `initialize`→`initialized`), `ct` throughout. Do **not** set `ProtocolVersion` (SDK negotiates — STK-04). Logs ids/counts only (P6).
   - `ListToolsAsync(ct)` → `IReadOnlyList<McpToolInfo>(Name,Title,Description,JsonSchema)`; `CallToolAsync(name, args, ct)` → `McpToolCallResult(IsError, Text, StructuredJson)`; `IAsyncDisposable`.
8. **EDIT** `Infrastructure/ServiceCollectionExtensions.cs` — add `AddProviderMcp()` registering the guard + session factory (inert singletons; nothing calls them on the hot path). Call from composition root additively. Do **not** touch `AddConnectors` provider-source registrations or the client return values.

### Discovery CLI — `tools/ProviderDiscovery` (NEW project; add to `FinancialServices.slnx`)
9. `ProviderDiscovery.csproj` — Exe, net9.0, own quality gate, `ProjectReference` → API (reuse the guard/OAuth/session — P2/P8).
10. `Program.cs` — args `--provider morningstar|moodys` (+ optional `--call <tool> --args <json>`); bind `Prism__Providers__*` from env (same as SeedData). **Fail loud** with a clear message when `Enabled=false` or `ClientId` blank (P1 — this is the creds-absent gate). SSRF-validate; interactive loopback login → tokens to `FileTokenCache`; connect; `tools/list` (print names + schemas); optional one sample `tools/call`; write a findings note.
11. `DiscoveryReportWriter.cs` — writes `.copilot-tracking/discovery/<date>/<provider>-tools.md`: real tool names + JSON schemas + a **suggested** field→`ProviderRatingRecord` mapping *template* the user fills (mapping itself is Round 2 — no fabrication).

### Config / scripts / docs
12. **EDIT** `.gitignore` — ignore the token store (`.prism/` + `**/.prism/tokens/`).
13. `run-provider-discovery.bat` — repo-root runner (loads `.env` like `run-backend.bat`, runs the CLI).
14. **EDIT** `architecturalPlan/TASKS.md` — package-13 Round-1 entry + implementation-log line.

### Tests — `FinancialServices.Tests/Connectors/Mcp/`
15. `ProviderMcpConfigTests.cs` — `Prism__Providers__*` binds to `PrismOptions.Providers` (flags default false).
16. `ProviderMcpEndpointGuardTests.cs` — accept allowlisted https host; reject off-allowlist host, http, IP literal, loopback, link-local; AuthServer same-domain accept / cross-domain reject; redirect loopback accept / non-loopback reject.
17. `FileTokenCacheTests.cs` — round-trip; **rotation** (store rt1 then rt2 → read rt2); absent→null; file has no cleartext in logs (CapturingLogger asserts no secret logged).
18. `TokenFreshnessTests.cs` — skew boundaries (expired, within-skew→refresh, fresh).
19. `McpToolSessionTests.cs` — handshake + `tools/list` + `tools/call` parse over a **fake `HttpMessageHandler`** serving real Streamable-HTTP **JSON** and **SSE (`text/event-stream`)** bodies (no live net); `ct` cancellation path.
20. `LoopbackAuthorizationHandlerTests.cs` — `TryExtractCode`: valid state→code; **state mismatch→reject** (SEC-04); OAuth `error` param surfaced.

## Guardrails re-asserted
- P1 no fabrication: the CLI *discovers* tool names/schemas; the mapper stays Round 2. Creds-absent = fail loud.
- P2 deterministic core untouched; P6 secrets never logged/committed (token file git-ignored, scopes minimized);
  P7 `ct` plumbed. STK-02 (Factors/Notch/RatingActionDate sentinels) is a Round-2 mapper concern — not in this run.

## Acceptance (Round 1)
Solution builds; 188 existing + new tests green; CLI fails loud without creds and (with creds) logs in + lists
tools; no running-app behavior change (all flags off = today's synthetic demo); 3-lens adversarial review +
Critical/High fixes; `TASKS.md` updated.
