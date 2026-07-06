# Architectural Plan — Prism

**Authoritative architecture & standards for the Prism project.** Every contributor and every AI
agent working in this repository **must read and follow** these documents when writing code.

> Folder note: created as `architecturalPlan/` (corrected spelling of the requested
> "architecuralPlan"). If you want the literal spelling, rename the folder and this pointer.

---

## Where this fits

| Source | Role |
|---|---|
| `.github/copilot-instructions.md` + `.github/instructions/**` | Org-wide coding standards (auto-applied). **Still authoritative.** |
| **`architecturalPlan/` (this folder)** | **Project-specific architecture decisions for Prism** — the concrete "how we do it here." Resolves ambiguity and adds Prism rules on top of the org standards. |
| `implementationPlan/` | *What* to build, in sequence (work packages). |
| `PRISM-BUILD-PLAN.md` / `HACKATHON-FINDINGS.md` | Concept + validated research. |

**Precedence when in conflict:** org instructions define the baseline; this folder specializes it for
Prism. If a genuine conflict exists, this folder wins for Prism-specific decisions (naming, folder
layout, the deterministic-core rule, error model) — and you should note the deviation.

---

## Contents

| File | Follow it for… |
|---|---|
| [00 — Core principles](00-core-principles.md) | The non-negotiables. Read first. |
| [01 — Naming conventions](01-naming-conventions.md) | Types, files, routes, containers, agents, env vars |
| [02 — Folder structure](02-folder-structure.md) | Where every file goes |
| [03 — Error handling & propagation](03-error-handling-and-propagation.md) | Exceptions, ProblemDetails, agent/connector failures, frontend errors |
| [04 — Agent architecture](04-agent-architecture.md) | How agents are created, grounded, cited; deterministic-vs-LLM split |
| [05 — Configuration & secrets](05-configuration-and-secrets.md) | Options pattern, env vars, no hardcoding |
| [06 — Security & compliance](06-security-and-compliance.md) | Auth, CORS, validation, content safety, PII, audit |
| [07 — Observability & logging](07-observability-and-logging.md) | OpenTelemetry, structured logs, health |
| [08 — Data & persistence](08-data-and-persistence.md) | Cosmos, AI Search, as-of correctness |
| [09 — API & contracts](09-api-and-contracts.md) | REST, versioning, DTOs, AG-UI endpoint |
| [10 — Frontend architecture](10-frontend-architecture.md) | React/shadcn/TanStack/CopilotKit patterns |
| [11 — Testing & quality](11-testing-and-quality.md) | xUnit/vitest, coverage, what to test |
| [TASKS.md](TASKS.md) | **Progress tracker** — check items off as you go |

---

## How an AI agent should use this

1. Before generating code, open the file(s) relevant to the change (e.g. editing an agent → read
   [04](04-agent-architecture.md); adding an endpoint → [09](09-api-and-contracts.md) + [03](03-error-handling-and-propagation.md)).
2. Apply the org `.github/instructions/**` standards **and** these Prism rules.
3. Never violate a [core principle](00-core-principles.md) — especially the **deterministic core**
   and **never say buy/sell/recommend**.
4. Update [TASKS.md](TASKS.md) when you complete a tracked item.
