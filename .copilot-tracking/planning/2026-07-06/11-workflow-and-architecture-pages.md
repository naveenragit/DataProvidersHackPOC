# Plan 11 — Workflow & Architecture Pages (mandatory)

**Objective:** Ship the required "Architecture" navigation group: the interactive **Workflow**
visualization (one node per agent/service/gate/outcome with populated detail panels) and the
**Architecture** page (system diagram + ADRs). This makes the five-agent orchestration *visible* — a
scored differentiator ("visible orchestration" + "trust/verification").

**Depends on:** Plan 08. **Primary day:** 3 (parallel with Plan 09).

**Reference templates:** `templates/workflow-visualization/` (`workflowTypes.ts`, `workflowData.ts`,
`WorkflowNode.tsx`, `WorkflowDetailPanel.tsx`, `WorkflowDiagram.tsx`, `WorkflowPage.tsx`) and the
`.github/skills/workflow-visualization/SKILL.md` guide.

---

## 1. Copy the workflow visualization components

- [ ] Copy to `frontend/src/`: `types/workflowTypes.ts`, `data/workflowData.ts`,
      `components/workflow/{WorkflowNode,WorkflowDetailPanel,WorkflowDiagram}.tsx`,
      `pages/WorkflowPage.tsx`
- [ ] Register the route `/workflow` and the sidebar Architecture entry (done in Plan 08 — verify)

## 2. Author the Rewind workflow graph (`workflowData.ts`)

Create a "Decision Reconstruction Pipeline" tab with one node per pipeline element and a populated
detail panel (title, subtitle, description, sourceFiles, responsibilities, dataFlow, technologies,
keyFacts) for each:

- [ ] **Service node — Inquiry API** (`ReconstructionsController`, AG-UI entry)
- [ ] **Gate node — Confirm Scope** (amber/orange; the human-in-the-loop gate)
- [ ] **Agent node — Reconstruction Orchestrator** (gpt-5; owns the sweep + dossier assembly)
- [ ] **Agent node — Vault Forensics** (gpt-5-mini; Azure AI Search hybrid+vector)
- [ ] **Service/agent node — Market Snapshot Reconstructor** (gpt-4.1-mini narration over deterministic
      as-of C#)
- [ ] **Deterministic node — Timeline Assembler** (pure C#, no LLM)
- [ ] **Agent node — Gap & Red-Flag** (gpt-5-mini narration over deterministic C# rules)
- [ ] **Datastore nodes** — Azure AI Search (vault), Cosmos DB (inquiries + immutable audit)
- [ ] **Outcome node — Regulator-Ready Dossier** (two-lane timeline + ranked flags + export)
- [ ] Edges wiring the flow; dashed edges for the red-flag/escalation path
- [ ] Detail panels reference the real backend file paths from Plans 04–06 (traceability)

## 3. Architecture page

Author `frontend/src/pages/ArchitecturePage.tsx`:

- [ ] System architecture diagram (React → Node sidecar → C# AG-UI → Azure Foundry/Search/Cosmos),
      mirroring the findings' topology
- [ ] Component descriptions + the key ADRs: single AG-UI agent + in-process function tools (the
      .NET workflow-streaming workaround), deterministic core (timeline + rules in C#, LLM narrates
      only), observation-date filtering (not point-in-time), no buy/sell framing
- [ ] Note the folded hardening decisions (server-side gate, immutable audit, corrected SDK surface)

## Acceptance criteria

- `/workflow` shows the "Decision Reconstruction Pipeline" tab; every node is clickable with a fully
  populated detail panel (no empty panels)
- Node types are visually distinct (service/agent/gate/datastore/outcome); the human gate is amber
- `/architecture` shows the system diagram + ADRs and matches the implemented topology
- Detail panels cite real backend file paths

## Cut-lines

- The Workflow page is **required** (do not cut). If time is tight, the Architecture page ADR prose can
  be trimmed to bullet points, but keep the diagram + the Workflow tab.
