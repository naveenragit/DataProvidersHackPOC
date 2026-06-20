---
description: "Python backend coding standards for FastAPI financial services applications on Azure"
applyTo: "**/*.py"
---

# Python Backend Standards — Financial Services

## Project Structure

Every Python backend follows this exact structure:

```
backend/
├── app/
│   ├── main.py                   # FastAPI app factory, lifespan, CORS, routers
│   ├── agents/                   # One file per Azure AI Foundry agent
│   │   └── {name}_agent.py
│   ├── models/                   # Pydantic v2 models ONLY — no business logic
│   │   └── {domain}_models.py
│   ├── routers/                  # FastAPI APIRouter — one file per domain
│   │   └── {domain}.py
│   ├── services/                 # Business logic, orchestration calls
│   │   └── {domain}_service.py
│   ├── orchestration/            # Multi-agent pipeline orchestration
│   │   └── {workflow}_workflow.py
│   └── infra/
│       ├── settings.py           # pydantic-settings BaseSettings
│       ├── cosmos.py             # Cosmos DB client factory
│       ├── search.py             # Azure AI Search client factory
│       └── telemetry.py          # OpenTelemetry setup
├── .venv/                        # Local virtual environment (gitignored)
├── requirements.txt
├── .gitignore
├── .env.example
└── start.ps1
```

## Environment Setup (required)

All Python dependencies **must** be installed into a project-local virtual environment named
`.venv`. Never install packages globally or into the system interpreter.

```powershell
# Create the virtual environment (run from the backend/ folder)
python -m venv .venv

# Activate it
.\.venv\Scripts\Activate.ps1      # Windows PowerShell
# source .venv/bin/activate        # macOS / Linux

# Install dependencies into .venv
python -m pip install --upgrade pip
pip install -r requirements.txt
```

Rules:

- The virtual environment directory is always `.venv` at the backend root.
- Activate `.venv` before running, testing, or installing anything.
- `start.ps1` must create and/or activate `.venv` before launching Uvicorn.
- Every backend **must** include a `.gitignore` that excludes `.venv/`, caches, and secrets.

### Required `.gitignore` (backend)

Always create this `.gitignore` at the backend root when scaffolding or implementing:

```gitignore
# Virtual environment
.venv/
venv/
env/

# Python caches
__pycache__/
*.py[cod]
*$py.class
.pytest_cache/
.mypy_cache/
.ruff_cache/
*.egg-info/
.coverage
htmlcov/

# Environment & secrets — never commit
.env
.env.*
!.env.example

# Editor / OS
.vscode/
.idea/
.DS_Store
Thumbs.db
```

## FastAPI App Factory (`app/main.py`)

```python
from contextlib import asynccontextmanager
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from app.infra.settings import get_settings
from app.infra.telemetry import setup_telemetry
from app.routers import portfolio, meetings, advisory, health

settings = get_settings()

@asynccontextmanager
async def lifespan(app: FastAPI):
    setup_telemetry()
    yield

app = FastAPI(
    title="Financial Services AI API",
    version="1.0.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.cors_origins,  # Never use ["*"] in production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(health.router, prefix="/api", tags=["health"])
app.include_router(portfolio.router, prefix="/api/v1", tags=["portfolio"])
app.include_router(meetings.router, prefix="/api/v1", tags=["meetings"])
```

## Configuration (`app/infra/settings.py`)

```python
from functools import lru_cache
from pydantic_settings import BaseSettings, SettingsConfigDict
from pydantic import AnyHttpUrl, field_validator
from typing import List

class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")

    # Azure AI Foundry
    azure_ai_project_endpoint: str
    azure_ai_project_subscription_id: str
    azure_ai_project_resource_group: str
    azure_ai_project_name: str

    # Azure OpenAI
    azure_openai_endpoint: str
    azure_openai_api_version: str = "2024-12-01-preview"
    agent_model: str = "gpt-4o"

    # Azure Cosmos DB
    cosmos_endpoint: str
    cosmos_database: str
    cosmos_containers: dict = {}

    # Azure AI Search
    azure_search_endpoint: str
    azure_search_index: str

    # CORS
    cors_origins: List[str] = ["http://localhost:5173"]

    @field_validator("cors_origins", mode="before")
    @classmethod
    def parse_cors(cls, v):
        if isinstance(v, str):
            return [origin.strip() for origin in v.split(",")]
        return v

@lru_cache
def get_settings() -> Settings:
    return Settings()
```

## Pydantic v2 Models (`app/models/`)

```python
from pydantic import BaseModel, Field, field_validator
from typing import Optional, List
from datetime import datetime
from enum import Enum

class AssetClass(str, Enum):
    EQUITY = "equity"
    FIXED_INCOME = "fixed_income"
    DERIVATIVE = "derivative"
    ALTERNATIVE = "alternative"

class PortfolioPosition(BaseModel):
    instrument_id: str = Field(..., description="ISIN or internal instrument ID")
    asset_class: AssetClass
    quantity: float = Field(..., gt=0)
    market_value: float
    weight: float = Field(..., ge=0, le=1)
    last_updated: datetime

class PortfolioSummary(BaseModel):
    portfolio_id: str
    client_id: str
    total_value: float
    positions: List[PortfolioPosition]
    risk_score: Optional[float] = None
    benchmark: Optional[str] = None

# Request/Response models always separate from domain models
class RebalanceRequest(BaseModel):
    target_weights: dict[str, float]
    reason: str = Field(..., max_length=500)
    approved_by: Optional[str] = None
```

