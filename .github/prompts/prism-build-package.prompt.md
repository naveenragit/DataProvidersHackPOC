---
mode: agent
description: "Autonomously build ONE Prism implementation-plan work package end-to-end: detailed plan → implement → adversarial review → correct → update TASKS."
---

# Prism — Automated Build Package Workflow

Drive **one** work package from `implementationPlan/` through a full
**plan → implement → adversarial-review → correct → track** loop, delegating each phase to a
specialist **sub-agent** (run-subagent). You (the agent running this prompt) are the *orchestrator*:
you never implement directly — you load context, dispatch sub-agents, gate on their results, and
update the tracker.

## Input

`$ARGUMENTS` = the package number or slug (e.g. `02` or `02-domain-model-and-notch-ladder`).
If empty: read `architecturalPlan/TASKS.md` + `implementationPlan/README.md`, pick the
lowest-numbered package whose dependencies are complete and that is not yet done, and **confirm the
choice with the user** before proceeding.

## Guardrails (every phase)

- The package's own **acceptance criteria** are the definition of done.
- Enforce `architecturalPlan/00-core-principles.md` (P1–P8) — especially **P2 deterministic core**
  (notch math / red-flag triggers stay in `Analysis/`, LLM only narrates/cites) and **P4 never say
  buy/sell/hold/recommend/allocate/trade/alpha/signal**.
- **Never** mark a TASKS item complete unless: acceptance criteria met, `dotnet build` + `dotnet test`
  (and frontend `npm` build/test where relevant) pass, and adversarial review has **no unresolved
  Critical/High** findings.
- All artifacts go under `.copilot-tracking/` (paths below). Never delete prior artifacts.
- Summarize each phase back to the user (sub-agent output is not visible to them otherwise).

---

## Phase 0 — Load context (you)

1. Read `implementationPlan/{NN}-*.md` — capture tasks, file paths, and the **Acceptance** checklist.
2. Read `implementationPlan/README.md` (dependencies/build order) and `architecturalPlan/README.md`.
3. Read `architecturalPlan/00-core-principles.md` + the arch files relevant to this package
   (e.g. agents→`04`, API→`09`, data→`08`, frontend→`10`).
4. Read `architecturalPlan/TASKS.md`; verify prerequisite packages are `[x]`. If a dependency is
   incomplete, **stop and report** which one blocks this package.

## Phase 1 — Detailed plan → run-subagent **Fin Task Planner**

Dispatch with instructions to:
- Read `implementationPlan/{NN}-*.md` and all of `architecturalPlan/`.
- Produce a precise, **one-task-per-item, file-level** plan that conforms to the architecture
  standards (naming `01`, folders `02`, errors `03`, agents `04`, config `05`, security `06`,
  observability `07`, data `08`, API `09`, frontend `10`, testing `11`).
- Save to `.copilot-tracking/plans/{{YYYY-MM-DD}}/{NN}-plan.md`; return the path + task list.

**Gate:** the plan must cover every acceptance criterion. If gaps remain, re-dispatch once with the
gaps named.

## Phase 2 — Implement → run-subagent **Fin Task Implementor**

Dispatch with instructions to:
- Implement `.copilot-tracking/plans/{{YYYY-MM-DD}}/{NN}-plan.md` exactly, following
  `architecturalPlan/` standards. Deterministic logic in `Analysis/` (P2); no mock/fallback data
  (P1); no buy/sell language (P4); `CancellationToken` plumbed (P7).
- Run `dotnet build` + `dotnet test` (and frontend build/test if touched); fix failures.
- Record every file created/modified to `.copilot-tracking/changes/{{YYYY-MM-DD}}/{NN}-changes.md`.

Return the changes-log path + build/test status.

## Phase 3 — Adversarial review → run-subagents (independent — dispatch together)

Each reviews the implementation **against the plan and `architecturalPlan/`**:

| Sub-agent | Attacks |
|---|---|
| **Prism Standards Adversary** | P1–P8 conformance, naming/folder/error/DTO rules, package acceptance criteria |
| **Fin Adversary Architect** | layering, coupling, topology, failure modes |
| **Fin Adversary Security** | auth, secrets, PII, tool-arg trust, OWASP |
| **Fin Adversary Stack Critic** | SDK/API accuracy, version pinning, interop |

Consolidate all findings (tagged **Critical/High/Medium/Low**, file-anchored) into
`.copilot-tracking/reviews/{{YYYY-MM-DD}}/{NN}-review.md`.

## Phase 4 — Correct → run-subagent **Fin Task Implementor**

- Feed the consolidated findings back. Fix **all Critical/High** and every feasible **Medium**.
- Re-run build/tests; append corrections to the changes log.
- **Re-run Phase 3.** Loop Phase 3 ↔ 4 up to **2** times. If Critical/High remain after 2 loops,
  **STOP and escalate** to the user with the specific unresolved findings.

## Phase 5 — Final validation → run-subagent **Fin Task Reviewer**

- Validate against acceptance criteria + architecture standards + the workflow-visualization
  requirement (if the package touches the pipeline). Confirm build/tests green.
- If it fails, return to Phase 4 once; otherwise proceed.

## Phase 6 — Track → update `architecturalPlan/TASKS.md` (you)

- Check off the package's tasks: `[x]` done, `[~]` partial, `[!]` blocked (with reason).
- Append an entry to the **Implementation Log** section: date · package · files changed (link the
  changes log) · adversarial findings summary (counts by severity + how resolved) · residual risks ·
  build/test status.
- Update the package's row status in `implementationPlan/README.md`.

## Output (to the user)

A concise summary: package built · plan/changes/review artifact links · findings resolved vs residual ·
build/test status · **the next unblocked package** to run with `/prism-build-package`.
