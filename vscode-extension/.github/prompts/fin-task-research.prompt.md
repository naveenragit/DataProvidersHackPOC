---
mode: agent
description: "Start financial domain research for a new feature, integration, or architectural decision"
---

# Financial Domain Research

Research the following task thoroughly using Azure documentation, codebase analysis, and financial domain knowledge.

**Topic:** $TOPIC

## Research Goals

1. Understand the existing codebase patterns (Python backend, React frontend) relevant to this task
2. Identify the correct Azure services and SDKs to use (prefer `azure-ai-projects`, `azure-cosmos`, `azure-search-documents`)
3. Understand financial domain constraints (regulations, data formats, security requirements)
4. Evaluate implementation alternatives and recommend one approach
5. Identify the Workflow visualization impact — does this feature require a new workflow node or connection?

## Output

Produce a research document at `.copilot-tracking/research/{{YYYY-MM-DD}}/$TOPIC-research.md` covering:
- Scope, assumptions, and success criteria
- Azure service selection with rationale
- Financial domain considerations
- Selected implementation approach with full code examples
- Workflow visualization requirements
- Security and compliance notes

Start research now.
