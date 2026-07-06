---
description: "C# / ASP.NET Core (.NET 9) backend coding standards for financial services applications on Azure"
applyTo: "**/*.cs"
---

# C# Backend Standards — Financial Services

The service/API layer is **C# on ASP.NET Core (.NET 9)** using controller-based Web API.
AI agents are built with the **Microsoft Agent Framework (.NET)** over **Azure AI Foundry**.

## Project Structure

Every backend follows this solution layout:

```
backend/
├── FinancialServices.Api/
│   ├── Program.cs                    # Host builder, DI, middleware, CORS, OpenAPI
│   ├── Controllers/                  # ApiController-derived controllers — one per domain
│   │   └── {Domain}Controller.cs
│   ├── Agents/                       # Microsoft Agent Framework agents (one file per agent)
│   │   └── {Name}Agent.cs
│   ├── Models/                       # DTOs (records) + domain models — no business logic
│   │   └── {Domain}Models.cs
│   ├── Services/                     # Business logic, orchestration calls
│   │   └── {Domain}Service.cs
│   ├── Orchestration/                # Multi-agent workflow orchestration
│   │   └── {Workflow}Workflow.cs
│   ├── Infrastructure/
│   │   ├── AzureOptions.cs           # Strongly-typed options (IOptions<T>)
│   │   ├── CosmosClientFactory.cs    # Cosmos DB client registration
│   │   ├── SearchClientFactory.cs    # Azure AI Search client registration
│   │   └── Telemetry.cs              # OpenTelemetry + Azure Monitor setup
│   ├── Helpers/                      # Shared utilities
│   ├── appsettings.json              # Non-secret config
│   ├── appsettings.Development.json  # Local overrides (gitignored if it holds secrets)
│   └── FinancialServices.Api.csproj
├── FinancialServices.Tests/          # xUnit test project
│   └── FinancialServices.Tests.csproj
├── FinancialServices.sln
├── .gitignore
└── .env.example                      # Documents required environment variables
```

## Project File Conventions (`.csproj`)

Every API project enables strict compilation and nullable reference types:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Azure.Identity" Version="1.13.*" />
    <PackageReference Include="Microsoft.Agents.AI" Version="1.*" />
    <PackageReference Include="Azure.AI.Agents.Persistent" Version="1.*" />
    <PackageReference Include="Azure.AI.Projects" Version="1.*" />
    <PackageReference Include="Microsoft.Azure.Cosmos" Version="3.*" />
    <PackageReference Include="Azure.Search.Documents" Version="11.*" />
    <PackageReference Include="Azure.AI.ContentSafety" Version="1.*" />
    <PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.*" />
  </ItemGroup>
</Project>
```

Rules:

- `Nullable` and `TreatWarningsAsErrors` are always **on**. Do not suppress warnings globally.
- Never commit secrets. Use environment variables, .NET user-secrets (local), or Key Vault (prod).
- Every backend includes a `.gitignore` covering `bin/`, `obj/`, `.env`, and local secret files.

### Required `.gitignore` (backend)

```gitignore
# Build output
bin/
obj/
*.user

# Environment & secrets — never commit
.env
.env.*
!.env.example
appsettings.*.local.json

