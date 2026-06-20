# Extending the Kit: Think Big

This kit is a **foundation, not a ceiling.** It encodes a disciplined way of working — but
the real value comes when your team treats it as a living platform and pushes it further.

This document is a menu of ideas, organized from "quick wins" to "ambitious." Pick what
excites you. The best extensions usually come from a frustration you felt yesterday.

> **Guiding principle:** Every time someone solves a problem well, capture it as a component
> so the whole team inherits the solution. The kit should get smarter every week.

---

## How to think about extending

Ask three questions about any recurring task:

1. **Is this a standard we keep re-explaining?** → Write an **instruction**.
2. **Is this a workflow we keep re-typing?** → Write a **prompt**.
3. **Is this a specialized job with its own judgment?** → Write an **agent**.
4. **Is this deep knowledge we only sometimes need?** → Write a **skill**.

If you can answer "yes" to any, you have your next contribution.

---

## Quick wins (hours, not days)

### New instructions

| Idea | `applyTo` | Why |
|---|---|---|
| **Testing standards** | `**/*.test.ts, **/test_*.py` | Enforce pytest/vitest patterns, coverage expectations, naming |
| **Bicep / IaC standards** | `**/*.bicep` | Azure Verified Modules, naming conventions, tagging policy |
| **Accessibility (a11y)** | `**/*.tsx` | WCAG AA, ARIA roles, keyboard nav for financial dashboards |
| **API versioning** | `**/routes/**/*.py` | Consistent `/v1/` routing, deprecation headers |
| **Logging & observability** | `**/*.py` | Structured logs, correlation IDs, no PII in traces |
| **SQL / Cosmos query standards** | `**/*.py` | Partition key discipline, RU budgeting, parameterized queries |

### New prompts

| Command idea | What it does |
|---|---|
| `/fin-generate-tests` | Generate a full test suite for the selected module |
| `/fin-create-adr` | Capture an architecture decision record from a discussion |
| `/fin-demo-data` | Generate realistic (synthetic, non-PII) financial sample data |
| `/fin-add-agent` | Scaffold a new Azure AI Foundry agent with the right model + tools |
| `/fin-security-scan` | Walk OWASP Top 10 + financial compliance against the diff |
| `/fin-explain-flow` | Generate a plain-English narrative of a workflow for stakeholders |

---

## Medium lifts (a day or two)

### New specialist agents

The RPI pipeline is just the start. Add specialists that match your team's real roles:

| Agent idea | Responsibility |
|---|---|
| **Compliance Reviewer** | Audits a feature against MiFID II / Basel / Solvency II requirements |
| **Data Modeler** | Designs Cosmos containers, partition strategy, and AI Search indexes |
| **Security Auditor** | Threat-models the change; checks auth, secrets, PII flow, injection |
| **Cost Optimizer** | Reviews Azure resource choices for RU/token/compute efficiency |
| **Demo Builder** | Turns a feature into a guided, click-through demo script |
| **Migration Specialist** | Plans lift-and-shift from legacy stacks (e.g., on-prem, AWS) to Azure |
| **Performance Engineer** | Profiles hot paths, recommends caching and async patterns |

Each can be wired into the existing pipeline via `handoffs` — e.g., the Reviewer could hand
off to the Compliance Reviewer for regulated features.

### New skills

| Skill idea | Packages |
|---|---|
| **Bicep IaC patterns** | Azure Verified Modules, RBAC, managed identity, networking |
| **Power BI / embedded analytics** | Embedding dashboards, row-level security, dataset design |
| **Real-time streaming** | Event Hubs / Web PubSub patterns for live market data |
| **Document Intelligence pipelines** | Extracting data from statements, contracts, claims forms |
| **Evaluation & red-teaming** | Foundry eval datasets, graders, groundedness checks |
| **Domain-specific calculators** | VaR, duration, DSCR, loss-ratio reference implementations |

---

## Ambitious (worth a sprint)

### Golden-path scaffolds per domain

Today `/scaffold-financial-app` is generic. Build **three opinionated scaffolds** — one each
for Capital Markets, Banking, and Insurance — that drop in a working vertical slice:
a sample agent, a Cosmos schema, a workflow tab, and a demo UI. New projects start at 60%.

### Wire in MCP servers and live tools

Give agents real tool access via Model Context Protocol:

- **Azure MCP** — query live resources, RBAC, costs during planning
- **GitHub / ADO MCP** — auto-create work items and PRs from the plan
- **Market data MCP** — let a research agent pull live (or sandbox) pricing
- **Database MCP** — let agents inspect real schemas before modeling

This turns agents from "advisors" into "operators."

### Continuous evaluation loop

Add a Foundry-based eval harness so changes to agents/prompts are **measured**, not guessed:

- Curate a golden dataset of representative financial tasks
- Run prompts/agents against it on every kit change
- Track groundedness, compliance adherence, and code-correctness over time
- Gate kit releases on eval scores

### Team playbooks and orchestration

- A **PRD → backlog** agent chain that turns a product brief into ADO/GitHub work items
- A **multi-agent "war room"** where Architect, Security, and Cost agents debate a design
- A **release readiness** prompt that aggregates review, security, and compliance into a go/no-go

### Self-improving kit

- A monthly `/kit-retro` prompt that scans recent `.copilot-tracking/` artifacts and proposes
  new instructions/skills based on what the team kept doing manually.
- Memory-backed agents that remember team-specific conventions across sessions.

---

## Distribution and governance

As the kit grows, treat it like a product:

- **Version it** — bump the extension version, keep a changelog
- **Publish it** — internal extension gallery or VS Code Marketplace (private)
- **Review contributions** — PRs to the kit get the same rigor as production code
- **Measure adoption** — which prompts/agents get used; retire what doesn't
- **Onboard with it** — new hires install the extension and inherit the whole team's expertise

---

## A challenge for your team

Run this experiment for two weeks: **every time someone says "ugh, I always have to..." —
stop and turn it into a component.** Instruction, prompt, agent, or skill. By the end you'll
have a kit that fits your team like a glove, and a culture where improving the tools *is* the work.

The teams that win with Copilot aren't the ones who prompt the cleverest. They're the ones
who **compound** — capturing every good pattern so the whole team levels up at once.

---

## Where to go next

- **Quick start** → [../README.md](../README.md)
- **Concepts (HVE Core)** → [AI Artifacts Architecture](https://github.com/microsoft/hve-core/blob/main/docs/architecture/ai-artifacts.md)
