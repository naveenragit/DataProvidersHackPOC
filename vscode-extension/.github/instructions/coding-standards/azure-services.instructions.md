---
description: "Azure Data and AI Services integration standards for financial applications"
applyTo: "**/*.cs, **/*.ts, **/*.tsx"
---

# Azure Services Integration Standards — Financial Services

The service layer is **C# / ASP.NET Core (.NET 9)**. Use the official Azure `.NET` SDKs with
`DefaultAzureCredential`. AI agents use the **Microsoft Agent Framework (.NET)** over
**Azure AI Foundry**.

## Real Services Only — No Mocks or Fallbacks

The value is in **real, plugged-in Azure services**. Application code must call live Azure
resources — never simulate them.

- No mock data, stub clients, or fake responses in runtime code. Mocks/fakes are allowed
  **only in automated test code** (`xUnit`, `vitest`).
- No silent fallbacks to canned/sample data when a service is unconfigured or unreachable.
  Raise a clear error that names the missing setting or the failing service.
- All Azure configuration comes from bound `IOptions<T>` (environment variables / Key Vault).
  Missing required settings must fail loudly at startup (`ValidateOnStart`) — never substitute
  placeholder or fake values.

## Authentication

Always use `DefaultAzureCredential` for production deployments. It works with:
- Managed Identity (Azure Container Apps, App Service, AKS)
- Azure CLI (`az login`) for local development
- Service Principal via environment variables as a fallback

```csharp
using Azure.Identity;

// One credential instance, reused across clients
var credential = new DefaultAzureCredential();

// For local dev fallback only — never commit service principal secrets.
// Set AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID in the environment.
```

```typescript
// TypeScript — credentials are always handled server-side.
// The frontend never holds Azure credentials.
```

**Required environment variables (`.env.example` / App Settings):**
```
# Azure AI Foundry
AZURE__AiProjectEndpoint=https://<hub>.services.ai.azure.com/api/projects/<project>
AZURE__AiProjectName=

# Azure OpenAI (used by the CopilotKit Node runtime sidecar)
AZURE__OpenAiEndpoint=https://<name>.openai.azure.com/
AZURE__OpenAiApiVersion=2024-12-01-preview

# Azure Cosmos DB
AZURE__CosmosEndpoint=https://<name>.documents.azure.com:443/
AZURE__CosmosDatabase=<database-name>

# Azure AI Search
AZURE__SearchEndpoint=https://<name>.search.windows.net
AZURE__SearchIndex=<index-name>

# Azure Speech
AZURE__SpeechKey=
AZURE__SpeechRegion=eastus

# Azure Monitor
APPLICATIONINSIGHTS_CONNECTION_STRING=

# Identity (local dev / service principal only)
AZURE_CLIENT_ID=
AZURE_CLIENT_SECRET=
AZURE_TENANT_ID=
```

> The double-underscore (`AZURE__Name`) convention binds environment variables to the nested
> `AzureOptions` configuration section in ASP.NET Core.

---

## Azure AI Foundry — Microsoft Agent Framework (.NET)

Agents are authored in Azure AI Foundry and invoked through the Microsoft Agent Framework
`.NET` SDK. Define agents in Foundry; reference them by name in code — never recreate on every
request.

```csharp
// PackageReference: Microsoft.Agents.AI, Azure.AI.Projects, Azure.Identity
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;

public sealed class FoundryAgentClient(IOptions<AzureOptions> options)
{
    private readonly AzureOptions _options = options.Value;

    private AIProjectClient CreateProjectClient() =>
        new(new Uri(_options.AiProjectEndpoint), new DefaultAzureCredential());

    public async Task<string> RunAgentAsync(
        string agentName, string prompt, CancellationToken ct)
    {
        AIAgent agent = await CreateProjectClient()
            .GetAIAgentAsync(agentName, cancellationToken: ct);
        AgentRunResponse response = await agent.RunAsync(prompt, cancellationToken: ct);
        return response.Text;
    }
}
```

### Agent Naming Convention

`{Domain}{Function}Agent` — for example:
- `PortfolioAnalysisAgent` — analyzes portfolio composition and risk
- `MarketIntelligenceAgent` — fetches and synthesizes market news (with grounding)
- `RiskAdvisoryAgent` — evaluates and scores financial risk
- `RebalanceAgent` — generates rebalancing recommendations
- `ComplianceAgent` — checks regulatory compliance

### Grounding for Market Data

Attach grounding/tool connections defined in the Foundry project when the agent needs current
market data. Configure the connection in Foundry and reference it by connection id from options.

---

## Azure Cosmos DB

Register `CosmosClient` as a **singleton** with `DefaultAzureCredential`. Partition keys must
align with access patterns.

```csharp
// PackageReference: Microsoft.Azure.Cosmos
using Microsoft.Azure.Cosmos;

public async Task<SessionDocument> UpsertSessionAsync(
    SessionDocument session, CancellationToken ct)
{
    Container container = _cosmos.GetContainer(_options.CosmosDatabase, "sessions");
    ItemResponse<SessionDocument> response = await container.UpsertItemAsync(
        session,
        new PartitionKey(session.ClientId),   // Partition key
        cancellationToken: ct);
    return response.Resource;
}
```

### Container Design

| Container | Partition Key | Purpose |
|---|---|---|
| `clients` | `/advisorId` | Client profiles, KYC data |
| `sessions` | `/clientId` | Meeting/interaction sessions |
| `portfolios` | `/clientId` | Portfolio positions and history |
| `backtests` | `/clientId` | Backtesting results |
| `auditLog` | `/advisorId` | Immutable audit trail |
| `rebalanceReports` | `/clientId` | Rebalancing history |

