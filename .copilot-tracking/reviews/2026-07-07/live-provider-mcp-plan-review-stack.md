# Adversarial Stack-Fit Review: Live Morningstar + Moody's MCP Integration (PLAN)

**Target:** [.copilot-tracking/planning/2026-07-07/live-provider-mcp-integration.md](../../planning/2026-07-07/live-provider-mcp-integration.md)
**Lens:** Fin Adversary Stack Critic — SDK/protocol accuracy, .NET-vs-Python interop, buildability-as-described.
**Date:** 2026-07-07 · Nothing implemented yet — this attacks the plan's technology claims, not code.

---

## ⭐ VERDICT

**buildable-with-corrections.** The core idea is technically sound — the provider MCP server is real,
an official first-class **.NET** MCP client exists and fits `net9.0`, and the honest connector seam +
ingestion path preserve determinism. But **as described** the plan (a) frames the native-C# option as
hand-rolling a flow the official SDK already ships (STK-01), (b) prescribes a degradation strategy
("leave unreturned fields null") that **will not compile / will NRE** against the deterministic core
(STK-02, the one won't-run defect), and (c) under-specifies headless refresh-token durability on ACA
(STK-03). Fix those three and it is buildable.

**Severity counts:** 1 Critical · 3 High · 3 Medium · 2 Low.

---

## Verification Log (protocol / SDK claims)

| # | Claim under test | Source | Verdict |
|---|---|---|---|
| V1 | A real, usable **.NET** MCP client speaks Streamable HTTP | `modelcontextprotocol/csharp-sdk` `HttpClientTransport` + `HttpTransportMode.StreamableHttp`, `McpClient.CreateAsync`/`ListToolsAsync`/`tool.InvokeAsync` (transports.md, src) | **VERIFIED** |
| V2 | The SDK client does OAuth 2.1 discovery+PKCE+refresh, not just enterprise SSO | `src/…/Authentication/ClientOAuthProvider.cs`: `GetAuthServerMetadataAsync` (RFC 8414), `ProtectedResourceMetadataWellKnownPath` (RFC 9728), `GenerateCodeVerifier`/PKCE S256, `InitiateAuthorizationCodeFlowAsync`, `RefreshTokensAsync` (rotation), `AugmentScopeWithOfflineAccess` (SEP-2207) | **VERIFIED** |
| V3 | The SDK has a headless refresh seam | `ClientOAuthOptions.TokenCache : ITokenCache` (`GetTokensAsync`/`StoreTokensAsync`); `GetAccessTokenSilentAsync` refreshes without calling `AuthorizationRedirectDelegate`; `TokenCacheTests.GetTokenAsync_InvalidAccessTokenTriggersRefresh` asserts `authDelegateCalledAgain == false` | **VERIFIED** |
| V4 | CIMD is supported by both Morningstar and the SDK | Morningstar AS metadata `client_id_metadata_document_supported=true` (fetched, prior); SDK `ClientOAuthOptions.ClientMetadataDocumentUri` + `ApplyClientIdMetadataDocument` gated on `authServerMetadata.ClientIdMetadataDocumentSupported` | **VERIFIED** |
| V5 | Current MCP protocol version is **2025-11-25**, not the plan's `2025-06-18` | modelcontextprotocol.io/specification/versioning: "The **current** protocol version is **2025-11-25**" | **VERIFIED** |
| V6 | Spec now marks **DCR (RFC 7591) deprecated** in favor of CIMD; `resource` (RFC 8707) is a client **MUST** | modelcontextprotocol.io draft authorization spec, Overview §3 + Resource Parameter Implementation | **VERIFIED** |
| V7 | The SDK packages target frameworks compatible with `net9.0` | nuget.org/packages/ModelContextProtocol 1.4.0 → targets **.NET 8.0 / .NET Standard 2.0**; Apache-2.0; latest stable 1.4.0 (Jun 4), prerelease newer exists | **VERIFIED** |
| V8 | The SDK auth surface is actively churning (pin risk) | csharp-sdk commit history: SEP-2350 scope accumulation "2 weeks ago", "de-draft 2026-07-28 terminology" 2 wks, SEP-990 Enterprise Managed Auth "last month" | **VERIFIED** |
| V9 | `DivergenceDecomposer` iterates `Factors` directly (null → NRE) | [Analysis/DivergenceDecomposer.cs](../../../backend/FinancialServices.Api/Analysis/DivergenceDecomposer.cs) `WeightingContribution` `foreach (RatingFactor fa in a.Factors)` | **VERIFIED** |
| V10 | `NotchLadder.ToNotch` throws on any non-ladder label | [Analysis/NotchLadder.cs](../../../backend/FinancialServices.Api/Analysis/NotchLadder.cs) `throw new ArgumentException($"Unknown rating '{label}'.")` | **VERIFIED** |
| V11 | Project has **no** HTTP resilience stack (Polly / Microsoft.Extensions.Http.Resilience) | csproj has none; connectors use typed `HttpClient` + bespoke `UpstreamServiceException` ([EdgarClient.cs](../../../backend/FinancialServices.Api/Connectors/EdgarClient.cs)) | **VERIFIED** |
| V12 | ModelContextProtocol 1.x graph coheres with the existing csproj (STJ 10.0.9 / OpenAI 2.10.0 / Azure.Core) | not restored this session | **REASONED** (needs a real `dotnet restore`) |

---

## Findings

### [STK-01] The plan never mentions the official .NET MCP SDK; D2-b is framed as a hand-roll that does not exist as work — Severity: **High**
- **Target:** Plan §3 D2 option (b), §5 Phase 2, §8 Q1.
- **Claim under test:** D2-b = "Native C# OAuth + MCP client inside the connectors (implement RFC 8414/9728
  discovery, PKCE, refresh, JSON-RPC over Streamable HTTP in .NET) … **reimplement a flow the sample gives
  us for free; more C# to test.**"
