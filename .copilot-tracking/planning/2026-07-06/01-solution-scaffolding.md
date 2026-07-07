# Plan 01 — Solution Scaffolding & Configuration

**Objective:** Stand up the C# solution, corrected package versions, DI/config, run scripts, and a
vanilla AG-UI skeleton that streams locally. This is the Day-0 half-day so deployment/build risk dies
early.

**Depends on:** Plan 00 (prerequisites, env vars). **Primary day:** 0.

**Reference templates:** `templates/csharp-api/` (copy + correct per the stack review). Do **not** copy
the abandoned `backend/app/` Python tree.

---

## 1. Create the solution and project

- [ ] Create solution `backend/FinancialServices.sln`
- [ ] Create web API project `backend/FinancialServices.Api/` (`Microsoft.NET.Sdk.Web`, `net9.0`,
      `Nullable=enable`, `TreatWarningsAsErrors=true`, `ImplicitUsings=enable`,
      `RootNamespace=FinancialServices.Api`)
- [ ] Create test project `backend/FinancialServices.Tests/` (xUnit) and add
      `Microsoft.AspNetCore.Mvc.Testing`, `FluentAssertions`, `NSubstitute`
- [ ] Add both projects to the solution

## 2. Package references (corrected — ⚠ STK-01/02/04/05, ARC-16)

Author `backend/FinancialServices.Api/FinancialServices.Api.csproj` with **pinned** versions
(no wildcards). Verify each against NuGet at install time and pin the exact resolved version.

- [ ] Identity / auth: `Azure.Identity`, `Microsoft.Identity.Web`
- [ ] Agent Framework + Foundry (⚠ STK-01): add **`Microsoft.Agents.AI.AzureAI`** (provides the
      `GetAIAgent(Async)` extension) alongside `Microsoft.Agents.AI`; bump **`Azure.AI.Projects` to 2.x**
      (the V2 integration targets Projects 2.x — pinning 1.0.0 makes the fix impossible)
- [ ] AG-UI hosting (⚠ prerelease — pin exact): `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`,
      `Microsoft.Agents.AI.Foundry` — pin day 0, never upgrade mid-build
- [ ] Data + search: **`Microsoft.Azure.Cosmos` ≥ 3.58.0** (needed for STJ serializer — ⚠ STK-04),
      `Azure.Search.Documents`
- [ ] Safety + telemetry: `Azure.AI.ContentSafety`, `Azure.Monitor.OpenTelemetry.AspNetCore`
- [ ] Resilience (⚠ ARC-11): `Microsoft.Extensions.Http.Resilience`
- [ ] API docs: `Microsoft.AspNetCore.OpenApi`
- [ ] Consider `Directory.Packages.props` (central package management) to lock versions once

## 3. Strongly-typed options

Author `backend/FinancialServices.Api/Infrastructure/AzureOptions.cs` extending the template with
Rewind-specific settings (⚠ STK-20 option drift — declare every property the code reads).

- [ ] Foundry: `AiProjectEndpoint` (required), `AiProjectName` (required), `AgentModel`
- [ ] Azure OpenAI: `OpenAiEndpoint` (required), `OpenAiApiVersion`, `OpenAiDeployment`
- [ ] Cosmos: `CosmosEndpoint` (required), `CosmosDatabase` (required)
- [ ] Search: `SearchEndpoint` (required), `SearchIndex` (required)
- [ ] Content Safety: `ContentSafetyEndpoint` — **required in non-Development** (⚠ ARC-05)
- [ ] Agent names: `OrchestratorAgentName`, `VaultForensicsAgentName`, `RedFlagAgentName`
- [ ] Market data: `FredApiKey` (bind from `FRED_API_KEY`), `FredBaseUrl`, `TreasuryBaseUrl`
- [ ] Keep `[Required]` data annotations; `ValidateDataAnnotations().ValidateOnStart()`

## 4. DI wiring — singleton Azure clients (⚠ ARC-13)

Author `backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs`.

