# End-to-End RPI Test Scenarios

Three real-world features — one each for **Capital Markets**, **Banking**, and **Insurance** —
that exercise the full Research → Plan → Implement → Review cycle. Each scenario is
deliberately built on a **different orchestration and deployment path** so the kit is
tested across the choices a real customer engagement would face.

---

## How the three scenarios vary

| # | Domain | Feature | Orchestration | Foundry agent type | Knowledge / grounding | What it stress-tests |
|---|---|---|---|---|---|---|
| 1 | **Capital Markets** | Portfolio Risk & Rebalancing Advisor | **Microsoft Agent Framework (MAF)** | Prompt Agents (Responses API v2) | Bing grounding + AI Search | Azure-native multi-agent orchestration with built-in HITL checkpoints |
| 2 | **Banking** | Loan Credit Risk Scoring | Direct async (`asyncio.gather`) | Prompt Agents (Responses API v2) | **Foundry IQ knowledge base** | Foundry IQ agentic retrieval as the grounding layer |
| 3 | **Insurance** | Claims Triage & Reserve Estimate | **LangChain / LangGraph** | **Hosted Agents deployed in Azure** (Responses API v2) | AI Search + Document Intelligence | Non-Azure orchestrator over Azure-hosted agents |

> **Pick the variation deliberately.** The orchestration framework, the Foundry agent
> type, and the knowledge layer are named *in each research prompt* so the Researcher
> investigates the right SDK surface and the Planner generates the right tasks. Change
> the named choice and you change which code path the kit produces.

---

## Shared conventions (apply to all three scenarios)

These are constant across every scenario. They are stated here once; each research prompt
references them so you don't have to repeat them.

- **Responses API v2, not v1 Assistants.** All Foundry agents use the **Azure AI Foundry
  Responses API (v2)** via `azure-ai-projects` — create/reference agents and call
  `responses.create`. Do **not** use the deprecated v1 Assistants API
  (`assistants` / `threads` / `runs`).
- **Real Azure only.** No mock clients or canned responses in runtime code. If a required
  Azure setting is missing, fail loudly and list the `.env` variables to populate.
- **Auth:** `DefaultAzureCredential` everywhere — never hardcoded keys.
- **Persistence:** Azure Cosmos DB (async client). Each scenario names its container and a
  candidate partition key; the Researcher must validate the key against query patterns.
- **Compliance baseline:** Content Safety on user text input, PII never logged, an audit
  log entry for every consequential mutation, and a **human-in-the-loop gate** before any
  consequential action is finalized.
- **Frontend:** React + TypeScript + Tailwind, dark theme, `lucide-react` icons, plus a new
  tab on the **Workflow visualization** page with one node per agent/service/gate/outcome
  and a populated detail panel for each.
- **API prefix:** `/api/v1/...`. **Health:** `GET /api/health`.

---

# Scenario 1 — Capital Markets: Portfolio Risk & Rebalancing Advisor

> **Focus: Microsoft Agent Framework (MAF) orchestration** with built-in human-in-the-loop
> checkpointing. Prompt Agents on the Responses API v2.

## The Feature

**Context:** A portfolio manager submits a client portfolio. A MAF-orchestrated multi-agent
pipeline analyzes market and concentration risk, proposes rebalancing trades within the
client's mandate, and checks suitability — pausing at a human advisor approval gate before
any proposed trades are surfaced.

**What gets built:**
- Python FastAPI route: `POST /api/v1/portfolios/{portfolio_id}/analyze`
- 3 Azure AI Foundry **Prompt Agents** (Responses API v2):
  - `MarketRiskAgent` — computes VaR, beta, max drawdown, and concentration from positions
  - `RebalanceProposalAgent` — proposes trades to reach target allocation within constraints
  - `SuitabilityAgent` — checks the proposal against the client risk profile / IPS (MiFID II suitability)
