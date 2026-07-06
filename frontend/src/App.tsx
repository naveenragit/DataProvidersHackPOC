// frontend/src/App.tsx — provider stack + routes mounted inside AppLayout.
//
// Provider order (outer → inner):
//   QueryClientProvider  → TanStack Query server-state cache
//   CopilotKit           → AI copilot; runtimeUrl is env-aware (VITE_COPILOT_URL, else /copilotkit)
//   TooltipProvider      → single Radix tooltip provider ancestor (required by @/components/ui/tooltip)
//   BrowserRouter        → routing
//   ErrorBoundary        → top-level render/runtime crash guard around <Routes>
//
// The CopilotKit runtime is Node-based: the sidecar (package 07) forwards actions to the C#
// /api/v1 endpoints and streams completions from Azure OpenAI. The browser never holds Azure creds.
import { CopilotKit } from '@copilotkit/react-core'
import { CopilotSidebar } from '@copilotkit/react-ui'
import '@copilotkit/react-ui/styles.css'
import { QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { ErrorBoundary } from '@/components/ErrorBoundary'
import { AppLayout } from '@/components/layout/AppLayout'
import { TooltipProvider } from '@/components/ui/tooltip'
import { queryClient } from '@/lib/queryClient'
import IssuersPage from '@/pages/IssuersPage'
import ReconciliationPage from '@/pages/ReconciliationPage'
import WorkflowPage from '@/pages/WorkflowPage'
import ArchitecturePage from '@/pages/ArchitecturePage'
import SettingsPage from '@/pages/SettingsPage'

// Env-aware in prod; falls back to the Vite-proxied `/copilotkit` path in dev.
const COPILOT_RUNTIME_URL = import.meta.env.VITE_COPILOT_URL ?? '/copilotkit'

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <CopilotKit runtimeUrl={COPILOT_RUNTIME_URL}>
        {/* Radix tooltips throw without a provider ancestor — mount one once, here. */}
        <TooltipProvider delayDuration={200}>
          <BrowserRouter>
            <ErrorBoundary>
              <Routes>
                <Route element={<AppLayout />}>
                  <Route index element={<Navigate to="/issuers" replace />} />
                  <Route path="issuers" element={<IssuersPage />} />
                  <Route path="reconciliation" element={<ReconciliationPage />} />
                  <Route path="workflow" element={<WorkflowPage />} />
                  <Route path="architecture" element={<ArchitecturePage />} />
                  <Route path="settings" element={<SettingsPage />} />
                </Route>
              </Routes>
            </ErrorBoundary>
          </BrowserRouter>
          <CopilotSidebar
            labels={{
              title: 'Prism Copilot',
              initial: 'Ask about a rating divergence or red flag.',
            }}
          />
        </TooltipProvider>
      </CopilotKit>
    </QueryClientProvider>
  )
}
