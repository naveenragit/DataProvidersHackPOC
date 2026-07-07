# 🧪 Fin Adversary Stack Critic: Prism pkg 13 Round 1 (live-provider MCP foundation)

**Target:** `backend/FinancialServices.Api/Connectors/Mcp/*`, `tools/ProviderDiscovery/*`,
`FinancialServices.Api.csproj`, `ProviderDiscovery.csproj` — just-written code.
**Lens:** technology-fit / SDK-API accuracy / version coherence / cross-project interop.
**Build state (given):** `net9.0` on .NET 10 SDK, `TreatWarningsAsErrors`, builds clean, 220 tests pass.

---

## VERDICT

- **SDK surface: GO.** Every `ModelContextProtocol.Core 1.4.0` type/method/overload used is the real
  1.4.0 surface — the headline calls are VERIFIED **from SDK source**, the rest from prior reflection on
  the pinned DLL. No hallucinated members, no deprecated overloads, `.Core` is the correct package.
- **Version coherence: AMBER.** No `NU1605`, `.Core` pulls no *new* uplift, but the Phase-0 CLI
  ProjectReferences the whole `Microsoft.NET.Sdk.Web` API and therefore compiles/ships against Cosmos,
  Search, the full Agents stack **including two `1.13.0-preview` packages**, and the ASP.NET Core shared
  framework — a discovery tool that only needs `Connectors/Mcp/*`. Builds green; blast radius is the risk.

No **Blocker/Critical**. No **won't-compile** risk found. 0 Critical / 1 High / 2 Medium / 2 Low.

---

## Verification Log (SDK claim → source → verdict)

| Claim under test | Source | Verdict |
|---|---|---|
| `HttpClientTransport : IClientTransport` + `HttpClientTransportOptions{Endpoint,TransportMode}` + `HttpTransportMode.StreamableHttp` | `src/ModelContextProtocol.Core/Client/HttpClientTransport.cs`; test `HttpClientTransportTests.cs` (`TransportMode = HttpTransportMode.StreamableHttp`) | **VERIFIED (source).** Transport class name is `HttpClientTransport` — **not** `SseClientTransport`/`StreamableHttpClientTransport`. Correct. |
| `McpClient.CreateAsync(IClientTransport, McpClientOptions, ILoggerFactory, ct)` (not `McpClientFactory`) | `src/…/Client/McpClient.Methods.cs`: `public static async Task<McpClient> CreateAsync(IClientTransport clientTransport, …)`; test `await McpClient.CreateAsync(transport, new McpClientOptions{…})` | **VERIFIED (source).** Factory is `McpClient.CreateAsync`; code matches an SDK test verbatim. Returns `Task<McpClient>` (code awaits — correct). |
| `client.CallToolAsync(name, IReadOnlyDictionary<string,object?>, cancellationToken:)` | test `StdioClientTransportTests.cs`: `client.CallToolAsync("echoCliArg", cancellationToken: …)`; reflection (prior) confirms `(string, IReadOnlyDictionary<string,object?>, IProgress?, RequestOptions?, ct)` | **VERIFIED (source+reflection).** Named-arg overload real. |
| `result.Content.OfType<TextContentBlock>().Text` + `TextContentBlock.Text` | `docs/concepts/getting-started.md`: `result.Content.OfType<TextContentBlock>().First().Text`; `ContentBlock` abstract base in `Protocol/ContentBlock.cs` | **VERIFIED (source).** This is the SDK's own documented idiom. |
| `CallToolResult.StructuredContent` is `JsonElement?` → `.GetRawText()` | test `CallToolResultTests.cs`: `StructuredContent = JsonElement.Parse("…")` | **VERIFIED (source).** `JsonElement?.GetRawText()` valid. |
| `McpClientTool.JsonSchema` is `JsonElement` → `.GetRawText()` | prior reflection on 1.4.0 DLL | VERIFIED (reflection). |
| `ITokenCache` = `ValueTask<TokenContainer?> GetTokensAsync(ct)` + `ValueTask StoreTokensAsync(TokenContainer, ct)` | `src/…/Authentication/ITokenCache.cs`; `InMemoryTokenCache.cs`; `OAuth/TokenCacheTests.cs` | **VERIFIED (source).** `FileTokenCache` implements the **`ValueTask`** shape — a common mistake is implementing `Task`; the code got it right. |
| `ClientOAuthOptions.AuthServerSelector` type + null semantics | `src/…/Authentication/ClientOAuthOptions.cs`: `public Func<IReadOnlyList<Uri>, Uri?>? AuthServerSelector`; `ClientOAuthProvider.cs`: `options.AuthServerSelector ?? DefaultAuthServerSelector` | **VERIFIED (source).** Real return is **`Uri?`** where **null = "no suitable server"**. Code declares `Func<…,Uri>` and **throws** instead → see STK-13-02. |
| `ClientOAuthOptions{ClientId,ClientSecret,RedirectUri,Scopes,TokenCache,AuthorizationRedirectDelegate}` | prior reflection on 1.4.0 DLL | VERIFIED (reflection). |
| `TokenContainer{TokenType,AccessToken,RefreshToken,ExpiresIn,Scope,ObtainedAt}` | prior reflection on 1.4.0 DLL; `OAuth/TokenCacheTests.cs` | VERIFIED (source+reflection). |
| `AuthorizationRedirectDelegate` = `(Uri authUri, Uri redirectUri, ct) → Task<string>` (returns auth code) | prior reflection on 1.4.0 DLL | VERIFIED (reflection). |
| Protocol version **not** hand-pinned → SDK negotiates | `McpClientOptions.ProtocolVersion` nullable string, code sets only `ClientInfo` | VERIFIED (reflection). STK-04 satisfied. |
| `.Core` (not umbrella `ModelContextProtocol`) is the right package | client + `HttpClientTransport` + `Authentication/*` all under `src/ModelContextProtocol.Core/…` | **VERIFIED (source).** Umbrella only adds hosting/DI the connector doesn't use. |
| `ModelContextProtocol.Core 1.4.0` multi-targets net8/net9/net10/netstd2.0 → binds net9 build | prior nupkg lib inspection | VERIFIED (reflection). |
| No `NU1605`; MCP.Core adds no new uplift | `dotnet list tools/ProviderDiscovery package --include-transitive` (this session) | **VERIFIED (ran).** See STK-13-03. |
| Live `initialize`/`tools/list` handshake against real Morningstar/Moody's | — | **COULD-NOT-VERIFY** (Phase-0 not yet run; no creds — that is the CLI's purpose). |
| SDK propagates (vs wraps/swallows) an exception thrown from a user `AuthServerSelector` at 1.4.0 | read type + docs, not the invocation try/catch | **COULD-NOT-VERIFY.** Underpins STK-13-02. |