- **Microsoft Agent Framework** workflow orchestrating the agents (parallel risk + proposal,
  then suitability), with a **Cosmos DB checkpoint store** and a first-class **HITL gate**
- Azure AI Search for the client's Investment Policy Statement; Bing grounding for market context
- Cosmos DB container `portfolio_proposals`
- React page: Portfolio Risk with allocation donut, risk-metric cards, proposed-trades table, approve/reject
- Workflow visualization tab: "Portfolio Rebalancing Pipeline"
- Settings: model selection per agent, risk-tolerance band, max single-position weight

## PHASE 1: Research

Select the **Fin Task Researcher** agent and send:

```
I need to build a portfolio risk and rebalancing advisor for a capital markets / wealth
management application. Follow the Shared Conventions in TEST-SCENARIO.md.

The feature should:
1. Accept a portfolio (client_id, positions[] with instrument/CUSIP, quantity, cost basis,
   market value; target allocation; client risk profile / IPS reference)
2. Run a MULTI-AGENT pipeline of 3 Azure AI Foundry Prompt Agents:
   - MarketRiskAgent: compute VaR, beta, max drawdown, sector/issuer concentration
   - RebalanceProposalAgent: propose buy/sell trades to reach target allocation within
     constraints (max single-position weight, no wash sales, cash buffer)
   - SuitabilityAgent: validate the proposal against the client risk profile / IPS (MiFID II)
3. Merge into a structured RebalanceProposal object and persist to Cosmos DB
4. Pause at a human advisor approval gate (GATE-1) before the proposal is surfaced
5. React UI: allocation donut, risk-metric cards, proposed-trades table, approve/reject
6. New "Portfolio Rebalancing Pipeline" tab in the Workflow page

ORCHESTRATION CONSTRAINT — use Microsoft Agent Framework (MAF):
- Investigate and VERIFY the current MAF package and version (pip install agent-framework
  --pre), the correct imports, and how to build a workflow that runs MarketRiskAgent and
  RebalanceProposalAgent in parallel, then SuitabilityAgent, with a human-in-the-loop gate
- Verify MAF checkpoint/state persistence to Cosmos DB and how the HITL gate suspends and
  resumes the workflow
- The agents themselves must be Azure AI Foundry Prompt Agents called via the Responses
  API v2 (azure-ai-projects, responses.create) — NOT the v1 Assistants API. Confirm how
  MAF invokes a Responses-API-v2 agent as a workflow step

Also research and decide:
- Cosmos DB container "portfolio_proposals": validate partition key candidate /client_id
  against the expected query patterns (by client, by date range)
- Azure AI Search retrieval pattern for the client IPS document, and Bing grounding for
  market context — which agent uses which, and the verified SDK calls
- Pydantic v2 model design for RebalanceProposal with nested per-agent outputs and a
  bounded risk score
- React allocation-donut + proposed-trades table pattern (recharts)
- Workflow node definitions: 3 agent nodes + 1 MAF orchestration/service node + GATE-1 +
  an "Proposal Approved" outcome node, with detail-panel content for each

Research output file: .copilot-tracking/research/[today]/portfolio-rebalance-research.md
```

## PHASE 2: Plan

`/clear`, open the research doc, then with **Fin Task Planner**:

```
/fin-task-plan
```

Expected plan (`.copilot-tracking/planning/[today]/portfolio-rebalance-plan.md`) should
include a Workflow Visualization phase with: 3 agent nodes, 1 MAF orchestration node,
GATE-1 (Advisor Review), 1 outcome node, and the "Portfolio Rebalancing Pipeline" tab.

## PHASE 3: Implement

`/clear`, open the plan, then with **Fin Task Implementor**:

```
/fin-task-implement
```

