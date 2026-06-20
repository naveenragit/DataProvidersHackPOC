---
description: "Azure Data and AI Services integration standards for financial applications"
applyTo: "**/*.py, **/*.ts, **/*.tsx"
---

# Azure Services Integration Standards — Financial Services

## Real Services Only — No Mocks or Fallbacks

The value is in **real, plugged-in Azure services**. Application code must call live Azure
resources — never simulate them.

- No mock data, stub clients, or fake responses in runtime code. Mocks/fakes are allowed
  **only in automated test code** (`pytest`, `vitest`).
- No silent fallbacks to canned/sample data when a service is unconfigured or unreachable.
  Raise a clear error that names the missing `.env` variable or the failing service.
- All Azure configuration comes from a populated `.env`. Missing required settings must fail
  loudly — never substitute placeholder or fake values.

## Authentication

Always use `DefaultAzureCredential` for production deployments. This works with:
- Managed Identity (Azure Container Apps, App Service, AKS)
- Azure CLI (`az login`) for local development
- Service Principal via environment variables as fallback

```python
# Python
from azure.identity.aio import DefaultAzureCredential, ChainedTokenCredential, AzureCliCredential

credential = DefaultAzureCredential()

# For local dev fallback only — never commit service principal secrets
# Set AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID in .env
```

```typescript
// TypeScript — credentials are always handled server-side
// Frontend never holds Azure credentials
```

**Required environment variables (`.env.example`):**
```
# Azure AI Foundry
AZURE_AI_PROJECT_ENDPOINT=https://<hub>.services.ai.azure.com/api/projects/<project>
AZURE_AI_PROJECT_SUBSCRIPTION_ID=
AZURE_AI_PROJECT_RESOURCE_GROUP=
AZURE_AI_PROJECT_NAME=

# Azure OpenAI
AZURE_OPENAI_ENDPOINT=https://<name>.openai.azure.com/
AZURE_OPENAI_API_VERSION=2024-12-01-preview

# Azure Cosmos DB
COSMOS_ENDPOINT=https://<name>.documents.azure.com:443/
COSMOS_DATABASE=<database-name>

# Azure AI Search
AZURE_SEARCH_ENDPOINT=https://<name>.search.windows.net
AZURE_SEARCH_INDEX=<index-name>

# Azure Speech
AZURE_SPEECH_KEY=
AZURE_SPEECH_REGION=eastus

# Azure Monitor
APPLICATIONINSIGHTS_CONNECTION_STRING=

# Identity (local dev / service principal only)
AZURE_CLIENT_ID=
AZURE_CLIENT_SECRET=
AZURE_TENANT_ID=
```

---

## Azure AI Foundry (MAF — Microsoft Agent Framework)

The **Responses API v2** with `azure-ai-projects` SDK is the standard for all agent work.

```python
# Install: azure-ai-projects>=1.0.0
from azure.ai.projects.aio import AIProjectClient
from azure.ai.projects.models import (
    MessageTextContent,
    AgentThread,
    RunStatus,
)
from azure.identity.aio import DefaultAzureCredential

async def create_foundry_client() -> AIProjectClient:
    return AIProjectClient(
        endpoint=settings.azure_ai_project_endpoint,
        credential=DefaultAzureCredential(),
    )

# Agent definition pattern — define agents in Azure AI Foundry Studio
# Reference them by name in code, never recreate on every request
async def get_or_create_agent(client: AIProjectClient, name: str, instructions: str, model: str):
    agents = await client.agents.list_agents()
    for agent in agents.data:
        if agent.name == name:
            return agent
    return await client.agents.create_agent(
        model=model,
        name=name,
        instructions=instructions,
    )
```

### Agent Naming Convention

```
{Domain}{Function}Agent
```
Examples:
- `PortfolioAnalysisAgent` — Analyzes portfolio composition and risk
- `MarketIntelligenceAgent` — Fetches and synthesizes market news
- `TranscriptionAgent` — Converts speech to structured transcript
- `RiskAdvisoryAgent` — Evaluates and scores financial risk
- `RebalanceAgent` — Generates rebalancing recommendations
- `ComplianceAgent` — Checks regulatory compliance

---

## Azure Cosmos DB

Use the **async SDK** with `DefaultAzureCredential`. Partition keys must align with access patterns.

```python
# Install: azure-cosmos>=4.7.0
from azure.cosmos.aio import CosmosClient
from azure.identity.aio import DefaultAzureCredential

async def upsert_session(session_id: str, data: dict, client_id: str) -> dict:
    credential = DefaultAzureCredential()
    cosmos = CosmosClient(url=settings.cosmos_endpoint, credential=credential)
    db = cosmos.get_database_client(settings.cosmos_database)
    container = db.get_container_client("sessions")

    item = {
        "id": session_id,
        "client_id": client_id,  # Partition key
        **data,
    }
    return await container.upsert_item(item)
```

### Container Design

| Container | Partition Key | Purpose |
|---|---|---|
| `clients` | `/advisor_id` | Client profiles, KYC data |
| `sessions` | `/client_id` | Meeting/interaction sessions |
| `portfolios` | `/client_id` | Portfolio positions and history |
| `backtests` | `/client_id` | Backtesting results |
| `audit_log` | `/advisor_id` | Immutable audit trail |
| `rebalance_reports` | `/client_id` | Rebalancing history |

