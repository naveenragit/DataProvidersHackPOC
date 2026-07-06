# 01 — Day 0: Azure Gates & Scaffold

**Purpose:** clear the external-lead-time gates and stand up a running skeleton (all three
processes) against **live Foundry** before feature work. Target: half a day.

**Depends on:** 00. **Blocks:** everything.

---

## A. External gates (do these first — they have lead time)

- [ ] **GPT-5 access** registered at aka.ms/openai/gpt-5. Keep **GPT-4.1** as fallback model.
- [ ] `az login` for **every** teammate.
- [ ] **RBAC** on the Foundry project (see package 11 for GUIDs):
      `Foundry User` for the Projects client, `Cognitive Services OpenAI User` for OpenAI.
- [ ] Pick a **region** that has Responses API + File Search + Code Interpreter (avoid Italy North /
      Brazil South — File Search absent). Provision Foundry, AI Search **Basic**, Cosmos **free tier**,
      Azure OpenAI (for the Node sidecar) there.
- [ ] Request a **TPM quota bump** (multi-agent fan-out hits 429s fast).
- [ ] **Pin** prerelease AG-UI NuGet versions (package 07) the moment you add them.

---

## B. Scaffold the repo from the in-repo accelerator

Copy templates into working folders (PowerShell):

```powershell
# Backend
Copy-Item templates/csharp-api/* backend/ -Recurse
# Frontend design system + workflow viz
New-Item frontend/src/components/workflow -ItemType Directory -Force
Copy-Item templates/frontend-design-system/* frontend/ -Recurse
Copy-Item templates/workflow-visualization/* frontend/src/components/workflow/
```

Then:
- Rename the API root namespace if desired (kit ships `FinancialServices.Api`; keeping it is fine).
- Wire the frontend template files into a real Vite app structure (`src/`, `components.json`,
  `tailwind.config.js`, `index.css`, `main.tsx`). The template files are drop-ins with `// Copy to ...`
  headers — follow each header's target path.

### Root run scripts (create these)
- `run-backend.bat` → `dotnet restore` then `dotnet run --project backend/FinancialServices.Api` (port 8000)
- `run-copilot-runtime.bat` → `npm install` (if needed) + start Node sidecar (port 4000)
- `run-frontend.bat` → `npm install` (if needed) + `npm run dev` in `frontend/` (port 5173)

---

## C. Minimal env + smoke test

1. Fill `.env` / user-secrets with the real Foundry, OpenAI, Cosmos, Search endpoints from step A.
2. Create the Cosmos database `prism` and containers `rating_reconciliations` (pk `/issuerId`) and
   `audit_events` (pk `/issuerId`). (Detailed in package 08.)
3. Create an empty AI Search index placeholder (real schema in package 03).
4. Define **one** trivial Foundry Prompt Agent (e.g. `PingAgent`) and call it from a throwaway
   endpoint to prove the Responses-API-v2 path + Entra auth works.

> **SDK caveat (accelerator note):** the Foundry agent retrieval surface lives in
> `Microsoft.Agents.AI.AzureAI`; verify `GetAIAgent` / `RunAsync` names against the installed
> package version before building more (package 06).

### azd skeleton (optional but recommended)
Clone `Azure-Samples/ai-chat-quickstart-csharp` (lightest C#: ACA + ACR + RBAC) as the deploy
reference for package 11, and the CopilotKit monorepo `examples/integrations/ms-agent-framework-dotnet`
as the AG-UI reference for package 07. Do a hello-world `azd up` to confirm your subscription/region.

---

## Acceptance for this package
- [ ] All external gates checked.
- [ ] `run-backend.bat`, `run-copilot-runtime.bat`, `run-frontend.bat` each start their process.
- [ ] `GET http://localhost:8000/api/health` returns 200.
- [ ] Frontend shell loads at http://localhost:5173 with the sidebar visible.
- [ ] `PingAgent` returns a completion from **live Foundry** (proves auth + region + model access).
- [ ] Costs confirmed inside the $200 credit (ACA warm + AI Search ~$2.50/day + tokens).