Expected files created:
- `backend/app/models/portfolio_models.py`
- `backend/app/agents/market_risk_agent.py`
- `backend/app/agents/rebalance_proposal_agent.py`
- `backend/app/agents/suitability_agent.py`
- `backend/app/orchestration/rebalance_workflow.py` (Microsoft Agent Framework workflow + HITL gate)
- `backend/app/routers/portfolios.py`
- `frontend/src/types/portfolioTypes.ts`
- `frontend/src/pages/Portfolios/PortfolioRiskPage.tsx`
- `frontend/src/components/AllocationDonut.tsx`
- `frontend/src/data/workflowData.ts` (updated with Portfolio Rebalancing Pipeline tab)

## PHASE 4: Review

`/clear`, open the changes log, then with **Fin Task Reviewer**:

```
/fin-task-review
```

The reviewer will check:
- [ ] Agents use Responses API v2 (`responses.create`), not v1 Assistants
- [ ] MAF workflow runs risk + proposal in parallel, then suitability, with a real HITL gate
- [ ] MAF state/checkpoint persisted to Cosmos DB; gate suspends and resumes correctly
- [ ] Content Safety on any free-text input; PII (client identifiers, holdings) not logged
- [ ] Audit log entry for every proposal created and every approve/reject
- [ ] DefaultAzureCredential used (not hardcoded keys)
- [ ] Suitability rationale present (MiFID II requirement); risk metrics bounded/validated
- [ ] All nodes present in the Workflow tab with populated detail panels

## Verify after each phase

- **Research:** MAF package/version + workflow + checkpoint pattern verified; Responses-API-v2
  invocation from MAF documented; partition key `/client_id` validated; workflow nodes filled in.
- **Plan:** Workflow Visualization phase present; every task references an exact file path;
  compliance tasks (audit, content safety, gate) included.
- **Implement:** `GET http://localhost:8000/docs` shows `/api/v1/portfolios/{id}/analyze`;
  `/workflow` shows the "Portfolio Rebalancing Pipeline" tab with clickable nodes; Settings
  shows risk-tolerance and max-position controls.
- **Review:** doc at `.copilot-tracking/reviews/[today]/portfolio-rebalance-review.md`;
  verdict PASS or PASS WITH MINORS; zero Critical issues.

---

# Scenario 2 — Banking: Loan Credit Risk Scoring

> **Focus: Foundry IQ** as the knowledge/grounding layer, with a lightweight **direct async
> (`asyncio.gather`)** orchestration. Prompt Agents on the Responses API v2.

## The Feature

**Context:** A commercial bank needs an AI-assisted credit risk assessment workflow. A loan
officer submits an application; a multi-agent pipeline scores creditworthiness, checks
KYC/AML status, and scans recent news — all grounded by a **Foundry IQ knowledge base** —
and produces a structured risk report before a human approval gate.

**What gets built:**
- Python FastAPI route: `POST /api/v1/loans/assess`
- 3 Azure AI Foundry **Prompt Agents** (Responses API v2): `CreditScoringAgent`,
  `KYCCheckAgent`, `NewsRiskAgent`
- A lightweight orchestration service running the three agents in parallel (`asyncio.gather`)
- **Foundry IQ knowledge base** grounding the KYC and credit agents over sanctions/PEP lists
  and company filings (agentic retrieval), with Bing grounding for current news
- Cosmos DB storage for assessment results
- React page: Loan Assessment with risk score gauge, findings panel, approval button
- Workflow visualization tab: "Credit Risk Pipeline"
- Settings page: model selection and risk-threshold configuration

## PHASE 1: Research

Select the **Fin Task Researcher** agent and send:

```
I need to build a commercial loan credit risk scoring service for a banking application.
Follow the Shared Conventions in TEST-SCENARIO.md.

The feature should:
1. Accept a loan application (company name, loan amount, loan purpose, financial statements
   as text)
2. Run 3 Azure AI Foundry Prompt Agents in parallel:
   - CreditScoringAgent: analyze financial statements, compute a risk score (0-100) + DSCR
   - KYCCheckAgent: check sanctions lists and PEP databases
   - NewsRiskAgent: scan recent news / adverse media for the company
3. Merge results into a structured CreditAssessment object and persist to Cosmos DB
4. Require human loan officer approval before finalizing (GATE-1)
5. React UI: risk score gauge, agent findings accordion, approve/reject buttons
6. New "Credit Risk Pipeline" tab in the Workflow page

KNOWLEDGE CONSTRAINT — use Foundry IQ:
- Investigate and VERIFY how to build a Foundry IQ knowledge base in Azure AI Foundry from
  knowledge sources (sanctions/PEP lists and company filings), and how to attach that
  knowledge base to a Responses-API-v2 agent so CreditScoringAgent and KYCCheckAgent ground
  their answers through Foundry IQ's agentic retrieval (rather than hand-rolled AI Search
  queries). Document the exact SDK surface and any required resource/role setup
- NewsRiskAgent uses Bing grounding for current adverse media — verify that SDK call
- Confirm the agents are Prompt Agents on the Responses API v2 (azure-ai-projects,
  responses.create) — NOT the v1 Assistants API

ORCHESTRATION:
- Lightweight: run the 3 agents concurrently with asyncio.gather in a service (no heavy
  framework). Verify the correct async Foundry project-client usage for concurrent
  responses.create calls and how to aggregate results safely

Also research and decide:
- Cosmos DB container "assessments": validate partition key candidate /company_id (queries
  filter by company and date range)
- Pydantic v2 model design for CreditAssessment with nested per-agent outputs and a bounded
  0-100 risk score (field validator)
- React gauge component pattern (recharts) for the risk score
- Workflow node definitions: 3 agent nodes + 1 orchestration/service node + GATE-1 +
  "Assessment Finalized" outcome node, with detail-panel content for each

Research output file: .copilot-tracking/research/[today]/loan-credit-risk-research.md
```

## PHASE 2: Plan

`/clear`, open the research doc, then with **Fin Task Planner**:

```
/fin-task-plan
```

Expected plan (`.copilot-tracking/planning/[today]/loan-credit-risk-plan.md`) should
include a Workflow Visualization phase with: 3 agent nodes (Credit/KYC/News), 1
orchestration service node, GATE-1 (Loan Officer Review), 1 "Assessment Finalized" outcome
node, and the "Credit Risk Pipeline" tab.

## PHASE 3: Implement

`/clear`, open the plan, then with **Fin Task Implementor**:

```
/fin-task-implement
```

Expected files created:
- `backend/app/models/loan_models.py`
- `backend/app/agents/credit_scoring_agent.py`
- `backend/app/agents/kyc_check_agent.py`
- `backend/app/agents/news_risk_agent.py`
- `backend/app/services/loan_orchestration_service.py` (asyncio.gather + Foundry IQ grounding)
- `backend/app/routers/loans.py`
- `frontend/src/types/loanTypes.ts`
- `frontend/src/pages/Loans/LoanAssessmentPage.tsx`
- `frontend/src/components/RiskScoreGauge.tsx`
- `frontend/src/data/workflowData.ts` (updated with Credit Risk Pipeline tab)

## PHASE 4: Review

`/clear`, open the changes log, then with **Fin Task Reviewer**:

```
/fin-task-review
```

The reviewer will check:
- [ ] Agents use Responses API v2 (`responses.create`), not v1 Assistants
- [ ] Credit/KYC agents ground through a Foundry IQ knowledge base (not ad-hoc AI Search)
- [ ] Content Safety on loan application text input
- [ ] PII not logged (applicant names, financials)
- [ ] Audit log entry for every assessment created
- [ ] Human gate present before assessment is finalized
- [ ] DefaultAzureCredential used (not hardcoded keys)
- [ ] All 3 agent nodes present in WorkflowPage with populated detail panels
- [ ] Risk score bounded 0-100 (Pydantic validator)
- [ ] Recommendation includes rationale (regulatory requirement)

## Verify after each phase