> Source citations are from `modelcontextprotocol/csharp-sdk` **main**, cross-checked against prior
> reflection on the pinned **1.4.0** DLL and the fact the code compiles at 1.4.0. Where only reflection
> backs a claim it is marked "(reflection)".

---

## Findings

### [STK-13-01] Phase-0 discovery CLI ProjectReferences the entire Web API — blast radius incl. prerelease AGUI — Severity: **High**
- **Target:** [tools/ProviderDiscovery/ProviderDiscovery.csproj](../../../tools/ProviderDiscovery/ProviderDiscovery.csproj#L24) (`AzureCosmosDisableNewtonsoftJsonCheck`), [L32](../../../tools/ProviderDiscovery/ProviderDiscovery.csproj#L32) (`ProjectReference` → API).
- **Claim under test:** "a net9 console CLI that reuses `Connectors/Mcp/*` via a project-ref to the API is coherent; the Cosmos check-disable is benign."
- **Reality (ran `dotnet list tools/ProviderDiscovery package --include-transitive`):** the CLI's compile/runtime closure is the **whole API graph**:
  `Microsoft.Azure.Cosmos 3.46.0` (+ `Newtonsoft.Json 13.0.3`), `Azure.Search.Documents 11.6.0`,
  `Azure.Identity 1.13.2`, `Azure.AI.OpenAI 2.1.0` / `OpenAI 2.10.0`, the **full** `Microsoft.Agents.AI`
  1.13.0 stack **including `Microsoft.Agents.AI.Hosting 1.13.0-preview.260703.1` and
  `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore 1.13.0-preview.260703.1`**, `Microsoft.AspNetCore.OpenApi
  9.0.0`, `Microsoft.Extensions.AI/AI.Evaluation/AI.OpenAI 10.6.0`, `Microsoft.ML.Tokenizers 2.0.0`,
  `OpenTelemetry.Api 1.15.3`. Because the API is `Microsoft.NET.Sdk.Web`, the CLI (`Microsoft.NET.Sdk`)
  also inherits the **`Microsoft.AspNetCore.App` FrameworkReference** — so a "discovery CLI" now requires
  the ASP.NET Core shared runtime to start. The `AzureCosmosDisableNewtonsoftJsonCheck=true` at
  ProviderDiscovery.csproj#L24 is the visible symptom: a build-guard hack in a tool that never opens a
  `CosmosClient`.
- **Concrete won't-restore risk:** the Phase-0 go/no-go gate now transitively depends on a **prerelease**
  package (`…AGUI.AspNetCore 1.13.0-preview.260703.1`) resolving from the feed. If that preview build is
  unlisted/yanked (preview packages churn — see the API csproj comment at
  [FinancialServices.Api.csproj#L51](../../../backend/FinancialServices.Api/FinancialServices.Api.csproj#L51)),
  `dotnet restore` for the CLI breaks even though the CLI never touches AG-UI.
- **Fix:** extract `Connectors/Mcp/*` + `Infrastructure/Errors/{ConfigurationException,ReloginRequiredException}`
  + the `PrismOptions.Providers*` slice into a small **`Microsoft.NET.Sdk` class library**
  (`FinancialServices.Providers`, deps = `ModelContextProtocol.Core` + `Microsoft.Extensions.{Logging,Options,Configuration}.Abstractions`
  + `Microsoft.AspNetCore.WebUtilities` for `QueryHelpers`). API and CLI both reference it. That deletes
  the Cosmos check-disable, the ASP.NET Core FrameworkReference, and the prerelease-AGUI coupling from the
  discovery path in one move. (Note: `LoopbackAuthorizationHandler` uses `Microsoft.AspNetCore.WebUtilities.QueryHelpers`
  + `Microsoft.Extensions.Primitives.StringValues`; the new lib needs the `Microsoft.AspNetCore.WebUtilities`
  package explicitly so it does not silently re-drag the shared framework.)

### [STK-13-02] `AuthServerSelector` throws instead of returning the SDK's documented `null` — Severity: **Medium**
- **Target:** [ProviderOAuth.cs#L70-L86](../../../backend/FinancialServices.Api/Connectors/Mcp/ProviderOAuth.cs#L70) (`GuardedAuthServerSelector` returns `Func<IReadOnlyList<Uri>, Uri>`, throws `ConfigurationException` when nothing matches); wired at [ProviderOAuth.cs#L54](../../../backend/FinancialServices.Api/Connectors/Mcp/ProviderOAuth.cs#L54).
- **Claim under test:** "`AuthServerSelector` may throw to fail loud when no in-domain authorization server is advertised."
- **Reality (VERIFIED source `ClientOAuthOptions.cs`):** the property is `Func<IReadOnlyList<Uri>, Uri?>?`
  and its XML doc says the selector returns "**…or null if no suitable server is found**";
  `ClientOAuthProvider` uses `options.AuthServerSelector ?? DefaultAuthServerSelector`. The documented
  "none matched" signal is **`return null`**, not an exception. The code compiles (`Func<…,Uri>` →
  `Func<…,Uri?>?` is a legal return-type-covariant conversion), so this is not a build break — it is a
  **contract divergence**: a `ConfigurationException` is thrown from *inside* the SDK's OAuth discovery.
- **Why it matters / what I could not verify:** on the only Round-1 consumer (the CLI) this is caught by
  the `catch (ConfigurationException)` at [Program.cs#L120](../../../tools/ProviderDiscovery/Program.cs#L120)
  and fails loud — the intended behavior. **COULD-NOT-VERIFY** whether the SDK invokes the selector inside
  a `try/catch` that wraps/swallows non-auth exceptions at 1.4.0; if it does, the fail-loud
  `ConfigurationException` could be reboxed into a generic SDK auth error and lose the setting name.
- **Fix (defensive):** return `null` for "none matched" and let the SDK raise its own no-authorization-server
  error (still fail-closed — Prism never follows an out-of-domain server), **or** keep the throw but add a
  unit test that drives a real `HttpClientTransport` through discovery with an out-of-domain AS and asserts
  the `ConfigurationException` propagates unwrapped. Either way, document the intentional divergence at L70.

### [STK-13-03] net9.0 TFM rides the .NET-10 `Microsoft.Extensions.*` wave — Severity: **Low**
- **Target:** [FinancialServices.Api.csproj#L21](../../../backend/FinancialServices.Api/FinancialServices.Api.csproj#L21) (`ModelContextProtocol.Core 1.4.0`); [ProviderDiscovery.csproj#L28](../../../tools/ProviderDiscovery/ProviderDiscovery.csproj#L28) (`Microsoft.Extensions.Hosting 10.0.1`).
- **Claim under test (attack #2):** "pinning `ModelContextProtocol.Core 1.4.0` pulls transitive
  `System.Text.Json`/`Microsoft.Extensions.*` that conflict with / uplift the existing graph (NU1605)."
- **Reality (ran `--include-transitive`):** the resolved graph unifies at the .NET-10 wave —
  `Microsoft.Extensions.Configuration/DependencyInjection/Logging/Hosting 10.0.1`,
  `…Options 10.0.3`, `…Primitives 10.0.8`, `…DependencyInjection.Abstractions 10.0.9`,
  `Microsoft.Extensions.AI 10.6.0` — but this is driven by **`Microsoft.Agents.AI 1.13.0`** (pkg 06/07) and
  the CLI's own **`Microsoft.Extensions.Hosting 10.0.1`**, **not** by MCP.Core. MCP.Core 1.4.0's
  `Microsoft.Extensions.AI.Abstractions`/`Logging.Abstractions` requirements are *satisfied by* the higher
  10.6.0/10.0.9 already present, so **pinning MCP.Core introduces no new uplift and no conflict** — attack
  #2's premise is **refuted**. Single resolved version per package; **no `NU1605`**; build green.
- **Residual (pre-existing, pkg 06/07):** a `net9.0` runtime executing an almost-entirely-`10.x`
  `Microsoft.Extensions` surface plus a prerelease AGUI. Standing risk, not introduced here.
- **Fix:** none required for MCP. Keep the exact pins; watch for a `10.x` servicing bump that drops the
  `net9.0` compat target. (Optionally centralize versions with `Directory.Packages.props` so the wave is
  pinned in one place.)

### [STK-13-04] CLI `--args` values arrive as boxed `JsonElement` (harmless, note for Round 2) — Severity: **Low**
- **Target:** [Program.cs `ParseCallArgs`](../../../tools/ProviderDiscovery/Program.cs#L138) →
  `JsonSerializer.Deserialize<Dictionary<string, object?>>(json)` handed to
  `CallToolAsync(IReadOnlyDictionary<string, object?>)`.
- **Reality:** STJ deserializes JSON values to **boxed `JsonElement`**, not native CLR `int`/`bool`/`string`.
  The SDK re-serializes the argument dictionary to JSON-RPC via its own options, and `JsonElement`
  round-trips cleanly, so the wire payload is correct — **not a bug**. Flagged only so a Round-2 reader who
  reuses this dictionary in-process does not assume `args["x"] is int`.
- **Fix:** none required for the CLI. If the dictionary is ever consumed in C# (not just forwarded), convert
  with `element.Deserialize<T>()` at the use site.

### [STK-13-05] `ProviderDiscovery` has no test project; CLI shell is unpinned — Severity: **Low** (testing-adjacent)
- **Target:** [FinancialServices.slnx#L5-L8](../../../backend/FinancialServices.slnx#L5) lists
  `SeedData` **and** `SeedData.Tests`, but `ProviderDiscovery` has **no** paired test project.
- **Reality:** `CliArgs`, `RepoLayout`, `DiscoveryReportWriter`, `Program` are exercised by nothing. The
  reusable MCP surface (`McpToolSession`, `ProviderOAuth`, `ProviderMcpEndpointGuard`, `FileTokenCache`,
  `TokenFreshness`, `LoopbackAuthorizationHandler.TryExtractCode`) **does** live in the API project and is
  covered by the +32 new API tests (188 → 220). Only the CLI shell (arg parsing, repo-root walk, report
  writer) is untested.
- **Fix:** optional for a dev-only tool. If pinned: a tiny `ProviderDiscovery.Tests` (net9) covering
  `CliArgs.TryParse` edge cases (missing value, `--timeout` non-positive, provider aliases) and
  `DiscoveryReportWriter.WriteAsync` output shape — no network.

---

## Unverified (needs live/deeper confirmation — do not assume true)
- **Live MCP handshake** (`initialize` → `tools/list` → optional `tools/call`) against the real
  `mcp.morningstar.com` / `api.moodys.com` over OAuth — never run (Phase-0 is credential-gated; that is the
  CLI's job). The transport/OAuth wiring is API-correct; end-to-end success is unproven.
- **SDK exception handling for a throwing `AuthServerSelector`** at 1.4.0 (underpins STK-13-02) — type +
  doc read, invocation guard not read.
- **`main`-vs-1.4.0-tag drift** for the reflection-only surfaces (`ClientOAuthOptions` full property set,
  `TokenContainer`, 4-arg `HttpClientTransport` ctor, `McpClientTool.JsonSchema`) — source citations are
  from `main`; cross-checked against prior 1.4.0-DLL reflection + the clean 1.4.0 build, but not re-reflected
  this session.
- **`Directory.Build.props` interaction:** whether a repo-wide `Directory.Build.props` sets analyzers/langver
  that the tools/ project inherits differently was not re-checked (out of the requested file set).

---

## Top 3 won't-run / fragility risks
1. **STK-13-01 — prerelease AGUI in the CLI restore path.** A discovery tool's `dotnet restore` can break
   on an unlisted `1.13.0-preview` AG-UI package it never uses. Fix: extract a small provider library.
2. **STK-13-02 — throwing `AuthServerSelector` vs the SDK's documented `null`.** Fine on the caught CLI
   path; a latent, unverified reboxing risk if reused headlessly in Round 2. Fix: return `null` or test the
   propagation.
3. **STK-13-01 (runtime facet) — ASP.NET Core FrameworkReference on a console tool.** The CLI needs the
   ASP.NET Core shared runtime to start (inherited from the Web-SDK project-ref); a base-runtime-only box
   fails at launch. Same fix as #1.

---

## What's solid (VERIFIED — do not regress)
- **Every MCP SDK call is the real 1.4.0 surface.** Transport is `HttpClientTransport` +
  `HttpClientTransportOptions{Endpoint, TransportMode = HttpTransportMode.StreamableHttp, OAuth}`
  ([McpToolSessionFactory.cs#L42-L49](../../../backend/FinancialServices.Api/Connectors/Mcp/McpToolSessionFactory.cs#L42));
  session via `McpClient.CreateAsync(transport, ClientOptions, loggerFactory, ct)`
  ([#L79-L82](../../../backend/FinancialServices.Api/Connectors/Mcp/McpToolSessionFactory.cs#L79)) — both match SDK **source**, not guesses.
- **`.Core` is the correct, leaner package** — client + `HttpClientTransport` + `Authentication/*` all live
  in `ModelContextProtocol.Core`; the umbrella `ModelContextProtocol` only adds hosting/DI the connector
  never uses.
- **Result mapping is the SDK's own idiom** — `result.Content.OfType<TextContentBlock>()…Text` +
  `StructuredContent?.GetRawText()` ([McpToolSession.cs#L38-L41](../../../backend/FinancialServices.Api/Connectors/Mcp/McpToolSession.cs#L38)) matches `docs/concepts/getting-started.md`.
- **`ITokenCache` implemented at the right `ValueTask` shape** (a frequent mistake is `Task`);
  `FileTokenCache` write-back is atomic (temp + `File.Move(overwrite)`,
  [FileTokenCache.cs#L98-L100](../../../backend/FinancialServices.Api/Connectors/Mcp/FileTokenCache.cs#L98))
  and the SDK's `StoreTokensAsync`-on-refresh persists rotated refresh tokens (STK-03). `ITokenCache` is the
  correct seam for the Key Vault swap.
- **Protocol version not hand-pinned** — `ClientOptions` sets only `ClientInfo`, so the SDK negotiates at
  `initialize` (STK-04 satisfied). StreamableHttp is the right mode for both remote OAuth servers, and that
  path handles both JSON and SSE responses (`StreamableHttpClientSessionTransport`).
- **No `NU1605`; MCP.Core adds no new uplift** — the 10.x wave is pre-existing (Agents 1.13.0 + Hosting
  10.0.1); MCP.Core's transitive needs are satisfied by it. `net9.0` binds the SDK's `net9.0` build.
- **SSRF guard is real and fail-loud** — exact-host allowlist for the MCP endpoint, registrable-domain bound
  for discovered auth servers, loopback-`http`-only redirect, IP/loopback/link-local rejected
  ([ProviderMcpEndpointGuard.cs](../../../backend/FinancialServices.Api/Connectors/Mcp/ProviderMcpEndpointGuard.cs)) — mirrors the `EdgarClient` pattern.
- **P6 logging hygiene** — the session logs ids + counts only (never tool args/result text); scope minimized
  to `offline_access` ([ProviderOAuth.cs#L18](../../../backend/FinancialServices.Api/Connectors/Mcp/ProviderOAuth.cs#L18)); `ct` plumbed throughout.

---

### Severity counts
0 Critical · 1 High (STK-13-01) · 2 Medium (STK-13-02, and STK-13-01 has a runtime facet) · 2 Low (STK-13-03/04) · 1 Low testing-adjacent (STK-13-05).
> Counting STK-13-01 once as **High**: **0 Critical / 1 High / 1 Medium / 3 Low.**