- **Reality (VERIFIED):** The official `modelcontextprotocol/csharp-sdk` (Microsoft-collaborated, 4.4k★,
  stable **1.4.0**, `ModelContextProtocol.Core` targets net8.0/netstandard2.0 ⇒ runs on this project's
  `net9.0`) already implements **all of it**:
  - Streamable HTTP + JSON-RPC + `Mcp-Session-Id` + `initialize/initialized/tools/list/tools/call` are
    wrapped by `HttpClientTransport(HttpTransportMode.StreamableHttp)` + `McpClient.CreateAsync` /
    `ListToolsAsync` / `tool.InvokeAsync`. You never touch JSON-RPC or SSE framing.
  - RFC 8414 + RFC 9728 discovery, **PKCE S256**, `authorization_code`, **refresh with rotation**, RFC
    8707 `resource`, and `offline_access` augmentation all live in `ClientOAuthProvider`, wired via one
    property: `HttpClientTransportOptions.OAuth = new ClientOAuthOptions { … }`.
  So D2-b is **not** "reimplement the flow." It is: add one NuGet package, set ~6 options, and write
  exactly two small custom pieces — an `ITokenCache` over Key Vault (STK-03) and a one-time interactive
  login tool. The plan's effort framing ("reimplement … more C# to test") is inverted: **D2-b is now the
  *lower*-effort option than D2-a**, because D2-a stands up a whole second runtime (STK-06).