Always set `enable_cross_partition_query=True` for cross-partition queries.
Use `query_items` with `max_item_count` for paginated results.

---

## Azure AI Search

Use **hybrid search** (vector + keyword) for all financial document retrieval.

```python
# Install: azure-search-documents>=11.6.0
from azure.search.documents.aio import SearchClient
from azure.search.documents.models import VectorizableTextQuery
from azure.identity.aio import DefaultAzureCredential

async def hybrid_search(query: str, top: int = 5) -> list[dict]:
    client = SearchClient(
        endpoint=settings.azure_search_endpoint,
        index_name=settings.azure_search_index,
        credential=DefaultAzureCredential(),
    )
    async with client:
        results = await client.search(
            search_text=query,
            vector_queries=[
                VectorizableTextQuery(
                    text=query,
                    k_nearest_neighbors=50,
                    fields="content_vector",
                )
            ],
            top=top,
            select=["id", "title", "content", "source", "instrument_id", "document_date"],
        )
        return [result async for result in results]
```

### Index Schema for Financial Documents

```json
{
  "fields": [
    { "name": "id", "type": "Edm.String", "key": true },
    { "name": "title", "type": "Edm.String", "searchable": true },
    { "name": "content", "type": "Edm.String", "searchable": true },
    { "name": "instrument_id", "type": "Edm.String", "filterable": true },
    { "name": "document_type", "type": "Edm.String", "filterable": true, "facetable": true },
    { "name": "document_date", "type": "Edm.DateTimeOffset", "sortable": true, "filterable": true },
    { "name": "content_vector", "type": "Collection(Edm.Single)", "searchable": true, "vectorSearchDimensions": 1536 }
  ]
}
```

---

## Azure Speech Services

Use the **REST API** for batch transcription and the **SDK** for real-time streaming.

```python
# Install: azure-cognitiveservices-speech>=1.38.0
import azure.cognitiveservices.speech as speechsdk

def create_speech_config() -> speechsdk.SpeechConfig:
    config = speechsdk.SpeechConfig(
        subscription=settings.azure_speech_key,
        region=settings.azure_speech_region,
    )
    config.speech_recognition_language = "en-US"
    # Financial terminology optimization
    config.set_property(
        speechsdk.PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "2000"
    )
    return config
```

---

## Azure AI Content Safety

Run content safety checks on ALL user-provided text before passing to agents.

```python
# Install: azure-ai-contentsafety>=1.0.0
from azure.ai.contentsafety.aio import ContentSafetyClient
from azure.ai.contentsafety.models import AnalyzeTextOptions, TextCategory
from azure.identity.aio import DefaultAzureCredential

async def check_content_safety(text: str) -> bool:
    """Returns True if content is safe, raises ValueError if not."""
    client = ContentSafetyClient(
        endpoint=settings.content_safety_endpoint,
        credential=DefaultAzureCredential(),
    )
    async with client:
        request = AnalyzeTextOptions(text=text)
        response = await client.analyze_text(request)
        for item in response.categories_analysis:
            if item.severity and item.severity >= 4:
                raise ValueError(
                    f"Content safety violation: {item.category} severity {item.severity}"
                )
    return True
```

---

## Azure Document Intelligence

For processing financial documents (annual reports, prospectuses, policy documents).

```python
# Install: azure-ai-documentintelligence>=1.0.0
from azure.ai.documentintelligence.aio import DocumentIntelligenceClient
from azure.identity.aio import DefaultAzureCredential

async def analyze_financial_document(document_url: str) -> dict:
    client = DocumentIntelligenceClient(
        endpoint=settings.document_intelligence_endpoint,
        credential=DefaultAzureCredential(),
    )
    async with client:
        poller = await client.begin_analyze_document(
            model_id="prebuilt-layout",
            body={"url_source": document_url},
        )
        result = await poller.result()
        return {
            "pages": len(result.pages),
            "tables": [t.as_dict() for t in (result.tables or [])],
            "content": result.content,
        }
```

---

## OpenTelemetry + Azure Monitor

All agent calls, database operations, and external API calls must be instrumented.

```python
# app/infra/telemetry.py
from azure.monitor.opentelemetry import configure_azure_monitor
from opentelemetry import trace
from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
from opentelemetry.instrumentation.httpx import HTTPXClientInstrumentor

def setup_telemetry():
    configure_azure_monitor()
    FastAPIInstrumentor().instrument()
    HTTPXClientInstrumentor().instrument()

tracer = trace.get_tracer("financial-ai-app")

# Span attributes for financial context (never log PII values)
SPAN_ATTRS = {
    "portfolio.id": "portfolio_id",
    "session.id": "session_id",
    "agent.name": "agent_name",
    "operation.type": "operation_type",
}
```

---

## Deployment

All services deploy to **Azure Container Apps** or **Azure App Service** with:
- Managed Identity enabled
- Application Insights connected
- Key Vault references for secrets
- Bicep/ARM templates in `infra/` directory

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