- **Research:** Foundry IQ knowledge-base creation + attach-to-agent SDK surface verified;
  Bing grounding call documented; `asyncio.gather` concurrency pattern for Responses-API-v2
  calls documented; partition key `/company_id` validated; workflow nodes filled in.
- **Plan:** Workflow Visualization phase present with specific node definitions; every task
  references an exact file path; compliance tasks (audit log, content safety, gate) included.
- **Implement:** `GET http://localhost:8000/docs` shows `/api/v1/loans/assess`; `/workflow`
  shows the "Credit Risk Pipeline" tab with clickable nodes; Settings shows risk-threshold config.
- **Review:** doc at `.copilot-tracking/reviews/[today]/loan-credit-risk-review.md`; verdict
  PASS or PASS WITH MINORS; zero Critical issues.

---

# Scenario 3 — Insurance: Claims Triage & Reserve Estimate

> **Focus: LangChain / LangGraph orchestration** over agents **deployed as Azure-hosted
> Foundry agents** (Hosted Agents), still on the Responses API v2.

## The Feature

**Context:** A claims handler submits a First Notice of Loss (FNOL) with supporting
documents. A LangChain-orchestrated multi-agent pipeline classifies the claim, scores fraud
signals, and estimates the reserve — with the agents deployed as **Azure AI Foundry Hosted
Agents** (persistent compute, code interpreter for actuarial math) — pausing at a human
adjuster approval gate before the reserve is set.

**What gets built:**
- Python FastAPI route: `POST /api/v1/claims/triage`
- 3 Azure AI Foundry **Hosted Agents** (Responses API v2):
  - `ClaimsTriageAgent` — classify claim type/severity; extract entities from the FNOL +
    documents (Azure Document Intelligence)
  - `FraudSignalAgent` — score fraud indicators against historical patterns (AI Search over
    prior claims)
  - `ReserveEstimateAgent` — estimate reserve / IBNR using actuarial logic (code interpreter)
- **LangChain / LangGraph** graph orchestrating the three Hosted Agents (triage → parallel
  fraud + reserve), invoking each via the Responses API v2
- Cosmos DB container `claims`
- React page: Claims Triage with severity badge, fraud gauge, reserve estimate, route/approve
- Workflow visualization tab: "Claims Triage Pipeline"
- Settings: model selection, fraud-score threshold, reserve confidence band

## PHASE 1: Research

Select the **Fin Task Researcher** agent and send:

```
I need to build a claims triage and reserve estimation service for a P&C insurance
application. Follow the Shared Conventions in TEST-SCENARIO.md.

The feature should:
1. Accept a First Notice of Loss (policy_id, claim type, loss description, claimant details,
   uploaded documents)
2. Run a MULTI-AGENT pipeline of 3 agents:
   - ClaimsTriageAgent: classify claim type/severity; extract structured fields from the FNOL
     and attached documents using Azure Document Intelligence
   - FraudSignalAgent: score fraud indicators vs historical patterns (Azure AI Search over
     prior claims)
   - ReserveEstimateAgent: estimate reserve / IBNR using actuarial calculations
3. Merge into a structured ClaimAssessment object and persist to Cosmos DB
4. Pause at a human adjuster approval gate (GATE-1) before the reserve is set
5. React UI: severity badge, fraud gauge, reserve estimate, route/approve buttons
6. New "Claims Triage Pipeline" tab in the Workflow page

ORCHESTRATION CONSTRAINT — use LangChain / LangGraph:
- Investigate and VERIFY the current LangChain + LangGraph packages and versions, and how to
  build a graph that runs ClaimsTriageAgent first, then FraudSignalAgent and
  ReserveEstimateAgent in parallel, then a human-in-the-loop interrupt before finalizing
- Verify how LangChain/LangGraph invokes Azure AI Foundry agents through the Responses API
  v2 (azure-ai-projects, responses.create) — NOT the v1 Assistants API — including auth with
  DefaultAzureCredential

DEPLOYMENT CONSTRAINT — Hosted Agents in Azure:
- The 3 agents must be deployed as Azure AI Foundry HOSTED Agents (persistent compute), not
  Prompt Agents. Investigate and VERIFY how to create/deploy a Hosted Agent, when a code
  interpreter tool is needed (ReserveEstimateAgent actuarial math), quota implications, and
  how the app references a Hosted Agent at runtime via the Responses API v2

Also research and decide:
- Azure Document Intelligence model/SDK for extracting fields from FNOL documents
- Cosmos DB container "claims": validate partition key candidate /policy_id against query
  patterns (by policy, by claim status, by date)
- Pydantic v2 model design for ClaimAssessment with nested per-agent outputs, a bounded
  fraud score, and a reserve estimate with a confidence band
- React severity-badge + fraud-gauge pattern
- Workflow node definitions: 3 Hosted-Agent nodes + 1 LangGraph orchestration node + GATE-1
  + "Reserve Set" outcome node, with detail-panel content for each

Research output file: .copilot-tracking/research/[today]/claims-triage-research.md
```