Prefer point reads (`ReadItemAsync` with id + partition key) over queries. Use
`QueryDefinition` with parameters (never string concatenation) and page with
`FeedIterator` + `MaxItemCount`.

---

## Azure AI Search

Use **hybrid search** (vector + keyword) for all financial document retrieval.

```csharp
// PackageReference: Azure.Search.Documents
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

public async Task<IReadOnlyList<SearchDocument>> HybridSearchAsync(
    string query, int top, CancellationToken ct)
{
    var client = new SearchClient(
        new Uri(_options.SearchEndpoint), _options.SearchIndex, new DefaultAzureCredential());

    var searchOptions = new SearchOptions
    {
        Size = top,
        VectorSearch = new()
        {
            Queries = { new VectorizableTextQuery(query) { KNearestNeighborsCount = 50, Fields = { "contentVector" } } }
        },
    };
    searchOptions.Select.Add("id");
    searchOptions.Select.Add("title");
    searchOptions.Select.Add("content");
    searchOptions.Select.Add("instrumentId");
    searchOptions.Select.Add("documentDate");

    SearchResults<SearchDocument> results = await client.SearchAsync<SearchDocument>(query, searchOptions, ct);
    var docs = new List<SearchDocument>();
    await foreach (SearchResult<SearchDocument> r in results.GetResultsAsync())
        docs.Add(r.Document);
    return docs;
}
```

### Index Schema for Financial Documents

```json
{
  "fields": [
    { "name": "id", "type": "Edm.String", "key": true },
    { "name": "title", "type": "Edm.String", "searchable": true },
    { "name": "content", "type": "Edm.String", "searchable": true },
    { "name": "instrumentId", "type": "Edm.String", "filterable": true },
    { "name": "documentType", "type": "Edm.String", "filterable": true, "facetable": true },
    { "name": "documentDate", "type": "Edm.DateTimeOffset", "sortable": true, "filterable": true },
    { "name": "contentVector", "type": "Collection(Edm.Single)", "searchable": true, "vectorSearchDimensions": 1536 }
  ]
}
```

Build `$filter` clauses with `SearchFilter.Create($"...")` to avoid injection.

---

## Azure Speech Services

Use the **SDK** for real-time streaming and the REST API for batch transcription.

```csharp
// PackageReference: Microsoft.CognitiveServices.Speech
using Microsoft.CognitiveServices.Speech;

public SpeechConfig CreateSpeechConfig()
{
    var config = SpeechConfig.FromSubscription(_options.SpeechKey, _options.SpeechRegion);
    config.SpeechRecognitionLanguage = "en-US";
    config.RequestWordLevelTimestamps();
    // Financial meeting scenarios: extend end-of-sentence silence
    config.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "2000");
    return config;
}
```

Stream results to the frontend over a **SignalR hub** (`/hubs/transcription`).

---

## Azure AI Content Safety

Run content safety checks on ALL user-provided text before passing it to agents.

```csharp
// PackageReference: Azure.AI.ContentSafety
using Azure.AI.ContentSafety;

public async Task EnsureSafeAsync(string text, CancellationToken ct)
{
    var client = new ContentSafetyClient(
        new Uri(_options.ContentSafetyEndpoint), new DefaultAzureCredential());

    AnalyzeTextResult result = await client.AnalyzeTextAsync(new AnalyzeTextOptions(text), ct);

    // Financial platforms have low tolerance — flag severity >= 2
    var violation = result.CategoriesAnalysis.FirstOrDefault(c => c.Severity >= 2);
    if (violation is not null)
        throw new ContentSafetyException(
            $"Content safety violation: {violation.Category} severity {violation.Severity}");
}
```

---

## Azure Document Intelligence

For processing financial documents (annual reports, prospectuses, policy documents).

```csharp
// PackageReference: Azure.AI.DocumentIntelligence
using Azure.AI.DocumentIntelligence;

public async Task<AnalyzeResult> AnalyzeDocumentAsync(Uri documentUri, CancellationToken ct)
{
    var client = new DocumentIntelligenceClient(
        new Uri(_options.DocumentIntelligenceEndpoint), new DefaultAzureCredential());

    Operation<AnalyzeResult> op = await client.AnalyzeDocumentAsync(
        WaitUntil.Completed, "prebuilt-layout",
        new AnalyzeDocumentContent { UrlSource = documentUri }, cancellationToken: ct);
    return op.Value;
}
```

---

## OpenTelemetry + Azure Monitor

All agent calls, database operations, and external API calls must be instrumented.

```csharp
// PackageReference: Azure.Monitor.OpenTelemetry.AspNetCore
builder.Services.AddOpenTelemetry()
    .UseAzureMonitor()                         // APPLICATIONINSIGHTS_CONNECTION_STRING
    .WithTracing(t => t
        .AddSource("FinancialServices.Agents")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());

// Create spans in agent/service code (never put PII values into attributes)
private static readonly ActivitySource Activity = new("FinancialServices.Agents");

using var span = Activity.StartActivity("portfolio_analysis");
span?.SetTag("portfolio.id", portfolioId);
span?.SetTag("session.id", sessionId);
```

---

## Deployment

All services deploy to **Azure Container Apps** or **Azure App Service** with:
- Managed Identity enabled
- Application Insights connected
- Key Vault references for secrets
- Bicep/ARM templates in the `infra/` directory

```
infra/
├── main.bicep
├── modules/
│   ├── containerApp.bicep
│   ├── cosmosDb.bicep
│   ├── aiSearch.bicep
│   └── aiFoundry.bicep
└── parameters/
    ├── dev.bicepparam
    └── prod.bicepparam
```

The CopilotKit Node runtime sidecar deploys as a **separate container/app** alongside the C#
API and is granted only the Azure OpenAI access it needs.
