---
mode: agent
description: "Execute the implementation plan for a financial services feature"
---

# Financial Domain Implementation

Execute the implementation plan task by task with verification at each step.

## Pre-Implementation Checklist

Before implementing, verify:
- [ ] Plan document is open and attached
- [ ] Research document referenced in plan is available
- [ ] Understanding of all Azure service dependencies
- [ ] Understanding of financial domain constraints for this feature

## Implementation Rules

1. **Follow the plan exactly** — do not add features not in the plan
2. **Python backend first** — implement services and routers before frontend
3. **Types first in TypeScript** — define interfaces before components
4. **Workflow page last** — update `WorkflowPage.tsx` after all feature code is done
5. **Never hardcode credentials** — always use settings / environment variables
6. **Always add PII safety** — add content safety checks on any new user input endpoints
7. **Always add audit logging** — log all financial data mutations

## Task Execution

For each task in the plan:
1. Mark the checkbox as in-progress
2. Implement the change
3. Verify the implementation matches the plan specification
4. Mark the checkbox as complete
5. Log the change in `.copilot-tracking/changes/{{YYYY-MM-DD}}/$TOPIC-changes.md`

## Workflow Visualization Implementation

When implementing the Workflow page update:
1. Open `.github/skills/workflow-visualization/SKILL.md` for the component specification
2. Add new nodes to `workflowData.ts` (or equivalent data file)
3. Update `WorkflowPage.tsx` with new connections
4. Verify the detail panel content renders correctly for new nodes

## Output

Maintain `.copilot-tracking/changes/{{YYYY-MM-DD}}/$TOPIC-changes.md` with:
- All files created or modified
- Summary of changes per file
- Any deviations from the plan with rationale

Start implementing now.
