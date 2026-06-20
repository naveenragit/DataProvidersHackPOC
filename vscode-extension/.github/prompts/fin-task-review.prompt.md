---
mode: agent
description: "Review and validate a completed financial services feature implementation"
---

# Financial Domain Implementation Review

Validate the completed implementation against research, plan, and financial domain standards.

## Review Checklist

### Code Quality
- [ ] Python code uses `async`/`await` for all I/O operations
- [ ] Pydantic v2 models with proper validation
- [ ] `pydantic-settings` used for all configuration (no raw `os.getenv`)
- [ ] No hardcoded credentials, endpoints, or secrets
- [ ] Proper error handling with structured error responses

### Azure Services
- [ ] `DefaultAzureCredential` used (not hardcoded keys)
- [ ] Async SDK clients used correctly
- [ ] Cosmos DB partition keys align with access patterns
- [ ] OpenTelemetry spans on all agent and external service calls

### Financial Domain
- [ ] Financial terminology used consistently in naming
- [ ] PII detection and redaction on all user inputs
- [ ] Audit logging for all financial data mutations
- [ ] Human-in-the-loop gates for consequential actions
- [ ] Recommendation rationale and source attribution included

### Security (OWASP)
- [ ] Input validation on all API endpoints
- [ ] No SQL/NoSQL injection vectors (parameterized queries)
- [ ] CORS configured to specific origins
- [ ] Content Safety checks on all user text inputs
- [ ] Sensitive data not logged

### Frontend
- [ ] Dark theme colors applied consistently (`bg-slate-900`, `bg-slate-800`, `border-slate-700`)
- [ ] `lucide-react` icons used
- [ ] TypeScript strict mode — no `any` types
- [ ] Financial data formatted correctly (currency, percentages, compact numbers)
- [ ] Error states handled in UI

### Workflow Visualization
- [ ] `WorkflowPage.tsx` updated with new nodes for this feature
- [ ] All new nodes have correct type colors
- [ ] All new nodes have populated detail panel content
- [ ] Connections between nodes are accurate

### Tests
- [ ] Unit tests added for new services
- [ ] API endpoint integration tests added
- [ ] Frontend component tests added

## Output

Produce a review document at `.copilot-tracking/reviews/{{YYYY-MM-DD}}/$TOPIC-review.md` with:
- Pass/fail for each checklist item
- Specific issues found with file paths and line numbers
- Severity: Critical (blocks merge) / Major (should fix) / Minor (nice to fix)
- Recommended follow-up work

Start review now.