## Router Pattern (`app/routers/`)

```python
from fastapi import APIRouter, Depends, HTTPException, status
from app.models.portfolio_models import PortfolioSummary, RebalanceRequest
from app.services.portfolio_service import PortfolioService
from app.infra.settings import get_settings, Settings

router = APIRouter(prefix="/portfolios")

@router.get("/{portfolio_id}", response_model=PortfolioSummary)
async def get_portfolio(
    portfolio_id: str,
    service: PortfolioService = Depends(),
    settings: Settings = Depends(get_settings),
) -> PortfolioSummary:
    portfolio = await service.get_portfolio(portfolio_id)
    if not portfolio:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail={"code": "PORTFOLIO_NOT_FOUND", "message": f"Portfolio {portfolio_id} not found"},
        )
    return portfolio

@router.post("/{portfolio_id}/rebalance", status_code=status.HTTP_202_ACCEPTED)
async def rebalance_portfolio(
    portfolio_id: str,
    request: RebalanceRequest,
    service: PortfolioService = Depends(),
) -> dict:
    job_id = await service.submit_rebalance(portfolio_id, request)
    return {"job_id": job_id, "status": "accepted"}
```

## Azure AI Foundry Agent Pattern (`app/agents/`)

```python
from azure.ai.projects.aio import AIProjectClient
from azure.identity.aio import DefaultAzureCredential
from app.infra.settings import get_settings
import logging

logger = logging.getLogger(__name__)
settings = get_settings()

async def run_portfolio_analysis_agent(
    portfolio_summary: str,
    market_context: str,
) -> str:
    """Run the portfolio analysis agent using Azure AI Foundry Responses API v2."""
    credential = DefaultAzureCredential()
    client = AIProjectClient(
        endpoint=settings.azure_ai_project_endpoint,
        credential=credential,
    )

    async with client:
        agent = await client.agents.get_agent(name="PortfolioAnalysisAgent")
        thread = await client.agents.create_thread()
        await client.agents.create_message(
            thread_id=thread.id,
            role="user",
            content=f"Analyze portfolio:\n{portfolio_summary}\n\nMarket context:\n{market_context}",
        )
        run = await client.agents.create_and_process_run(
            thread_id=thread.id,
            agent_id=agent.id,
        )
        messages = await client.agents.list_messages(thread_id=thread.id)
        return messages.data[0].content[0].text.value
```

## Cosmos DB Pattern (`app/infra/cosmos.py`)

```python
from azure.cosmos.aio import CosmosClient
from azure.identity.aio import DefaultAzureCredential
from app.infra.settings import get_settings
from functools import lru_cache

@lru_cache
def get_cosmos_client() -> CosmosClient:
    settings = get_settings()
    return CosmosClient(
        url=settings.cosmos_endpoint,
        credential=DefaultAzureCredential(),
    )

async def get_container(database: str, container: str):
    client = get_cosmos_client()
    db = client.get_database_client(database)
    return db.get_container_client(container)
```

## WebSocket for Real-Time Features

```python
from fastapi import WebSocket, WebSocketDisconnect
from app.services.transcription_service import TranscriptionService

@router.websocket("/ws/transcribe/{session_id}")
async def transcribe_ws(websocket: WebSocket, session_id: str):
    await websocket.accept()
    service = TranscriptionService(session_id)
    try:
        while True:
            audio_chunk = await websocket.receive_bytes()
            result = await service.process_chunk(audio_chunk)
            await websocket.send_json({
                "type": "transcript_chunk",
                "session_id": session_id,
                "text": result.text,
                "is_final": result.is_final,
                "speaker": result.speaker,
                "timestamp": result.timestamp,
            })
    except WebSocketDisconnect:
        await service.cleanup()
```

## Error Handling

- Use `HTTPException` with structured detail dicts: `{"code": "ERROR_CODE", "message": "Human readable"}`
- Never expose internal stack traces to the API response
- Log exceptions with `logger.exception()` for unexpected errors
- Use `logger.warning()` for expected business errors (not found, validation)
- Never catch and swallow exceptions silently

## PII and Security

- Run Azure AI Content Safety checks on all user text inputs before passing to agents
- Redact PII from all logs — names, account numbers, SSN, dates of birth
- Validate and sanitize all inputs at the router level using Pydantic
- Use `azure-keyvault-secrets` to retrieve secrets in production; never hardcode

## OpenTelemetry

```python
# app/infra/telemetry.py
from azure.monitor.opentelemetry import configure_azure_monitor
from opentelemetry import trace

def setup_telemetry():
    configure_azure_monitor()  # Uses APPLICATIONINSIGHTS_CONNECTION_STRING env var

tracer = trace.get_tracer(__name__)

# Usage in services:
with tracer.start_as_current_span("portfolio_analysis") as span:
    span.set_attribute("portfolio.id", portfolio_id)
    span.set_attribute("client.id", client_id)
    result = await run_analysis(...)
```
