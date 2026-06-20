# Financial Services Copilot Kit

GitHub Copilot agents, prompts, instructions, and skills for building AI-powered
financial services applications (Capital Markets, Banking, Insurance) on **Microsoft Azure**.

## What's Included

- **4 RPI Agents** — Fin Task Researcher, Planner, Implementor, Reviewer
- **4 Instruction sets** — Python/FastAPI, React/TypeScript, Azure services, Financial domain
- **5 Prompts** — `/fin-task-research`, `/fin-task-plan`, `/fin-task-implement`, `/fin-task-review`, `/scaffold-financial-app`
- **2 Skills** — Azure Financial Services patterns, Workflow Visualization
- **Workflow visualization templates** — Ready-to-use React components

## Usage

After installing, the agents appear in the Copilot Chat agent picker and the
prompts appear under `/` commands in every workspace.

Start a feature with the RPI workflow:
```
/fin-task-research <your feature>   →  /clear  →  /fin-task-plan
→  /clear  →  /fin-task-implement   →  /clear  →  /fin-task-review
```

All solutions target Python + FastAPI backends, React + TypeScript frontends,
and Azure Data and AI Services.
