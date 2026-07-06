# C# API Template (ASP.NET Core .NET 9 + Microsoft Agent Framework)

Copy-paste starter for the **service/API layer**. Pairs with the shadcn/TanStack/CopilotKit
frontend in `templates/frontend-design-system/`.

## What's here

```
csharp-api/
├── FinancialServices.Api/
│   ├── Program.cs                      # Host builder, DI, CORS, OpenAPI, health
│   ├── Controllers/                    # HealthController, PortfoliosController
│   ├── Agents/                         # PortfolioAnalysisAgent (Microsoft Agent Framework)
│   ├── Models/                         # DTO / domain records
│   ├── Services/                       # PortfolioService (reads real Cosmos DB)
│   ├── Infrastructure/                 # AzureOptions + DI extensions
│   ├── appsettings.json
│   └── FinancialServices.Api.csproj    # net9.0, Nullable, TreatWarningsAsErrors
├── copilot-runtime/                    # CopilotKit Node sidecar (server.ts, package.json)
├── .gitignore
└── .env.example
```

## Run it

```powershell
# 1. Backend (port 8000)
cd FinancialServices.Api
dotnet restore
dotnet run --urls http://0.0.0.0:8000

# 2. CopilotKit sidecar (port 4000) — in a second terminal
cd copilot-runtime
npm install
npm run dev
```

## Conventions

- **Real Azure services only.** Every value in `.env` / `appsettings.json` must point at a real
  resource. `ValidateOnStart()` fails fast if a required `Azure__*` setting is missing — there
  are no mock/fallback code paths.
- **Auth:** Microsoft Entra ID JWT bearer with **deny-by-default** authorization. Set the `AzureAd`
  section in `appsettings.json` to your API app registration. Enforce **object-level authorization**
  (scope every read/mutation to the caller's advisorId/clientId) in services — never trust ids from
  routes, bodies, or LLM tool calls. `DefaultAzureCredential` is used for Azure resource access
  (managed identity in prod, `az login` locally).
- **Copilot:** the browser talks to the CopilotKit Node sidecar (`/copilotkit`), which forwards
  actions to `/api/v1/...` and streams Azure OpenAI completions. The sidecar is a client, not a
  trust boundary — it must forward the end-user token, and the API independently re-authorizes every
  call. The frontend never holds Azure creds.
- **Agents** are defined in Azure AI Foundry and referenced by id/name via the Microsoft Agent
  Framework `.NET` SDK. Confirm the exact `Microsoft.Agents.AI[.AzureAI]` API surface and package
  versions against what you install (the samples pin floating versions and note this).
- **Resilience:** add timeouts + bounded retries + circuit breakers (Polly /
  `Microsoft.Extensions.Http.Resilience`) to outbound Azure calls. "No fake fallbacks" forbids canned
  data, not resilience.

## Add the test project

```powershell
dotnet new xunit -n FinancialServices.Tests
dotnet add FinancialServices.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add FinancialServices.Tests package FluentAssertions
dotnet add FinancialServices.Tests package NSubstitute
```

Integration tests use `WebApplicationFactory<Program>` (enabled by `public partial class Program`).