## PHASE 2: Plan

`/clear`, open the research doc, then with **Fin Task Planner**:

```
/fin-task-plan
```

Expected plan (`.copilot-tracking/planning/[today]/claims-triage-plan.md`) should include a
Workflow Visualization phase with: 3 Hosted-Agent nodes, 1 LangGraph orchestration node,
GATE-1 (Adjuster Review), 1 "Reserve Set" outcome node, and the "Claims Triage Pipeline" tab.

## PHASE 3: Implement

`/clear`, open the plan, then with **Fin Task Implementor**:

```
/fin-task-implement
```

Expected files created:
- `backend/app/models/claim_models.py`
- `backend/app/agents/claims_triage_agent.py`
- `backend/app/agents/fraud_signal_agent.py`
- `backend/app/agents/reserve_estimate_agent.py`
- `backend/app/orchestration/claims_graph.py` (LangChain / LangGraph graph + HITL interrupt)
- `backend/app/routers/claims.py`
- `frontend/src/types/claimTypes.ts`
- `frontend/src/pages/Claims/ClaimsTriagePage.tsx`
- `frontend/src/components/FraudGauge.tsx`
- `frontend/src/data/workflowData.ts` (updated with Claims Triage Pipeline tab)

## PHASE 4: Review

`/clear`, open the changes log, then with **Fin Task Reviewer**:

```
/fin-task-review
```

The reviewer will check:
- [ ] Agents are Hosted Agents on the Responses API v2, invoked via LangChain/LangGraph
- [ ] LangGraph runs triage → parallel fraud + reserve, with a real human interrupt gate
- [ ] Document Intelligence used for FNOL extraction; PII (claimant details) not logged
- [ ] Content Safety on free-text loss description
- [ ] Audit log entry for every claim assessment and every approve/route
- [ ] DefaultAzureCredential used (not hardcoded keys)
- [ ] Fraud score bounded and reserve estimate carries a confidence band (Pydantic validators)
- [ ] All nodes present in the Workflow tab with populated detail panels

## Verify after each phase

- **Research:** LangChain/LangGraph package + graph + HITL-interrupt pattern verified; Hosted
  Agent create/deploy + code-interpreter + Responses-API-v2 invocation documented; Document
  Intelligence model chosen; partition key `/policy_id` validated; workflow nodes filled in.
- **Plan:** Workflow Visualization phase present; every task references an exact file path;
  compliance tasks (audit, content safety, gate, Doc Intelligence) included.
- **Implement:** `GET http://localhost:8000/docs` shows `/api/v1/claims/triage`; `/workflow`
  shows the "Claims Triage Pipeline" tab with clickable nodes; Settings shows fraud-threshold
  and reserve-confidence controls.
- **Review:** doc at `.copilot-tracking/reviews/[today]/claims-triage-review.md`; verdict
  PASS or PASS WITH MINORS; zero Critical issues.