- [ ] Register **one shared `DefaultAzureCredential`** singleton and reuse it everywhere
- [ ] `CosmosClient` singleton — set `UseSystemTextJsonSerializerWithOptions(...)` with camelCase +
      `JsonStringEnumConverter` (⚠ STK-04: default serializer is Newtonsoft; align DTO attributes to STJ)
- [ ] `AIProjectClient` singleton (endpoint + shared credential)
- [ ] `SearchClient` / `SearchIndexClient` singleton
- [ ] `ContentSafetyClient` singleton
- [ ] Typed `HttpClient` for FRED/Treasury with `AddStandardResilienceHandler()` (timeout +
      retry-with-jitter + circuit breaker — ⚠ ARC-11)
- [ ] `AddDomainServices()` registers services + agents (scoped) — filled in by later plans
- [ ] `AddAppTelemetry()` — in non-Development, **require** `APPLICATIONINSIGHTS_CONNECTION_STRING`
      and fail startup if missing (⚠ ARC-14); tracing sources include `Rewind.Agents`, `Rewind.Analysis`

## 5. Host configuration — `Program.cs`

Start from the template `Program.cs` and add:

- [ ] Entra ID JWT bearer auth + **deny-by-default** `FallbackPolicy` (already in template — keep)
- [ ] `app.UseHttpsRedirection()` + HSTS in non-dev (⚠ SEC-09)
- [ ] CORS to specific origins only; **remove `http://localhost:4000`** from the list — the sidecar is
      a server-side caller, not a browser origin (⚠ ARC-15)
- [ ] `AddProblemDetails()` + `UseExceptionHandler()` (RFC 7807 error shape)
- [ ] `AddAzureClients()`, `AddDomainServices()`, `AddAppTelemetry()`
- [ ] Health check at `GET /api/health` (`AllowAnonymous`)
- [ ] OpenAPI at `/openapi/v1.json` via `AddOpenApi()` / `MapOpenApi()` (⚠ STK-12: no Swagger UI in .NET 9)
- [ ] `public partial class Program;` (enables `WebApplicationFactory<Program>`)
- [ ] AG-UI endpoints (`AddAGUI()` / `MapAGUI(...)`) are added in Plan 04 — leave a marked TODO

## 6. Config files

- [ ] `backend/FinancialServices.Api/appsettings.json` — non-secret shape (empty `Azure` values, CORS
      origins `http://localhost:5173` only, `AzureAd` placeholders)
- [ ] `backend/FinancialServices.Api/appsettings.Development.json` — local overrides
- [ ] `.env.example` at repo root and `backend/` documenting every variable from Plan 00 §6
- [ ] `.gitignore` covers `.env`, `appsettings.*.local.json`, `bin/`, `obj/`
- [ ] Prefer **user-secrets** (`dotnet user-secrets`) for local secrets over `.env`

## 7. Root run scripts (required by copilot-instructions)

- [ ] `run-backend.bat` — `dotnet restore` then `dotnet run --urls http://0.0.0.0:8000` in the API project
- [ ] `run-copilot-runtime.bat` — `npm install` (if needed) + start sidecar on port 4000 (Plan 07)
- [ ] `run-frontend.bat` — `npm install` (if needed) + `npm run dev` in `frontend/` on 5173 (Plan 08)

## 8. Housekeeping

- [ ] Delete the stale `backend/app/__pycache__` tree and empty Python scaffold dirs (they are the
      abandoned prototype — confirm nothing references them)
- [ ] Add a minimal smoke test in `FinancialServices.Tests` hitting `GET /api/health` via
      `WebApplicationFactory<Program>`

## Acceptance criteria

- `dotnet build` succeeds with **zero warnings** (warnings are errors)
- `run-backend.bat` starts; `GET http://localhost:8000/api/health` returns healthy
- `GET http://localhost:8000/openapi/v1.json` serves the document
- Missing a required `AZURE__*` value fails startup with a clear message (no silent fallback)
- No `Microsoft.Agents.AI.AzureAI` / Projects version conflict; the corrected agent call compiles

## Cut-lines

- None — this is foundational. If time-boxed, defer AG-UI wiring (Plan 04) but keep health + OpenAPI.
