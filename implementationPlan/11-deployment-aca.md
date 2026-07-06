# 11 — Deployment to Azure Container Apps

**Purpose:** get Prism running on Azure (constraint: app + DBs on Azure; models from Foundry). ACA is
the right home for the two SSE-streaming servers.

**Depends on:** backend + frontend feature-complete enough to demo. Target: Day 4.

> Avoid App Service for the streaming servers — Windows plans buffer SSE via IIS/ARR
> (HACKATHON-FINDINGS §7). ACA/Envoy streams cleanly.

---

## A. Topology

| Component | Azure resource | Notes |
|---|---|---|
| ASP.NET API (AG-UI + REST) | Container App | `minReplicas: 1` (avoid cold start during judging); ~20s heartbeat (LB idles at 230–240s) |
| Node CopilotKit runtime | Container App | same ACA environment as the API |
| Frontend (`dist/`) | Static Web App (Free) or a container | auto-HTTPS |
| Foundry project + models | Azure AI Foundry | GPT-5 registered (day 0) |
| Vault corpus | Azure AI Search Basic | ~$2.50/day |
| Persistence | Cosmos DB free tier | 1000 RU/s + 25 GB |
| Telemetry | App Insights | tracing on (judging asset) |

---

## B. IaC (reuse an azd skeleton)

Base on `Azure-Samples/ai-chat-quickstart-csharp` (ACA + ACR + RBAC, lightest C#). Add: AI Search,
Cosmos (free-tier flag at creation), the second container app (Node runtime), and Foundry model
Bicep. Provision into the region chosen on day 0.

Managed identity + RBAC (use GUIDs in Bicep; **propagation takes ~10 min** — "works locally, 403 in
cloud" usually means wait, not debug):

| Client | Role | GUID |
|---|---|---|
| `AIProjectClient` | **Foundry User** | `53ca6127-db72-4b80-b1b0-d745d6d5456d` |
| `AzureOpenAIClient` (Node sidecar) | **Cognitive Services OpenAI User** | (assign by name→GUID) |
| CosmosClient | Cosmos DB Built-in Data Contributor | data-plane role |
| SearchClient | Search Index Data Reader/Contributor | reader at query time |

`DefaultAzureCredential` promotes local→managed identity with no code change; set `AZURE_CLIENT_ID`
for a user-assigned identity on the container apps.

---

## C. Config & secrets

- No secrets in images. Container Apps env vars / secrets provide `Azure__*`, `Prism__*`, and the
  Node sidecar's `AZURE_OPENAI_*`, `PRISM_AGUI_URL` (internal ACA URL of the API), `API_BASE_URL`.
- CORS: set the API's `Cors:Origins` to the deployed frontend origin (never `*`).
- `FredApiKey` and `SecUserAgent` as env vars.

---

## D. Deploy loop
`azd up` for first provision+deploy (~half a day from template); then `azd deploy <service>` (1–3 min)
per service on changes.

---

## Acceptance for this package
- [ ] `azd up` provisions all resources green in the chosen region.
- [ ] API health endpoint reachable at the ACA URL; SSE from `/prism` streams (no buffering).
- [ ] Managed identity works (no keys); RBAC roles assigned via GUID; 403s resolved by waiting ~10 min.
- [ ] Frontend served over HTTPS talks to the API + runtime.
- [ ] End-to-end NordStar demo runs against the deployed stack.
- [ ] Cost inside the $200 credit.

**Cut line:** if deploy fights back near the deadline, demo localhost against **live Foundry** — the
models still come from Foundry, satisfying the hard constraint.
