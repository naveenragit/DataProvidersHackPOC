# Financial Services Copilot Kit

GitHub Copilot agents, prompts, instructions, and skills for building AI-powered
financial services applications (Capital Markets, Banking, Insurance) on **Microsoft Azure**.

## What's Included

- **4 RPI Agents** — Fin Task Researcher, Planner, Implementor, Reviewer
- **3 Adversarial red-team Agents** — Fin Adversary Architect, Security, Stack Critic
- **4 Instruction sets** — C#/ASP.NET Core, React (shadcn/TanStack/CopilotKit), Azure services, Financial domain
- **5 Prompts** — `/fin-task-research`, `/fin-task-plan`, `/fin-task-implement`, `/fin-task-review`, `/scaffold-financial-app`
- **2 Skills** — Azure Financial Services patterns, Workflow Visualization
- **Templates** — shadcn/ui frontend design system, workflow visualization, and a C# API starter

## Usage

After installing, the agents appear in the Copilot Chat agent picker and the
prompts appear under `/` commands in every workspace.

Start a feature with the RPI workflow:
```
/fin-task-research <your feature>   →  /clear  →  /fin-task-plan
→  /clear  →  /fin-task-implement   →  /clear  →  /fin-task-review
```

All solutions target C# / ASP.NET Core (.NET 9) backends, React 18 (shadcn/ui, TanStack,
CopilotKit) frontends, and Azure Data and AI Services.
