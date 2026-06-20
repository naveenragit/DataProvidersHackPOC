---
mode: agent
description: "Create an implementation plan from completed research for a financial services feature"
---

# Financial Domain Implementation Planning

Transform the completed research into a detailed, actionable implementation plan.

## Pre-Planning Checklist

Before planning, verify:
- [ ] Research document exists at `.copilot-tracking/research/{{YYYY-MM-DD}}/`
- [ ] Selected approach is documented with rationale
- [ ] Azure service dependencies are identified
- [ ] Financial domain constraints are listed

## Planning Requirements

The plan **must** include the following sections:

### 1. Backend Tasks
- FastAPI routes to add or modify (with exact file paths)
- Pydantic models to create
- Azure SDK integrations (with exact SDK calls)
- Cosmos DB container changes
- Agent definitions in Azure AI Foundry

### 2. Frontend Tasks
- React components to create or modify
- New pages required
- TypeScript types to define
- API client functions needed

### 3. Workflow Visualization Update (REQUIRED)
Every plan must include a task for updating `src/pages/WorkflowPage.tsx`:
- [ ] Define new workflow node(s) for this feature (node type, label, source file, description)
- [ ] Define connections between existing and new nodes
- [ ] Define detail panel content for each new node (responsibilities, data flow, technology tags)
- [ ] Assign node color by type: Service (blue), AI Agent (purple), Human Gate (amber), Data Store (teal), Outcome (green)

### 4. Architecture Sidebar Update
- [ ] Add new components to `src/pages/ArchitecturePage.tsx` if infrastructure changes
- [ ] Update Settings page if new configuration is needed

### 5. Security and Compliance Tasks
- [ ] Content safety checks on new user inputs
- [ ] Audit logging for new financial operations
- [ ] PII handling review

### 6. Testing Tasks
- [ ] Unit tests for new services and agents
- [ ] Integration tests for new API endpoints
- [ ] Frontend component tests

## Output

Create plan files at:
- `.copilot-tracking/planning/{{YYYY-MM-DD}}/$TOPIC-plan.md` — Phase checklist with file paths and line references
- `.copilot-tracking/planning/{{YYYY-MM-DD}}/$TOPIC-details.md` — Detailed per-task implementation notes

Start planning now.
