# Adversarial Stack-Fit Review — Fin Copilot Kit

- **Reviewer role:** Fin Adversary Stack Critic (adversarial technology-fit red team)
- **Date:** 2026-07-06
- **Stack under test:** C# / ASP.NET Core (.NET 9) + Microsoft Agent Framework (`Microsoft.Agents.AI`) over Azure AI Foundry · React 18 (shadcn/ui, TanStack Query v5 + Table, CopilotKit, Tailwind) · CopilotKit Node runtime sidecar (`@copilotkit/runtime`)
- **Method:** Every SDK call, export, and package version was assumed hallucinated until confirmed against official docs (Microsoft Learn, NuGet, npm) or first-party source (`microsoft/agent-framework`, `CopilotKit/CopilotKit`). Anything without a source is marked **Unverified**.
- **Verdict headline:** The package *version strings* are mostly real GA releases — but the **agent invocation surface and the CopilotKit Azure adapter are hallucinated**, so neither the backend nor the sidecar compiles/runs as written.

**Severity counts:** 🔴 Blocker **2** · 🟠 Major **5** · 🟡 Minor **3**

---

## 1. Verification Log (claim → source → verdict)

| # | Claim under test | Source | Verdict |
|---|---|---|---|
| V1 | `new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential())` | [Azure.AI.Projects README (NuGet 2.0.1)](https://www.nuget.org/packages/Azure.AI.Projects) | ✅ **Correct** — exact constructor shown in docs. |
| V2 | `AIProjectClient.GetAIAgentAsync("Name", …)` returns an `AIAgent` | Azure.AI.Projects README (agents via `AgentAdministrationClient` / `GetPersistentAgentsClient()`); no `GetAIAgentAsync` member | ❌ **Not a member of `AIProjectClient`** in the Azure SDK. |
| V3 | Real way to bridge Foundry → `AIAgent` | `microsoft/agent-framework` `dotnet/.../PersistentAgentsClientExtensions.cs`; `docs/decisions/0011-create-get-agent-api.md` | ⚠️ `GetAIAgent(Async)` is an **extension** in **`Microsoft.Agents.AI.AzureAI`** (V2, `AIProjectClient`-based, name overload) or **`Microsoft.Agents.AI.AzureAI.Persistent`** (V1, `PersistentAgentsClient`-based, **`[Obsolete]`**). Requires a package the csproj does **not** reference. |
| V4 | `AIAgent.RunAsync(string, …)` exists | [Learn: AIAgent.RunAsync](https://learn.microsoft.com/dotnet/api/microsoft.agents.ai.aiagent.runasync?view=agent-framework-dotnet-latest) | ✅ **Method exists**, accepts `string`. |
| V5 | `RunAsync` returns `AgentRunResponse` with `.Text` | Same Learn page shows `Task<Microsoft.Agents.AI.AgentResponse>` (pkg `Microsoft.Agents.AI.Abstractions` **rc2**); `AgentRunResponse` not found in `.NET` source search | ⚠️ **Return type documented as `AgentResponse`, not `AgentRunResponse`.** `.Text` plausible; exact GA type name **Unverified**. |
| V6 | `Microsoft.Agents.AI` `1.0.0` is a real GA | [NuGet version list](https://www.nuget.org/packages/Microsoft.Agents.AI/#versions-body-tab) — `1.0.0` (3 mo ago), current `1.13.0` | ✅ **Real** (but 13 minors stale). |
| V7 | `Azure.AI.Projects` `1.0.0` is current GA | [NuGet version list](https://www.nuget.org/packages/Azure.AI.Projects/#versions-body-tab) — `1.0.0` (9 mo ago); current **`2.0.1`**, install shown as `--prerelease` | ⚠️ **Real but superseded major** (1→2 was a breaking agent-API rewrite). |
| V8 | `Azure.AI.Agents.Persistent` `1.0.0` real | [NuGet page](https://www.nuget.org/packages/Azure.AI.Agents.Persistent) — current `1.1.0` | ✅ **Real** (stale). |
| V9 | `Azure.AI.ContentSafety` `1.0.0` real + `CategoriesAnalysis[].Severity/Category` | [NuGet page](https://www.nuget.org/packages/Azure.AI.ContentSafety) (GA 12/12/2023) | ✅ **Real & surface correct.** |
| V10 | `Microsoft.Azure.Cosmos` `SerializerOptions{PropertyNamingPolicy}` uses System.Text.Json | [Learn: Cosmos .NET best practices](https://learn.microsoft.com/azure/cosmos-db/best-practice-dotnet#managing-newtonsoftjson-dependencies) + [UseSystemTextJsonSerializerWithOptions](https://learn.microsoft.com/dotnet/api/microsoft.azure.cosmos.cosmosclientoptions.usesystemtextjsonserializerwithoptions?view=azure-dotnet) | ❌ **Default serializer is Newtonsoft.Json.** STJ needs `UseSystemTextJsonSerializerWithOptions` (pkg **v3.58+**); csproj pins **3.46.0**. |
| V11 | `.NET 9` `AddOpenApi()`/`MapOpenApi()`, doc at `/openapi/v1.json` | [Learn: What's new in ASP.NET Core 9 – OpenAPI](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-9.0#openapi); [OpenAPI overview](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview) | ✅ **Correct** — and **no Swagger UI** ships by default. |
| V12 | Root instructions: "OpenAPI docs via Swashbuckle / built-in OpenAPI at `/swagger`" | Same Learn pages — Swashbuckle removed from .NET 9 templates; built-in OpenAPI serves JSON only, no `/swagger` | ❌ **Wrong** (self-contradictory vs the correct Program.cs). |
| V13 | `@copilotkit/runtime` exports `CopilotRuntime`, `copilotRuntimeNodeHttpEndpoint` | [CopilotKit self-hosting docs](https://docs.copilotkit.ai/backend/copilot-runtime) | ✅ **Real** (v1/legacy Node factory). |
| V14 | `@copilotkit/runtime` exports `AzureOpenAIAdapter` | [npm 1.62.2](https://www.npmjs.com/package/@copilotkit/runtime); `CopilotKit/CopilotKit` `examples/v1/next-openai/.../dynamic-service-adapter.ts` (Azure path imports **`OpenAIAdapter`**) | ❌ **No `AzureOpenAIAdapter` export.** Azure OpenAI is wired through `OpenAIAdapter` + an `AzureOpenAI` client. |
| V15 | `@copilotkit/runtime` `^1.5.0` | npm — current `1.62.2`; `^1.5.0` resolves to latest 1.x | ⚠️ **Resolvable but drifts** across a major API shift (v2 `agents` model). |
| V16 | shadcn `components.json` `style:new-york` + `baseColor:slate` vs indigo dark vars | `templates/frontend-design-system/components.json` + `index.css` (`--primary: 239 84% 67%` = indigo) | ⚠️ **Inconsistent** — `baseColor` drives CLI scaffolding; re-`init` can overwrite the indigo theme with slate. |
| V17 | `tailwind.config.js` `require('tailwindcss-animate')` is installed | react-frontend.instructions "Mandatory Setup" npm lists; no frontend `package.json` in template | ❌ **Never installed** — not in any documented `npm install`. |
| V18 | TanStack Query **v5** naming (`isPending`/`isError`, not v4 `isLoading`) | react-frontend.instructions ("Handle `isPending`/`isError`") | ✅ **Correct v5 usage.** |
| V19 | Frontend has a SignalR client for the mandated real-time hubs | `templates/frontend-design-system/*` — no `@microsoft/signalr`, no provider/hook | ❌ **Missing dependency & client.** |
| V20 | `AzureOptions` exposes every field the samples read | csharp-backend.instructions `AzureOptions` lacks `ContentSafetyEndpoint`/`DocumentIntelligenceEndpoint`/`Speech*` used by azure-services.instructions | ⚠️ **Cross-file option drift** (samples reference undefined properties). |

---

## 2. Findings

### 🔴 STK-01 — `AIProjectClient.GetAIAgentAsync(...)` is a hallucinated agent surface (won't compile)
- **Severity:** Blocker
- **Target:** [templates/csharp-api/FinancialServices.Api/Agents/PortfolioAnalysisAgent.cs](../../../templates/csharp-api/FinancialServices.Api/Agents/PortfolioAnalysisAgent.cs#L33-L34); csproj [line 16](../../../templates/csharp-api/FinancialServices.Api/FinancialServices.Api.csproj#L16) and [line 18](../../../templates/csharp-api/FinancialServices.Api/FinancialServices.Api.csproj#L18); mirrored in [azure-services.instructions.md](../../../.github/instructions/coding-standards/azure-services.instructions.md) and [csharp-backend.instructions.md](../../../.github/instructions/coding-standards/csharp-backend.instructions.md)
- **Claim under test:** `AIAgent agent = await projectClient.GetAIAgentAsync("PortfolioAnalysisAgent", cancellationToken: ct);` where `projectClient` is `Azure.AI.Projects.AIProjectClient`.
- **Reality (sourced):**
  - The Azure SDK `AIProjectClient` exposes **no `GetAIAgentAsync`**. Its agent access points are `projectClient.AgentAdministrationClient.*` and `projectClient.GetPersistentAgentsClient()` (Azure.AI.Projects README, NuGet 2.0.1).
  - `GetAIAgent(Async)` is an **Agent Framework extension method** that lives in a **separate integration package**:
    - `Microsoft.Agents.AI.AzureAI` (V2) — extends `AIProjectClient`, has a **name/`ChatClientAgentOptions`** overload (`docs/decisions/0011-create-get-agent-api.md`).
    - `Microsoft.Agents.AI.AzureAI.Persistent` (V1) — extends `PersistentAgentsClient`, is now `[Obsolete("Please use the latest Foundry Agents service via the Microsoft.Agents.AI.AzureAI package.")]` (`PersistentAgentsClientExtensions.cs`).
  - The csproj references only `Microsoft.Agents.AI` (core) + `Azure.AI.Projects` + `Azure.AI.Agents.Persistent`. **Neither `Microsoft.Agents.AI.AzureAI` nor `.AzureAI.Persistent` is referenced**, so the extension is not in scope → `CS1061 'AIProjectClient' does not contain a definition for 'GetAIAgentAsync'`.
  - Compounding: the V2 integration targets `Azure.AI.Projects` **2.x** (`AgentReference`/`AgentRecord`), but the csproj pins **1.0.0** → adding the package alone still yields a version conflict.
- **Fix:** Add `<PackageReference Include="Microsoft.Agents.AI.AzureAI" Version="1.*" />`, bump `Azure.AI.Projects` to `2.*`, `using Microsoft.Agents.AI.AzureAI;`, then call the real surface, e.g.:
  ```csharp
  var projectClient = new AIProjectClient(new Uri(_options.AiProjectEndpoint), new DefaultAzureCredential());
  AIAgent agent = await projectClient.GetAIAgentAsync(_options.PortfolioAgentName /* or agent id */, cancellationToken: ct);
  var response = await agent.RunAsync(prompt, cancellationToken: ct); // see STK-02 for the return type
  ```
  Verify the exact overload/namespace against the installed `Microsoft.Agents.AI.AzureAI`.

### 🔴 STK-03 — CopilotKit `AzureOpenAIAdapter` is not an exported symbol (sidecar won't start)
- **Severity:** Blocker
- **Target:** [templates/csharp-api/copilot-runtime/server.ts](../../../templates/csharp-api/copilot-runtime/server.ts#L14) (import) and [L24-L29](../../../templates/csharp-api/copilot-runtime/server.ts#L24-L29) (`new AzureOpenAIAdapter(...)`)
- **Claim under test:** `import { AzureOpenAIAdapter } from '@copilotkit/runtime'`.
- **Reality (sourced):** `@copilotkit/runtime` (current 1.62.2) does **not** export `AzureOpenAIAdapter`. In the CopilotKit repo the Azure OpenAI path is implemented with **`OpenAIAdapter`** (`examples/v1/next-openai/src/lib/dynamic-service-adapter.ts`: `const { OpenAIAdapter } = await import("@copilotkit/runtime")`). The named import therefore resolves to `undefined`, and `new AzureOpenAIAdapter(...)` throws `TypeError: AzureOpenAIAdapter is not a constructor` at boot.
- **Fix:** Use `OpenAIAdapter` with an Azure-configured `openai` client (add `openai` to deps):
  ```ts
  import { CopilotRuntime, OpenAIAdapter, copilotRuntimeNodeHttpEndpoint } from '@copilotkit/runtime'
  import { AzureOpenAI } from 'openai'
  const openai = new AzureOpenAI({
    endpoint: process.env.AZURE_OPENAI_ENDPOINT!,
    apiKey: process.env.AZURE_OPENAI_API_KEY!,
    apiVersion: process.env.AZURE_OPENAI_API_VERSION ?? '2024-12-01-preview',
    deployment: process.env.AZURE_OPENAI_DEPLOYMENT!,
  })
  const serviceAdapter = new OpenAIAdapter({ openai, model: process.env.AZURE_OPENAI_DEPLOYMENT! })
  ```

### 🟠 STK-02 — `AgentRunResponse` return type does not match the documented `AgentResponse`
- **Severity:** Major (compile risk; partially Unverified)
- **Target:** [PortfolioAnalysisAgent.cs L36-L40](../../../templates/csharp-api/FinancialServices.Api/Agents/PortfolioAnalysisAgent.cs#L36-L40)
- **Claim under test:** `AgentRunResponse response = await agent.RunAsync(...); return response.Text;`
- **Reality (sourced):** The Learn reference for `AIAgent.RunAsync(string, …)` (and `ChatClientAgent.RunAsync`) documents the return type as **`Task<Microsoft.Agents.AI.AgentResponse>`** (assembly `Microsoft.Agents.AI.Abstractions`, tagged v1.0.0-rc2). A `.NET` source search for `AgentRunResponse` returned no type definition. An **explicit** `AgentRunResponse` annotation will not compile if the GA type is `AgentResponse`.
- **Caveat:** The Agent Framework renamed response types across previews and `agentRunResponse` appears as a variable name in official migration docs, so the GA type name is **Unverified**. Do not treat `.Text` as confirmed.
- **Fix:** Use `var response = await agent.RunAsync(...)` and read the text via the property confirmed in the installed package (`response.Text`), rather than hard-coding `AgentRunResponse`.

### 🟠 STK-04 — Cosmos "System.Text.Json serializer" assumption is false; decimal/enum round-trips run through Newtonsoft
- **Severity:** Major
- **Target:** csproj [line 20](../../../templates/csharp-api/FinancialServices.Api/FinancialServices.Api.csproj#L20); `CosmosClientOptions { SerializerOptions = … CamelCase }` in [csharp-backend.instructions.md](../../../.github/instructions/coding-standards/csharp-backend.instructions.md) and [azure-services.instructions.md](../../../.github/instructions/coding-standards/azure-services.instructions.md)
- **Claim under test:** Attack #4 — Cosmos + C# `decimal` round-trip "with the System.Text.Json Cosmos serializer."
- **Reality (sourced):** Cosmos .NET SDK v3's **default serializer is Newtonsoft.Json**. `CosmosClientOptions.SerializerOptions` (`CosmosSerializationOptions`) only tunes the *Newtonsoft* serializer (naming/indent/ignore-null). System.Text.Json requires `CosmosClientOptions.UseSystemTextJsonSerializerWithOptions` (or `CosmosClientBuilder.WithSystemTextJsonSerializerOptions`) — *"If no options are specified, Newtonsoft.Json will be used."* That property exists only in **Cosmos v3.58.0+**, but the csproj pins **3.46.0**, so STJ can't even be selected. Consequences:
  - DTOs authored for System.Text.Json (records, STJ `JsonStringEnumConverter`, `System.Text.Json.Serialization.JsonPropertyName/JsonIgnore`) are **silently ignored** by Cosmos → e.g. `AssetClass` persists as an int, not the string a STJ converter would emit; STJ `required`/`init` semantics differ from Newtonsoft binding.
  - `decimal` (`MarketValue`, `Weight`) survives Newtonsoft, but the *serializer identity* differs from the ASP.NET Core response serializer (STJ), so the same record serializes two different ways on the wire vs in the DB.
  - Newtonsoft.Json 13.0.3+ should be an **explicit** direct dependency (SDK compiles against 10.x, NU1109 downgrade risk); it is absent from the csproj.
- **Fix:** Either bump `Microsoft.Azure.Cosmos` to `3.58+` and set `UseSystemTextJsonSerializerWithOptions` (aligning with the STJ DTO story), or explicitly accept Newtonsoft and add `Newtonsoft.Json` `13.0.4` + Newtonsoft attributes. Pick one serializer and make DTO attributes match it.

### 🟠 STK-07 — `tailwind.config.js` requires `tailwindcss-animate`, which is never installed (frontend build fails)
- **Severity:** Major
- **Target:** [templates/frontend-design-system/tailwind.config.js](../../../templates/frontend-design-system/tailwind.config.js#L97) (`plugins: [require('tailwindcss-animate')]`)
- **Reality:** No frontend `package.json` exists in the template, and the react-frontend.instructions "Mandatory Setup" `npm install` / `npm install -D` lists (`tailwindcss@^3 postcss autoprefixer @vitejs/plugin-react` + the runtime deps) **omit `tailwindcss-animate`**. A bare `require('tailwindcss-animate')` then throws `Cannot find module 'tailwindcss-animate'` and Tailwind/PostCSS aborts — the exact "white unstyled page" failure the instructions warn about.
- **Fix:** Add `tailwindcss-animate` to the mandatory `npm install -D` list (or switch to `tw-animate-css`, which newer shadcn `new-york` scaffolds use, and update the config import accordingly).

### 🟠 STK-09 — SignalR is mandated for real-time, but the frontend ships no SignalR client
- **Severity:** Major
- **Target:** frontend template ([App.tsx](../../../templates/frontend-design-system/App.tsx), design-system dir); backend hubs in [csharp-backend.instructions.md](../../../.github/instructions/coding-standards/csharp-backend.instructions.md) and [azure-services.instructions.md](../../../.github/instructions/coding-standards/azure-services.instructions.md)
- **Reality:** The backend standard prescribes SignalR hubs (`/hubs/transcription`, live workflow status) and `workflowData.ts` references SignalR, but the frontend has **no `@microsoft/signalr` dependency, provider, or hook**, and it is absent from every "Mandatory Setup" install list. Any real-time feature is unconsumable on the client. (The Vite proxy does set `ws: true` on `/api`, implying WebSocket intent that nothing fulfills.)
- **Fix:** Add `@microsoft/signalr` to the frontend deps and ship a typed `useHubConnection`/provider, or drop the SignalR mandate in favor of the WebSocket path the proxy hints at — pick one and wire it end-to-end.

### 🟠 STK-10 — Cross-language contract drift: C# records vs hand-written TS with no generated client
- **Severity:** Major
- **Target:** C# DTOs in [csharp-backend.instructions.md](../../../.github/instructions/coding-standards/csharp-backend.instructions.md) (`PortfolioPosition`, `PortfolioSummary`) vs TS `src/types` + table/formatters in [react-frontend.instructions.md](../../../.github/instructions/coding-standards/react-frontend.instructions.md)
- **Reality:** The API already emits OpenAPI at `/openapi/v1.json` (STK-06/V11), yet the frontend types are **hand-authored** with no NSwag/Kiota/`openapi-typescript` generation. Concrete drift scenario:
  - C# `PortfolioPosition.Weight` is `decimal` `[Range(0,1)]`, serialized as a JSON number; the TS grid does `formatPercent(getValue<number>() * 100)`. If the backend later returns basis points or renames it `weightPct`, TypeScript compiles clean and the UI silently renders `NaN%`/`0%`.
  - C# `enum AssetClass` serializes as `0..3` by default (STJ) while a hand-written TS union `'Equity' | 'FixedIncome' | …` expects strings → the grid shows raw integers with no compile-time signal.
- **Fix:** Generate the TS client/types from `/openapi/v1.json` (Kiota or `openapi-typescript`) in CI so schema changes break the build instead of the UI.

### 🟡 STK-05 — NuGet versions are real GA but stale/superseded (not "invented")
- **Severity:** Minor (correctness of the *claim*; interacts with STK-01)
- **Target:** [FinancialServices.Api.csproj L14-L26](../../../templates/csharp-api/FinancialServices.Api/FinancialServices.Api.csproj#L14-L26)
- **Reality (sourced):** `Microsoft.Agents.AI 1.0.0` (current 1.13.0), `Azure.AI.Agents.Persistent 1.0.0` (current 1.1.0), `Azure.AI.ContentSafety 1.0.0`, and `Azure.AI.Projects 1.0.0` are **all real published GA versions** — so the "invented versions" hypothesis is largely **disproved**. The trap is `Azure.AI.Projects 1.0.0`: current GA is **2.0.1** (a breaking rewrite) and pinning 1.0.0 is what makes STK-01's fix (the 2.x-targeting `Microsoft.Agents.AI.AzureAI` integration) impossible without a bump.
- **Fix:** Track the current majors deliberately (`Azure.AI.Projects 2.*`, `Microsoft.Agents.AI 1.13.*`) and pin the Agent Framework Foundry integration package alongside them.

### 🟡 STK-06 — Root instructions claim Swashbuckle + `/swagger`; .NET 9 built-in OpenAPI does neither
- **Severity:** Minor (docs mislead; the reviewed Program.cs is correct)
- **Target:** `copilot-instructions.md` ("OpenAPI docs via Swashbuckle / built-in OpenAPI at `/swagger`") vs [Program.cs L16 / L41-L44](../../../templates/csharp-api/FinancialServices.Api/Program.cs#L41-L44)
- **Reality (sourced):** .NET 9 removed Swashbuckle from templates; `Microsoft.AspNetCore.OpenApi` `AddOpenApi()`/`MapOpenApi()` generate the document at **`/openapi/v1.json`** and ship **no Swagger UI**. Program.cs is right; the root doc's "`/swagger`" is wrong and will send developers to a 404. (Minor extra: `AddEndpointsApiExplorer()` at Program.cs L15 is a Minimal-API/Swashbuckle relic — harmless, unnecessary for controllers + built-in OpenAPI.)
- **Fix:** Correct the instruction to "OpenAPI JSON at `/openapi/v1.json`; add Scalar/Swagger UI separately if an interactive UI is needed."

### 🟡 STK-08 — shadcn `new-york`/`slate` config vs hand-authored indigo dark theme
- **Severity:** Minor
- **Target:** [components.json L3/L9](../../../templates/frontend-design-system/components.json#L3-L9) vs [index.css L20](../../../templates/frontend-design-system/index.css#L20)
- **Reality:** `baseColor: "slate"` tells the shadcn CLI which palette to scaffold, but the template ships its own indigo (`--primary: 239 84% 67%`) dark variables. The react-frontend "Mandatory Setup" step 3 instructs running `npx shadcn@latest init`, which can **regenerate `index.css` with slate defaults and overwrite the indigo theme**. `style:new-york` also changes component defaults vs the hand-built look.
- **Fix:** Either regenerate the CSS variables from a `slate`/indigo-consistent `init` (don't hand-edit + re-init), or set `baseColor` to match the shipped palette and tell users **not** to re-run `init` over the committed theme.

---

## 3. Unverified list (no authoritative source found — do not assert correct)

1. **Exact `RunAsync` GA return type & `.Text`** for pinned `Microsoft.Agents.AI 1.0.0` — Learn shows `AgentResponse` at rc2; `AgentRunResponse` unconfirmed in .NET source. (STK-02)
2. **Exact `GetAIAgentAsync` overload/namespace** in `Microsoft.Agents.AI.AzureAI` (V2) — a name-based overload is documented in a decision record, but the precise signature (`(string, CancellationToken)`) and its compatibility with `Azure.AI.Projects 1.0.0` vs `2.x` were not pinned. (STK-01)
3. **`@copilotkit/runtime` `CopilotRuntime({ actions: () => [...] })`** — the v1 `actions` constructor option was not re-confirmed against 1.62.x; current docs foreground the `agents: {}` model. `copilotRuntimeNodeHttpEndpoint({ endpoint, runtime, serviceAdapter })` shape is consistent with docs but the Node-HTTP overload was not byte-verified.
4. **Package versions not individually fetched** (assumed real, not sourced this pass): `Azure.Identity 1.13.1`, `Azure.Monitor.OpenTelemetry.AspNetCore 1.3.0`, `Microsoft.AspNetCore.OpenApi 9.0.0`, `Microsoft.Azure.Cosmos 3.46.0` (exact), `Azure.Search.Documents 11.6.0` (exact).
5. **`VectorizableTextQuery` / `SearchOptions.VectorSearch`** shape in `Azure.Search.Documents 11.6.0` (azure-services.instructions hybrid-search sample) — not verified against the pinned 11.6.0 API.
6. **`AzureOptions` completeness** — samples in azure-services.instructions read `ContentSafetyEndpoint`, `DocumentIntelligenceEndpoint`, `Speech*`, which are **not defined** on the `AzureOptions` shown in csharp-backend.instructions (internal drift; would not compile as written). (V20)

---

## 4. Top 3 Won't-Compile / Won't-Run Risks

1. **Backend won't compile — hallucinated agent surface.** `projectClient.GetAIAgentAsync("PortfolioAnalysisAgent", …)` is not a member of `AIProjectClient`; the real extension lives in the unreferenced `Microsoft.Agents.AI.AzureAI` package (and takes an id, targets `Azure.AI.Projects 2.x`). Plus the explicit `AgentRunResponse` type likely mismatches the documented `AgentResponse`. → `CS1061` + type error. *(STK-01, STK-02)*
2. **CopilotKit sidecar won't boot — nonexistent export.** `import { AzureOpenAIAdapter } from '@copilotkit/runtime'` resolves to `undefined`; `new AzureOpenAIAdapter(...)` throws `TypeError: AzureOpenAIAdapter is not a constructor`. Azure OpenAI must go through `OpenAIAdapter` + an `AzureOpenAI` client. *(STK-03)*
3. **Frontend build fails — missing Tailwind plugin.** `tailwind.config.js` does `require('tailwindcss-animate')`, but no install step or `package.json` provides it → `Cannot find module 'tailwindcss-animate'`, PostCSS/Tailwind aborts, page renders unstyled. *(STK-07)*

---
---

# Adversarial Stack-Fit Review — Package 02: Domain Model + NotchLadder (deterministic core)

- **Reviewer role:** Fin Adversary Stack Critic (technology-fit / C# correctness red team)
- **Date:** 2026-07-06
- **Scope note:** This section reviews the **package-02 deterministic core** (pure C#, no Azure by design) and is distinct from the FinCopilotKit template review above.
- **Targets:** `backend/FinancialServices.Api/Analysis/NotchLadder.cs` · `backend/FinancialServices.Api/Models/PrismModels.cs` · `backend/FinancialServices.Tests/Analysis/NotchLadderTests.cs` · `FinancialServices.Api.csproj` · `FinancialServices.Tests.csproj`
- **Method:** Rating-scale correctness attacked against standard S&P/Fitch/Moody's/DBRS equivalence; FluentAssertions license boundary verified against fluentassertions.com/releases.
- **Verdict headline:** The notch math is **domain-correct** — no alias corrupts the gap. Findings are robustness / idiom / tooling hardening, plus one **latent licensing trap**.

**Severity counts:** 🔴 Critical **0** · 🟠 High **1** · 🟡 Medium **6** · ⚪ Low **8**

## Verification Log (Pkg 02)
| Claim under test | Source | Verdict |
|---|---|---|
| Canonical AAA=1 … C=21 (no `D`) | Standard S&P/Fitch long-term scale | ✅ Correct |
| IG floor = BBB- = notch 10 (`<=10` IG) | IG = BBB-/Baa3 and above | ✅ Correct |
| DBRS high/mid/low = family−1 / family / family+1 | S&P equivalence (AA (high)↔AA+) | ✅ Offsets sane; `(mid)` not real DBRS (STK-16) |
| Moody's Aaa/Aa1…Ca/C rung-for-rung | Aaa=1…Baa3=10…Ca=20,C=21 | ✅ Correct (bottom is approx, STK-17) |
| Alias overlaps "AAA","C" | value-consistent (1, 21), idempotent indexer | ✅ No corruption |
| `ToUpperInvariant` + `StringComparer.Ordinal` | tr-TR safe for "high"/"mid" | ✅ Correct |
| FluentAssertions 6 vs 7/8 licensing | https://fluentassertions.com/releases/ | ✅ **v8.0.0 = commercial; v7.x + 6.12.x = free** — pin safe, upgrade trap (STK-27) |
| net9.0 on .NET 10 SDK | SDK backward-compat / on-demand targeting pack | ✅ Fine |

## Findings (Pkg 02)

- **[STK-16 · Medium · domain]** `NotchLadder.cs` L86-88 — DBRS `(mid)` is fabricated; real DBRS middle grade is the **bare label** ("A", not "A (mid)"). Math consistent (=canonical `A`=6) so no gap corruption, but it mints non-authentic notation a reconciliation tool should never emit. *Fix:* display bare family label; keep "(mid)" only as an accepted input alias.
- **[STK-17 · Low · domain]** `NotchLadder.cs` L20-24, L101 — Moody's `C` ≈ S&P `D` (not S&P `C`); ladder omits `D`/`SD`/`RD`. Monotonic → fine for relative gaps, but absolute bottom labels aren't strictly equivalent and a defaulted issuer throws. *Fix:* document bottom-anchor; map `D/SD/RD`→21 in connectors.
- **[STK-18 · Low→Med at pkg04 · domain]** `NotchLadder.cs` L34-37 — `NR`/`WR` (not-rated / withdrawn) throw; these are routine in real feeds. *Fix:* treat as unrated sentinel upstream.
- **[STK-19 · Medium · C#]** `NotchLadder.cs` L53-54 — `ToNotch(null)` → `NullReferenceException` (no null guard in `Normalize`); provider labels are an external boundary. *Fix:* `ArgumentException.ThrowIfNullOrWhiteSpace(label)`.
- **[STK-20 · Medium · C# idiom]** `NotchLadder.cs` L37 — unrecognized label throws `ArgumentOutOfRangeException` (wrong type; value not out of a range). *Fix:* `ArgumentException` / `UnknownRatingException`.
- **[STK-21 · Medium · fail-loud]** `NotchLadder.cs` L40 — `ToLabel` silently `Math.Clamp`s out-of-range notch → masks upstream gap-math bugs in an auditable core (violates "no silent fallbacks"); test L118-126 locks the masking in. *Fix:* throw for `notch<1||notch>21`; assert the throw.
- **[STK-22 · Low · latent]** `NotchLadder.cs` L74-106 — `BuildMap` uses indexer assignment; a future conflicting alias silently overwrites. *Fix:* `map.Add(...)` + special-case the two value-consistent overlaps.
- **[STK-23 · Low · layering]** `NotchLadder.cs` L6-11 — `Provider` (core domain type) lives in an Analysis utility file. *Fix:* move to `Models/`.
- **[STK-24 · Medium · C#]** `PrismModels.cs` (~L20/L34/L39/L47/L50-58) — positional records with `IReadOnlyList<T>` get **reference** equality → identical-content dossiers compare unequal; bites dedup/caching/`.Should().Be()`. *Fix:* document + use `.Should().BeEquivalentTo()`, or a value-equal collection type.
- **[STK-25 · Low · cross-language]** `PrismModels.cs` `decimal` fields — verify Cosmos (default = Newtonsoft, cf. V10 above) + Node sidecar (`number`) round-trip; the "money moment" attribution must not drift to float noise. *Fix:* fixed-decimal formatting at the API boundary; STJ serializer on Cosmos.
- **[STK-26 · Low · tooling]** `FinancialServices.Tests.csproj` L3-9 — no `<TreatWarningsAsErrors>`/`<LangVersion>` (only the API project has them); xUnit analyzer + nullable warnings in the core's tests won't fail the build. *Fix:* hoist into `Directory.Build.props`.
- **[STK-27 · High · latent licensing]** `FinancialServices.Tests.csproj` L15 — `FluentAssertions 6.12.1` is Apache-2.0 **free and the correct pin**. **Verified:** v8.0.0 moved to a **commercial** license (free only OSS/non-commercial; v8.1+ soft-warns), **v7.x stays free**. Trap: a blanket NuGet update / version-less `dotnet add package` jumps to v8 → silent licensing liability for a financial-services product. *Fix:* `Version="[6.12.1,7.0.0)"` (or central packaging) + comment. (For this public hackathon repo, even v8 is free; risk is only if commercialized.)
- **[STK-28 · Low · tooling]** `FinancialServices.Tests.csproj` L12 — `Microsoft.NET.Test.Sdk 17.11.1` predates .NET 9 alignment (17.12.0); xunit 2.9.2 / runner 2.8.2 minor skew — all build/run fine, no floating versions. *Fix:* bump test SDK when convenient.
- **[STK-29 · Medium · test rigor]** `NotchLadderTests.cs` L67-83 — `Moody_Aliases_Map_CaseInsensitive` uses **stored casing** on every row, so it does **not** prove case-insensitivity (it'd pass even if `Normalize` skipped case-folding). Case-folding is genuinely covered for DBRS in `Normalize_Is_Trim_...`. *Fix:* add a differing-case row, e.g. `[InlineData("BAA1", 8)]`.
- **[STK-30 · Low · test rigor]** `NotchLadderTests.cs` — no negative tests that out-of-family aliases ("AAA (high)","CC (low)","Aa4") or `null` throw. *Fix:* add them.

## Domain verdict (Pkg 02)
1–21 ladder, IG floor, DBRS offsets, and Moody's cross-walk are all correct rung-for-rung; no mis-mapping poisons the notch-gap waterfall. **GO** on technical correctness, conditional on the Medium items (STK-19 null guard, STK-18 NR/WR, STK-21 clamp-masking, STK-24 record equality, STK-29 test) before pkg 04 (real data) / pkg 08 (persistence), plus the STK-27 version ceiling.

---
---

# Adversarial Stack-Fit Review — Package 09: Frontend Shell

- **Reviewer role:** Fin Adversary Stack Critic (adversarial technology-fit red team)
- **Date:** 2026-07-06
- **Targets:** `frontend/package.json` · `vite.config.ts` · `vitest.config.ts` · `tsconfig.json`/`tsconfig.node.json` · `tailwind.config.js` · `postcss.config.js` · `components.json` · `src/components/ui/*` (hand-authored shadcn primitives) · `src/App.tsx` · `src/lib/{apiClient,utils,queryClient,settings}.ts` · `src/hooks/*` · `src/types/prism.ts`
- **Method:** Every SDK export/prop/version assumed hallucinated until confirmed via `frontend/package-lock.json` (resolved + integrity), the green strict `tsc --noEmit` + `vite build` (which validates imported exports and prop types against shipped `.d.ts`/dist), Radix docs, and grep of the whole `src` tree. Anything without a source is **Unverified**.
- **Verdict headline:** Unlike the kit/backend sections above, **the frontend stack is genuinely coherent and version-compatible** — CopilotKit `1.4.8` is a real dist-shipping GA whose React `^18||^19` peer range accepts `18.3.1`; strict TS is not bypassed. Findings are **runtime interop gaps types can't catch**, led by a missing root `TooltipProvider`.

**Severity counts:** 🔴 Critical **0** · 🟠 High **1** · 🟡 Medium **3** · ⚪ Low **4**

## Verification Log (Pkg 09)
| # | Claim under test | Source | Verdict |
|---|---|---|---|
| P1 | `@copilotkit/react-core@1.4.8` / `react-ui@1.4.8` are real, dist-shipping GA | `package-lock.json` L393-427 — resolved from registry, valid `integrity` sha512, `license:MIT`, full `1.4.8` sub-tree | ✅ **Real** (fixes broken 1.4.4/1.4.5) |
| P2 | CopilotKit accepts React 18.3.1 | lock L404-407/L427 peer `react:"^18 || ^19 || ^19.0.0-rc"` | ✅ **Satisfied**, no React-19-only API |
| P3 | `<CopilotKit runtimeUrl>`, `<CopilotSidebar labels>`, `@copilotkit/react-ui/styles.css` | green strict `tsc` + `vite build` resolve the exports/props/CSS subpath | ✅ **Verified by build** |
| P4 | TanStack Query v5 (`isPending`, object API) not v4 `isLoading` | `IssuersPage.tsx` L84; grep `isLoading` → **0 hits** | ✅ **v5 consistent** |
| P5 | Radix pins wired to real primitives (not stubbed) | `ui/*.tsx` import `@radix-ui/react-{dialog,label,select,slot,tabs,tooltip}` verbatim shadcn | ✅ **Real wiring** |
| P6 | `<Button asChild>` Slot usage | `button.tsx` L42 `Comp = asChild ? Slot`; `IssuersPage.tsx` L70-74 wraps a single `<Link>` | ✅ **Correct** |
| P7 | `tailwindcss-animate` registered | `tailwind.config.js` L83 `plugins:[require('tailwindcss-animate')]`; dep present | ✅ **Backed** (build green) |
| P8 | All CSS tokens used by primitives exist | `index.css` `:root`+`.dark` define border/input/ring/bg/fg/primary/secondary/destructive/muted/accent/popover/card | ✅ **Complete**; `index.html` `class="dark"` |
| P9 | TS strictness genuine | strict+noUnused*; grep `:any`/`as any`/`@ts-ignore`/`@ts-expect-error`/`as unknown as` → **1 hit, test-only** (`apiClient.test.ts` L15) | ✅ **Not bypassed** |
| P10 | `TooltipProvider` mounted for consumers | grep `TooltipProvider` → only `tooltip.tsx` L5,L27; **not in App.tsx/AppLayout** | ❌ **Never mounted** (STK-31) |
| P11 | Vite 5.4.9 + plugin-react 4.3.2 + Vitest 2.1.3 + jsdom 25 + TS 5.6.3 | mutually compatible; `vitest/config` owns test block | ✅ **No conflict** |

## Findings (Pkg 09)

- **[STK-31 · High · runtime]** `App.tsx` L21-46 + `ui/tooltip.tsx` L5,L27 — `tooltip.tsx` exports `TooltipProvider` (= `TooltipPrimitive.Provider`) but the shell **never mounts it** (grep: only in `tooltip.tsx`). shadcn's canonical pattern wraps the app once; Radix `@radix-ui/react-tooltip@1.1.x` `Tooltip.Root` without a provider ancestor throws *"Tooltip must be used within TooltipProvider"* (at minimum loses global delay). The mode names `<TooltipProvider>` as a pkg-10 consumer → **first `<Tooltip>` pkg 10 renders crashes**; not caught by tsc/build. *Fix:* wrap once in `App.tsx` — `<TooltipProvider delayDuration={200}>` inside `<CopilotKit>`.
- **[STK-32 · Medium · dev-vs-prod]** `App.tsx` L23 + `vite.config.ts` L27-30 — `runtimeUrl="/copilotkit"` is a relative path that only works via the Vite dev proxy; a prod static build (pkg 11/ACA) has no proxy → copilot calls 404 unless the host reverse-proxies `/copilotkit`. *Fix:* `runtimeUrl={import.meta.env.VITE_COPILOT_URL ?? '/copilotkit'}` + prod ingress rule.
- **[STK-33 · Medium · contract drift]** `types/prism.ts` L6-11 — the typed wire contract explicitly mirrors `Models/PrismDtos.cs` *"authored in package 08 (not yet built)"*, with **no codegen** (OpenAPI/NSwag/Kiota). `apiGet<IssuerListItem[]>` compiles even if BE renames/omits fields (`coverage` already "provisional") → runtime `undefined`, zero type error. *Fix:* generate the client from `/openapi/v1.json` when pkg 08 lands, or add a BE-JSON↔TS contract test.
- **[STK-34 · Medium · enum casing]** `types/prism.ts` L26 — `Severity='high'|'medium'|'low'` is lowercase while the same file states C# enums serialize as member **names** and `Provider`/`Bucket`/`RedFlagCode` follow PascalCase. A default `JsonStringEnumConverter` emits `"High"` → won't match `'high'`; severity switch/lookup silently falls through. *Fix:* confirm BE naming policy or align the union to the serialized casing.
- **[STK-35 · Low · dormant bug]** `apiClient.ts` L55-58 — `{ headers:{…}, ...init }` spreads `...init` **after** `headers`, so any future caller passing `init.headers` overwrites the merged object and drops the `Content-Type` default (inner merge is dead code). Benign today (no caller passes headers). *Fix:* spread `...init` first, set `headers` last.
- **[STK-36 · Low · doc-vs-impl]** `package.json` (no `@microsoft/signalr`) + `vite.config.ts` L24 (`ws:true` anticipation) + `.github/copilot-instructions.md` ("real-time via SignalR hubs") — no SignalR client exists. If pkg-10 streaming is CopilotKit AG-UI (HTTP/SSE), SignalR is genuinely N/A and the org instruction is stale for Prism; if any feature targets a hub, the dep is missing. *Fix:* pick a transport explicitly.
- **[STK-37 · Low · cosmetic]** 1.39 MB CopilotKit chunk — root cause `react-ui` pulling `@headlessui/react@2.2.10`+`react-markdown@8`+`react-syntax-highlighter@15`+`remark-*` (lock L410-424). Vite defaults (esnext, tree-shaking, sourcemap off) are fine — a size warning, not a misconfig. *Fix (optional):* `React.lazy(CopilotSidebar)` or `manualChunks`.
- **[STK-38 · Low · informational]** `apiClient.test.ts` L15 `as unknown as Response` — the only strictness-escape in the tree, an acceptable mock-`Response` cast confined to test code. Flagged for transparency.

## Unverified (needs live confirmation)
- Radix `react-tooltip@1.1.3` **hard-throw** vs silent-degrade without a provider — docs show both provider-wrapped anatomy and a bare `Tooltip.Root` example; confirm at runtime in pkg 10 (STK-31 severity assumes throw/loss).
- Backend `Severity` serialized casing (STK-34) — depends on pkg-08 `JsonStringEnumConverter` config that does not exist yet.
- Prod hosting of `/copilotkit` (STK-32) — depends on pkg-11 ACA ingress.

## Top 3 Won't-Compile / Won't-Run Risks (Pkg 09)
1. **STK-31** — first `<Tooltip>` in pkg 10 throws (no root `TooltipProvider`). Runtime, not compile.
2. **STK-32** — copilot 404s in prod (relative `runtimeUrl`, no off-dev proxy).
3. **STK-34 / STK-33** — enum-casing / unbuilt-DTO drift: JSON-boundary mismatches that compile clean and fail silently.

## Stack verdict (Pkg 09)
**GO** on stack correctness — versions are real and mutually compatible (CopilotKit 1.4.8 GA + React 18.3.1 peer-satisfied), strict `tsc`+`vite`+`vitest` are green, and the hand-authored shadcn primitives match the real API surface — **conditional on adding a root `<TooltipProvider>` (STK-31) before pkg 10 consumes tooltips**, and resolving the `runtimeUrl` (STK-32) and enum/contract drift (STK-33/34) at the pkg-08/11 boundaries.