# Editor / OS
.vs/
.vscode/
.idea/
.DS_Store
Thumbs.db
```

## Host Builder (`Program.cs`)

```csharp
using FinancialServices.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Strongly-typed options — bound from configuration/environment, never read raw in business logic
builder.Services.AddOptions<AzureOptions>()
    .Bind(builder.Configuration.GetSection("Azure"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();               // OpenAPI document at /openapi/v1.json
builder.Services.AddProblemDetails();

// Azure clients (singletons) and domain services
builder.Services.AddAzureClients();          // CosmosClient, SearchClient, AIProjectClient factories
builder.Services.AddDomainServices();        // Scoped domain services + agents
builder.Services.AddAppTelemetry(builder.Configuration);

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                  ?? ["http://localhost:5173", "http://localhost:4000"];
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins(corsOrigins)                // Never AllowAnyOrigin in production
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();                    // ProblemDetails-based error responses
app.UseCors("frontend");
app.MapControllers();
app.MapHealthChecks("/api/health");

app.Run();

public partial class Program;                 // Enables WebApplicationFactory<Program> tests
```

## Configuration & Options (`Infrastructure/AzureOptions.cs`)

Never call `Environment.GetEnvironmentVariable` or read `IConfiguration` inside business logic.
Bind configuration once into `IOptions<T>` and inject it.

```csharp
using System.ComponentModel.DataAnnotations;

namespace FinancialServices.Api.Infrastructure;

public sealed class AzureOptions
{
    // Azure AI Foundry
    [Required] public required string AiProjectEndpoint { get; init; }
    [Required] public required string AiProjectName { get; init; }
    public string AgentModel { get; init; } = "gpt-4o";

    // Azure OpenAI (used by the CopilotKit runtime sidecar and grounding calls)
    [Required] public required string OpenAiEndpoint { get; init; }
    public string OpenAiApiVersion { get; init; } = "2024-12-01-preview";

    // Azure Cosmos DB
    [Required] public required string CosmosEndpoint { get; init; }
    [Required] public required string CosmosDatabase { get; init; }

    // Azure AI Search
    [Required] public required string SearchEndpoint { get; init; }
    [Required] public required string SearchIndex { get; init; }
}
```

If a required value is missing, `ValidateOnStart()` fails the app at boot with a clear message
naming the setting — **never** substitute a placeholder or fall back to fake values.

## DTOs and Domain Models (`Models/`)

Use `record` types with `System.Text.Json` and validation attributes. Keep request/response
DTOs separate from domain models.

```csharp
using System.ComponentModel.DataAnnotations;

namespace FinancialServices.Api.Models;

public enum AssetClass { Equity, FixedIncome, Derivative, Alternative }

public sealed record PortfolioPosition(
    [property: Required] string InstrumentId,          // ISIN or internal instrument ID
    AssetClass AssetClass,
    [property: Range(0, double.MaxValue)] decimal Quantity,
    decimal MarketValue,
    [property: Range(0, 1)] decimal Weight,
    DateTimeOffset LastUpdated);

public sealed record PortfolioSummary(
    string PortfolioId,
    string ClientId,
    decimal TotalValue,
    IReadOnlyList<PortfolioPosition> Positions,
    double? RiskScore,
    string? Benchmark);

// Request/response models always separate from domain models
public sealed record RebalanceRequest(
    IReadOnlyDictionary<string, decimal> TargetWeights,
    [property: MaxLength(500)] string Reason,
    string? ApprovedBy);
```

Use `decimal` (never `double` or `float`) for money, prices, weights, and rates.

## Controller Pattern (`Controllers/`)

```csharp
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinancialServices.Api.Controllers;

[ApiController]
[Route("api/v1/portfolios")]
public sealed class PortfoliosController(IPortfolioService service) : ControllerBase
{
    [HttpGet("{portfolioId}")]
    [ProducesResponseType<PortfolioSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortfolioSummary>> GetPortfolio(
        string portfolioId, CancellationToken ct)
    {
        var portfolio = await service.GetPortfolioAsync(portfolioId, ct);
        return portfolio is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "PORTFOLIO_NOT_FOUND",
                detail: $"Portfolio {portfolioId} not found")
            : Ok(portfolio);
    }

    [HttpPost("{portfolioId}/rebalance")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Rebalance(
        string portfolioId, [FromBody] RebalanceRequest request, CancellationToken ct)
    {
        var jobId = await service.SubmitRebalanceAsync(portfolioId, request, ct);
        return Accepted(new { jobId, status = "accepted" });
    }
}
```

- Model binding + validation attributes reject malformed input automatically (`[ApiController]`).
- Always accept and propagate a `CancellationToken`.
- Return errors as RFC 7807 `ProblemDetails` via `Problem(...)`.

## Microsoft Agent Framework Agent Pattern (`Agents/`)

Agents are defined against Azure AI Foundry and invoked through the Microsoft Agent Framework
`.NET` SDK. Reference agents by name; never recreate them on every request.

```csharp
using System.Runtime.CompilerServices;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using FinancialServices.Api.Infrastructure;

namespace FinancialServices.Api.Agents;

public sealed class PortfolioAnalysisAgent(
    IOptions<AzureOptions> options,
    ILogger<PortfolioAnalysisAgent> logger)
{
    private static readonly ActivitySource Activity = new("FinancialServices.Agents");
    private readonly AzureOptions _options = options.Value;

    public async Task<string> AnalyzeAsync(
        string portfolioSummary, string marketContext, CancellationToken ct)
    {
        using var span = Activity.StartActivity("PortfolioAnalysisAgent.Analyze");

        var projectClient = new AIProjectClient(
            new Uri(_options.AiProjectEndpoint), new DefaultAzureCredential());

        AIAgent agent = await projectClient
            .GetAIAgentAsync("PortfolioAnalysisAgent", cancellationToken: ct);

        AgentRunResponse response = await agent.RunAsync(
            $"Analyze portfolio:\n{portfolioSummary}\n\nMarket context:\n{marketContext}",
            cancellationToken: ct);

        return response.Text;
    }
}
```

### Agent Naming Convention

`{Domain}{Function}Agent` — e.g. `PortfolioAnalysisAgent`, `MarketIntelligenceAgent`,
`RiskAdvisoryAgent`, `RebalanceAgent`, `ComplianceAgent`, `KycCheckAgent`, `ClaimsTriageAgent`.

## Multi-Agent Orchestration (`Orchestration/`)

Run independent agents concurrently with `Task.WhenAll`; use the Microsoft Agent Framework
workflow primitives for sequential pipelines and human-in-the-loop gates.

```csharp
public async Task<PreMeetingBriefing> RunPreMeetingPrepAsync(
    string clientId, string meetingId, CancellationToken ct)
{
    var newsTask = _marketIntelligenceAgent.SummarizeAsync(clientId, ct);
    var advisoryTask = _advisoryAgent.AnalyzeAsync(clientId, ct);

    await Task.WhenAll(newsTask, advisoryTask);

    return new PreMeetingBriefing(
        NewsSummary: newsTask.Result,
        AdvisoryInsights: advisoryTask.Result,
        ClientId: clientId,
        MeetingId: meetingId);
}
```

## Cosmos DB Pattern (`Infrastructure/CosmosClientFactory.cs`)

Register `CosmosClient` as a singleton with `DefaultAzureCredential`. Partition keys must align
with access patterns.

```csharp
public static IServiceCollection AddAzureClients(this IServiceCollection services)
{
    services.AddSingleton(sp =>
    {
        var opts = sp.GetRequiredService<IOptions<AzureOptions>>().Value;
        return new CosmosClient(opts.CosmosEndpoint, new DefaultAzureCredential(),
            new CosmosClientOptions { SerializerOptions = new() { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase } });
    });
    return services;
}

// Usage inside a service
public async Task<SessionDocument> UpsertSessionAsync(
    SessionDocument session, CancellationToken ct)
{
    var container = _cosmos.GetContainer(_options.CosmosDatabase, "sessions");
    var response = await container.UpsertItemAsync(
        session, new PartitionKey(session.ClientId), cancellationToken: ct);
    return response.Resource;
}
```

## Real-Time Features — SignalR

Prefer SignalR hubs for streaming/real-time features (transcription, live workflow status).
Use raw WebSockets only when a non-SignalR client protocol is required.

```csharp
public sealed class TranscriptionHub(ITranscriptionService service) : Hub
{
    public async Task PushAudioChunk(string sessionId, byte[] chunk)
    {
        var result = await service.ProcessChunkAsync(sessionId, chunk, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("transcriptChunk", new
        {
            sessionId, text = result.Text, isFinal = result.IsFinal,
            speaker = result.Speaker, timestamp = result.Timestamp
        });
    }
}
// Program.cs: builder.Services.AddSignalR(); app.MapHub<TranscriptionHub>("/hubs/transcription");
```

## Error Handling

- Use `ProblemDetails` for all error responses: `{ "error": { "code": "...", "message": "..." } }`
  shape via a consistent problem factory, or the built-in `Problem(...)`.
- Never expose stack traces or inner exception detail in responses.
- Log unexpected exceptions with `logger.LogError(ex, ...)`; log expected business errors with
  `logger.LogWarning(...)`.
- Never catch and swallow exceptions silently. No empty `catch { }` blocks.

## Authentication & Authorization (never optional for financial endpoints)

Financial endpoints are **never anonymous**. Authenticate with Microsoft Entra ID and default to
deny.

```csharp
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    // Deny-by-default: every endpoint requires an authenticated caller unless it
    // explicitly opts out with [AllowAnonymous] (e.g. health checks).
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Program.cs pipeline: app.UseAuthentication(); app.UseAuthorization();
```

- **Object-level authorization is mandatory.** Every read/mutation must be scoped to the caller's
  `advisorId`/`clientId` — verify ownership in the service, do not trust ids from the route,
  request body, or an LLM tool call. This prevents broken-object-level-authorization (BOLA).
- **The CopilotKit sidecar is a client, not a trust boundary.** The API must independently
  authenticate and authorize every call the sidecar forwards; treat all LLM-supplied arguments as
  hostile input and re-authorize them. Never rely on the sidecar to enforce access control.
- Grant the API's managed identity **least privilege** (data-plane roles only), never broad
  subscription Contributor.

## Resilience (fail loudly ≠ fail fragile)

The "no fake fallbacks" rule forbids returning canned/sample data — it does **not** forbid
resilience. Transient Azure failures (HTTP 429/503, timeouts) must not become hard outages.

- Apply timeouts, bounded retries with jittered backoff, and circuit breakers to outbound Azure
  calls (e.g. `Microsoft.Extensions.Http.Resilience` / `Polly`).
- Distinguish transient (retryable) from terminal errors; surface terminal errors loudly.
- Never retry non-idempotent financial mutations without an idempotency key.

## Human-in-the-Loop Gates & Audit (enforce server-side)

- HITL approval gates for consequential actions (trade/rebalance submission, recommendation
  delivery, large payouts) must be **enforced on the server** as a state transition — never only in
  the UI. Never trust a client-supplied `approvedBy`; derive the approver from the authenticated
  identity.
- Financial mutations and their **immutable audit record** must be written atomically. If the audit
  write fails, the mutation fails (fail closed). Audit persistence must not depend on optional
  telemetry (e.g. an App Insights connection string).

## PII and Security

- Run Azure AI Content Safety on all user-provided text before passing it to agents.
- Redact PII from all logs — names, account numbers, SSN/Tax IDs, dates of birth.
- Rely on model binding + validation attributes for input validation at the controller boundary.
- Retrieve secrets from Key Vault (`Azure.Security.KeyVault.Secrets`) in production; never hardcode.
- Use `System.Text.Json` source generation where hot paths matter; never deserialize into `dynamic`.

## Logging & OpenTelemetry (`Infrastructure/Telemetry.cs`)

```csharp
public static IServiceCollection AddAppTelemetry(
    this IServiceCollection services, IConfiguration config)
{
    services.AddOpenTelemetry()
        .UseAzureMonitor()                       // APPLICATIONINSIGHTS_CONNECTION_STRING
        .WithTracing(t => t
            .AddSource("FinancialServices.Agents")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());
    return services;
}
```

Use `ILogger<T>` with structured message templates and scopes — never string interpolation
that embeds PII:

```csharp
using (logger.BeginScope(new Dictionary<string, object> { ["PortfolioId"] = portfolioId }))
{
    logger.LogInformation("Rebalance submitted with {TradeCount} trades", tradeCount);
}
```

## Testing

- Unit tests: `xUnit` + `FluentAssertions`; mock dependencies with `NSubstitute` or `Moq`.
- Integration tests: `WebApplicationFactory<Program>` from `Microsoft.AspNetCore.Mvc.Testing`.
- Mocks/fakes belong **only** in test projects — never in the API runtime paths.
- Minimum 80% coverage on services and agents.
