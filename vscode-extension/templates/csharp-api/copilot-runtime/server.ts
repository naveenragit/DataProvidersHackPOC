// Copy to copilot-runtime/server.ts
//
// CopilotKit Node runtime sidecar. The frontend's <CopilotKit runtimeUrl="/copilotkit" />
// targets this service (proxied by Vite). It:
//   1. Streams completions from Azure OpenAI.
//   2. Exposes CopilotKit actions that forward to the C# ASP.NET Core API (/api/v1/...).
//
// The browser never holds Azure credentials — they live here (or, in production, are provided
// via managed identity / environment variables on the sidecar container).
import { fileURLToPath } from 'node:url'
import express from 'express'
import {
  CopilotRuntime,
  OpenAIAdapter,
  copilotRuntimeNodeHttpEndpoint,
} from '@copilotkit/runtime'
import { AzureOpenAI } from 'openai'

const PORT = Number(process.env.PORT ?? 4000)
const API_BASE = process.env.API_BASE_URL ?? 'http://localhost:8000'

const app = express()

// Azure OpenAI via the openai SDK's AzureOpenAI client, wrapped by CopilotKit's OpenAIAdapter.
// (CopilotKit does not export a dedicated "AzureOpenAIAdapter" — use OpenAIAdapter + AzureOpenAI.)
const azureOpenAI = new AzureOpenAI({
  apiKey: process.env.AZURE_OPENAI_API_KEY,
  endpoint: process.env.AZURE_OPENAI_ENDPOINT!,
  apiVersion: process.env.AZURE_OPENAI_API_VERSION ?? '2024-12-01-preview',
  deployment: process.env.AZURE_OPENAI_DEPLOYMENT!,
})
const serviceAdapter = new OpenAIAdapter({
  openai: azureOpenAI as never,
  model: process.env.AZURE_OPENAI_DEPLOYMENT!,
})

// Actions forward to the C# API. Add one per backend capability the copilot may call.
//
// SECURITY (must-do before real data):
//  - Authenticate the /copilotkit endpoint itself (reject anonymous browser calls).
//  - Forward the END-USER identity (bearer token) to the C# API on every action call so the
//    API enforces per-user, object-level authorization. Do NOT call the API as an
//    over-privileged service with no user context (confused-deputy / broken access control).
//  - Treat ALL LLM-provided action arguments (e.g. portfolioId) as hostile; the API must
//    re-authorize them against the caller's advisorId/clientId, never trust the model.
const runtime = new CopilotRuntime({
  actions: () => [
    {
      name: 'getPortfolio',
      description: 'Fetch a portfolio summary by id from the financial services API.',
      parameters: [{ name: 'portfolioId', type: 'string', required: true }],
      handler: async ({ portfolioId }: { portfolioId: string }) => {
        const res = await fetch(`${API_BASE}/api/v1/portfolios/${portfolioId}`)
        if (!res.ok) throw new Error(`API error ${res.status}`)
        return res.json()
      },
    },
  ],
})

app.use('/copilotkit', (req, res, next) => {
  const handler = copilotRuntimeNodeHttpEndpoint({
    endpoint: '/copilotkit',
    runtime,
    serviceAdapter,
  })
  return handler(req, res, next)
})

app.listen(PORT, () => {
  console.log(`CopilotKit runtime sidecar on http://localhost:${PORT}/copilotkit → API ${API_BASE}`)
})

// Allow `node server.ts` via a loader, or compile with tsx/ts-node.
if (import.meta.url === `file://${fileURLToPath(import.meta.url)}`) {
  // no-op; listen() above starts the server
}