- **Why it matters:** The entire D2 recommendation ("(a) for speed + faithful reuse … or (b) if you want
  to avoid a second language") is decided on a false premise. A reviewer signing off on D2-a "to avoid
  hand-rolling OAuth" is avoiding work that does not exist.
- **Fix:** Rewrite D2. Name the SDK explicitly (`ModelContextProtocol` / `ModelContextProtocol.Core`).
  Re-score D2-b as: 1 package + `ClientOAuthOptions` + a Key-Vault `ITokenCache` + a login CLI. Make D2-b
  the default recommendation for a .NET shop; keep D2-a only if you specifically want to reuse the
  wealthgen Python code verbatim (and then read STK-06).

### [STK-02] The stated degradation strategy — "leave unreturned fields null" — does not type-check against the deterministic core; it NREs and throws — Severity: **Critical**
- **Target:** Plan §5 Phase 2 ("**any field the provider doesn't return is left null**; if factor weights
  are absent, record that honestly"), §6 R1 ("decomposition degrades honestly … rather than fabricating").
- **Claim under test:** That a mapper can null out missing fields and the deterministic core "degrades
  honestly" instead of faulting.
- **Reality (VERIFIED against the actual signatures):**
  1. **`Factors = null` → `NullReferenceException`.** `ProviderRatingRecord.Factors` and
     `ProviderRating.Factors` are **non-nullable** `IReadOnlyList<RatingFactor>`
     ([IProviderRatingsSource.cs](../../../backend/FinancialServices.Api/Connectors/IProviderRatingsSource.cs),
     [PrismModels.cs](../../../backend/FinancialServices.Api/Models/PrismModels.cs)). `DivergenceDecomposer.WeightingContribution`
     does `foreach (RatingFactor fa in a.Factors)` (V9). A null list throws — the whole reconciliation
     faults, the opposite of "honest degradation." Honest degradation requires
     **`Array.Empty<RatingFactor>()`**, *not* null. With an empty list the algebra is safe: weighting=0,
     input=0, `adj = gap`, `EnsureBucketsReconcile(0,0,gap,gap)` passes, `ResidualShare = 1.0` ⇒
     `IsResidualDominated` true ⇒ the intended "methodology-driven" story.
  2. **`Notch` and `RatingActionDate` are value types — they cannot be null.** `Notch` is `int`,
     `RatingActionDate` is `DateTimeOffset`. "Leave the field null" is type-impossible. The mapper is
     *forced* to synthesize both. A missing action date defaulting to `default(DateTimeOffset)`
     (0001-01-01) is worse than null: `ToProviderRating()` maps `RatingActionDate → InputAsOfDate`, which
     **drives the stale-input red flag (P3)** — so a missing date silently fabricates a "stale" money-moment.
  3. **`Notch = NotchLadder.ToNotch(Letter)` throws on any non-ladder label (V10).** The ladder only knows
     S&P/Fitch canonical, DBRS long-term `(high/mid/low)`, and Moody's `Aaa…C`. If Morningstar's MCP returns
     Morningstar's *own* vocabulary (star ratings, Gold/Silver/Bronze analyst ratings, a 1–5 quantitative
     score) — plausible, since Morningstar's flagship is fund/equity research, not DBRS credit letters — or
     even legit-but-unmapped DBRS scales (`R-1 (high)` short-term, `Pfd-2` preferred), `ToNotch` throws
     `ArgumentException` and the connector faults. R1 says "the provider is dropped," but a drop is a
     *guarded* decision; an unmapped label is an *unguarded throw* unless the mapper wraps it.
  4. **`SourceRef` / `MethodologyDocId` are non-nullable `string`.** "Leave null" violates
     `TreatWarningsAsErrors=true` nullable analysis and risks a null entering the citation/`HtmlEncoder`
     path (`RefsFor(...,"weight")` adds `MethodologyDocId` to evidence refs).
- **Fix:** Replace "leave null" with an explicit sentinel contract in the mapper spec:
  `Factors = Array.Empty<RatingFactor>()` when absent; a **guarded** `TryToNotch` that maps unknown labels
  → `null` record (MISSING_COVERAGE) rather than throwing; `RatingActionDate` **required** from the provider
  (if absent, the issuer is MISSING_COVERAGE, never `asOf`/`default`); `SourceRef` = a real provenance
  string or `""`. Add a Phase-2 unit test that feeds a factor-less, non-ladder-label record through
  `DivergenceDecomposer` and asserts it reconciles (not throws).

### [STK-03] Headless refresh-token durability on ACA is under-specified; the SDK's `ITokenCache` is the seam, and rotation across replicas races into an interactive hang — Severity: **High**
- **Target:** Plan §3 D4, §6 R2 ("service refreshes headlessly from Key Vault").
- **Claim under test:** "The deployed service (ACA, no browser) reads + refreshes from Key Vault."
- **Reality (VERIFIED + REASONED):** The concrete seam the plan omits is `ClientOAuthOptions.TokenCache`
  (`ITokenCache.GetTokensAsync/StoreTokensAsync`; default `InMemoryTokenCache`). Headless refresh works
  **iff** you implement a Key-Vault-backed `ITokenCache` — the plan names Key Vault but not this interface,
  and the two are not interchangeable. Three under-specified hazards:
  1. **Rotation race (REASONED).** Morningstar rotates refresh tokens; the SDK persists the *new* refresh
     token on every refresh (`HandleSuccessfulTokenResponseAsync` → `StoreTokensAsync`, VERIFIED). Each ACA
     replica builds its own `ClientOAuthProvider`. Two replicas refreshing concurrently: replica A rotates
     (old RT invalidated), replica B still holds the old RT → B's refresh **fails**.
  2. **Failure falls into an interactive hang (VERIFIED).** On refresh failure the provider proceeds to the
     401 interactive flow; with no `AuthorizationRedirectDelegate` configured it uses
     `DefaultAuthorizationUrlHandler`, which calls **`Console.ReadLine()`** — on a headless ACA container
     that blocks/returns null and throws. So a rotation race doesn't degrade to MISSING_COVERAGE; it wedges.
  3. **No shared write-back contract.** The Key-Vault `ITokenCache` must be **shared, read-through, and
     write the rotated token back atomically** (Key Vault secret + ETag/optimistic concurrency, or a single
     refresher with a lease), or replicas will clobber each other's rotated tokens.
- **Fix:** Specify a concurrency-safe Key-Vault `ITokenCache` (ETag-guarded write-back), and set a headless
  `AuthorizationRedirectDelegate` that **throws a typed re-login-required error** (never `Console.ReadLine`)
  so a dead refresh token degrades to MISSING_COVERAGE + an ops alert, per R2's runbook. Add a refresh-token
  expiry/revocation alarm. (Applies to D2-a too — the Python broker has the identical race with its own store.)

### [STK-04] The hard-pinned `MCP-Protocol-Version: 2025-06-18` is already one revision stale and should not be hand-set under the SDK — Severity: **High**
- **Target:** Plan §1 table + §5 Phase 2 ("`MCP-Protocol-Version: 2025-06-18`").
- **Claim under test:** That pinning `2025-06-18` is the correct, durable handshake.
- **Reality (VERIFIED):** The **current** MCP protocol version is **2025-11-25** (V5). `2025-06-18` is a
  past ("Final") revision copied from the Python sample. Version negotiation happens at `initialize`, and
  the C# SDK owns the `MCP-Protocol-Version` header and negotiation — hand-setting a stale constant is both
  unnecessary and a drift liability. Under D2-b you must **not** set this header at all; the SDK negotiates.
  Under D2-a (Python) `2025-06-18` will likely still be accepted (backwards-compatible), but you are now
  hand-maintaining a protocol constant that rots and can desync from what the server negotiates.
- **Fix:** Delete the pinned version from the plan's build contract. Under the SDK, let the transport
  negotiate. If a version must be asserted for the sidecar, source it from the sample's SDK, don't
  hardcode it in Prism's plan, and add a Phase-0 note to record what Morningstar actually negotiates.

### [STK-05] Open Q4 omits CIMD (the spec-preferred, Morningstar-advertised, SDK-supported path) and leans on deprecated DCR — Severity: **Medium**
- **Target:** Plan §8 Q4 ("pre-registered `client_id`/`client_secret` … or … dynamic registration
  (Morningstar's `/register` supports it)"), §3 D4 ("store the `refresh_token` **+ client_secret**").
- **Claim under test:** That the registration decision is "pre-registered vs DCR."
- **Reality (VERIFIED):** There are **three** mechanisms, and the plan lists the two lesser ones. The MCP
  auth spec now marks **Dynamic Client Registration (RFC 7591) *deprecated*** and says clients/servers
  **SHOULD** use **Client ID Metadata Documents (CIMD)** (V6). Morningstar advertises
  `client_id_metadata_document_supported=true` (V4), and the C# SDK supports it directly
  (`ClientOAuthOptions.ClientMetadataDocumentUri`). CIMD is a **public client**: the `client_id` is an
  HTTPS URL you host serving a metadata JSON — **no `client_secret` at all**, no registration round-trip.
  That directly shrinks D4's Key-Vault surface: you'd store only the refresh token, not a client secret.
- **Fix:** Add CIMD as Q4 option 3 and note it is spec-preferred + Morningstar-supported + SDK-native, and
  that it removes `client_secret` from D4. Note the tradeoff: CIMD needs a public HTTPS metadata document
  (a small new deploy artifact). For a time-boxed hackathon, pre-registration is still the simplest; DCR is
  fine but flag it as deprecated so it isn't chosen for the wrong reason.

### [STK-06] D2-a relocates rather than removes the hard part — and adds a **third** language runtime with an unspecified resilience contract — Severity: **Medium**
- **Target:** Plan §3 D2 option (a), §4 diagram, §6 R6, §8 Q1.
- **Claim under test:** D2-a "isolates OAuth/secret complexity, minimal C# … faithful reuse of the sample."
- **Reality (REASONED + VERIFIED):**
  - Given STK-01, the sidecar no longer isolates complexity the C# side otherwise couldn't handle — the SDK
    handles it in-process. The sidecar mostly **moves** the OAuth problem into a second process and adds a
    new one: a hand-written REST contract (`GET /ratings/{provider}/{issuerId}?asOf=`), its own container
    image, its own ACA revision/scaling, health checks, and a network hop.
  - This repo already runs a **Node** CopilotKit sidecar (`run-copilot-runtime.bat`) alongside the **C#**
    API. A Python broker makes it **three** runtimes to build, pin, scan, and deploy. If a sidecar is truly
    wanted, the Node sidecar + the **TypeScript** MCP SDK (`@modelcontextprotocol/sdk`, same client OAuth)
    could host the broker with no third language — the plan doesn't consider this.
  - **Resilience contract is unspecified (VERIFIED gap).** The connectors today use typed `HttpClient` and
    fail **loud** with `UpstreamServiceException` (V11) — but the seam's contract is the **opposite**: a
    provider fault must return **`null` → MISSING_COVERAGE**, never throw (ARC-03, per
    [IProviderRatingsSource.cs](../../../backend/FinancialServices.Api/Connectors/IProviderRatingsSource.cs)).
    So the C#→sidecar call needs: a bounded timeout, retry-with-jitter, a circuit breaker, `ct` plumbing
    (P7), **and** a 5xx/timeout→`null` mapping — none of which the project has today (no Polly /
    `Microsoft.Extensions.Http.Resilience` registered, V11). The plan lists "timeouts, retries,
    cancellation" as an open worry, not a spec.
  - "Faithful reuse" hides real work: the sample stores tokens on disk (`data/oauth/<provider>.json`); D4
    demands Key Vault. Porting the Python broker's token store to Key Vault (+ STK-03's concurrency) is net-new
    Python, not free reuse.
- **Fix:** If D2-a survives STK-01, spell out the sidecar's deploy unit (image, ACA revision, identity),
  the exact REST contract + error semantics (5xx/timeout ⇒ `null`), and the C# resilience policy
  (add `Microsoft.Extensions.Http.Resilience`, map faults to `null`, plumb `ct`). Compare honestly against
  D2-b's "one package in-process." Consider hosting the broker in the existing Node sidecar to avoid a third runtime.

### [STK-07] New-dependency pinning + supply chain: the SDK fits `net9.0` but its **auth surface is churning**; verify graph coherence — Severity: **Medium**
- **Target:** Plan §5 Phase 1–2, §8 (no version/pinning section exists).
- **Reality (VERIFIED + REASONED):**
  - **Fit:** `ModelContextProtocol` 1.4.0 targets net8.0/netstandard2.0 → compatible with `net9.0`;
    Apache-2.0; 16.9M downloads (V7). Low TFM friction.
  - **Churn (pin risk):** the auth code you'd depend on is moving fast — SEP-2350 scope accumulation and a
    "de-draft 2026-07-28 terminology" rename landed within ~2 weeks of this review, SEP-990 within a month
    (V8). `ClientOAuthProvider` is `internal`; your public surface is `ClientOAuthOptions` / `ITokenCache` /
    `AuthorizationRedirectDelegate`. Pin to an **exact** version and budget for a surface change if you
    uplift. A newer **prerelease** exists — do not float onto it.
  - **Graph coherence (REASONED, unverified):** prior work already pulls System.Text.Json 10.0.9, OpenAI
    2.10.0, Azure.Core 1.44.1 via pkg06/07, plus a prerelease AG-UI hosting package. Adding
    ModelContextProtocol (which depends on `Microsoft.Extensions.AI.Abstractions` + STJ) *should* cohere but
    was **not** restored this session (V12).
- **Fix:** Add a "Versions & supply chain" section: pin `ModelContextProtocol` exactly (one validated pin,
  per repo convention), run a clean `dotnet restore` to confirm no `NU1605`/STJ downgrade against the
  existing graph, and record the pin + churn caveat. If D2-a, do the same for the Python `mcp`/`httpx` pins.

### [STK-08] Issuer-id ↔ provider-id mapping has no typed home in the record — Severity: **Low**
- **Target:** Plan §6 R8, §5 Phase 0.
- **Reality (VERIFIED):** `ProviderRatingRecord` carries only `IssuerId` — no CUSIP/ISIN/entity-id field.
  Prism's `Issuer` has `Cik` + `SampleBondIsin`. A provider keyed by CUSIP/ISIN/entity-id needs a mapping
  table that lives *outside* the record; an unmapped issuer must return **`null` (MISSING_COVERAGE)**, never
  a guessed key. The plan says this in prose but doesn't place it in the type model.
- **Fix:** Specify where the id map lives (config/Search) and assert the unmapped→null rule in the Phase-2
  mapper tests.

### [STK-09] "Leave null" also violates `TreatWarningsAsErrors` for the non-nullable string refs — Severity: **Low**
- **Target:** Plan §5 Phase 2.
- **Reality (VERIFIED):** `SourceRef` (record) and `MethodologyDocId` (domain) are non-nullable `string`;
  the project builds with `TreatWarningsAsErrors=true` + `Nullable=enable`. Assigning null won't even
  compile cleanly, and a null ref can reach the citation/`HtmlEncoder` path. (Sub-case of STK-02, called out
  separately because it's a compile-time, not runtime, failure.)
- **Fix:** Mapper emits `""` or a real provenance ref; never null.

---

## What's sound (do not re-litigate)

- **The premise is real, not hallucinated.** Morningstar hosts a genuine OAuth 2.1 MCP server; a genuine,
  Microsoft-collaborated **.NET** MCP client exists and targets a framework compatible with `net9.0`. This
  is not a "plausible but won't-run" integration at the transport layer. (VERIFIED)
- **The seam is the right shape.** `IProviderRatingsSource.GetRatingAsOfAsync → ProviderRatingRecord?` with
  `null ⇒ MISSING_COVERAGE`, the `ToProviderRating()` single-conversion choke point, and "live data only
  changes inputs, never the `Analysis/` core" (P2) are exactly how to bolt a live source on without
  destabilizing the deterministic math. (VERIFIED against the code.)
- **D3-a ingestion into Search is the correct call.** Keeping reconciliation off the provider hot path
  preserves determinism, reproducibility, caching, and as-of history, and de-risks the demo. Agreed.
- **The Phase-0 go/no-go gate is the right instinct.** Making "do these servers actually return
  corporate-bond issuer credit ratings (letter + action date + factors)?" a prerequisite is correct — R1 is
  the real risk. (This review sharpens *how* it fails if factors/labels don't fit: STK-02.)
- **The single most important framing correction in the TL;DR is right:** the sample *consumes* a provider
  MCP server; it does not build one. Good — carry that forward.
- **Determinism/no-fabrication instincts are consistent** with the rest of Prism (P1/P2): the plan refuses
  to invent tool names, endpoints, or fields. The corrections below are about *how* "honest degradation" is
  encoded, not whether it's the goal.

---

## Technical unknowns to resolve in Phase 0 (explicit)

1. **Does Morningstar's MCP expose corporate-bond issuer credit ratings at all**, or only fund/equity
   research? Capture real `tools/list` names + JSON schemas. (Gates the whole idea — R1.)
2. **What rating vocabulary does the tool return?** If it isn't S&P/DBRS/Moody's ladder labels,
   `NotchLadder.ToNotch` throws (STK-02.3) — you need a mapping or the provider is out.
3. **Does the payload include a rating-action date and per-factor weights/scores?** No action date ⇒ the
   record is un-constructible without faking `InputAsOfDate` (STK-02.2). No factors ⇒ decomposition is
   residual-only (acceptable, but must be `Array.Empty`, STK-02.1).
4. **Which registration mechanism** — pre-registered, DCR (deprecated), or **CIMD** (preferred, supported)?
   Decides whether a `client_secret` exists to store at all (STK-05).
5. **Protocol version Morningstar actually negotiates** at `initialize` (expect ≥ 2025-06-18; current spec
   is 2025-11-25). Confirm the SDK negotiates cleanly; don't hardcode (STK-04).
6. **Refresh-token lifetime + rotation behavior** (TTL, single-use?) — sizes the STK-03 concurrency design
   and the re-login cadence/runbook.
7. **Moody's:** MCP or REST? URL + auth type from the onboarding pack. If REST, the SDK doesn't apply and
   you're back to a typed `HttpClient` connector (fine) — but confirm before planning an MCP path.
8. **Clean `dotnet restore` with `ModelContextProtocol` pinned** against the existing STJ 10.0.9 / OpenAI
   2.10.0 / Azure.Core graph — confirm no downgrade/NU1605 (STK-07, V12).

---

## Top 3 won't-compile / won't-run risks

1. **STK-02 — `Factors = null` NREs `DivergenceDecomposer`, and `Notch`/`RatingActionDate` can't be null;
   `ToNotch` throws on non-ladder labels.** The plan's "leave fields null" degradation is a runtime fault,
   not honest degradation. (Critical — must be respecified before Phase 2.)
2. **STK-03 — headless refresh wedges on `Console.ReadLine` under replica rotation races** unless a shared,
   ETag-safe Key-Vault `ITokenCache` + a throwing (non-interactive) redirect delegate are specified. (High.)
3. **STK-01/STK-04 — building against a hand-rolled/stale protocol mental model** (ignoring the SDK,
   hardcoding `2025-06-18`) leads to unnecessary bespoke JSON-RPC/OAuth code and handshake drift. (High.)

---
*Marked VERIFIED where grounded in fetched spec text or csharp-sdk source; REASONED where inferred
(multi-replica race, dependency-graph coherence). D2-a's Python-side details were assessed from the plan +
the wealthgen sample description, not a live sidecar.*
