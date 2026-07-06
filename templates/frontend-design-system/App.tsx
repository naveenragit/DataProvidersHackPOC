// Copy to frontend/src/App.tsx — provider stack + routes mounted inside AppLayout.
//
// Provider order (outer → inner):
//   QueryClientProvider  → TanStack Query server-state cache
//   CopilotKit           → AI copilot; runtimeUrl points at the Node sidecar (proxied at /copilotkit)
//   BrowserRouter        → routing
//
// The CopilotKit runtime is Node-based: the sidecar forwards actions to the C# /api/v1
// endpoints and streams completions from Azure OpenAI. The browser never holds Azure creds.
import { CopilotKit } from '@copilotkit/react-core'
import { CopilotSidebar } from '@copilotkit/react-ui'
import '@copilotkit/react-ui/styles.css'
import { QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from '@/components/layout/AppLayout'
import { queryClient } from '@/lib/queryClient'
import DashboardPage from '@/pages/DashboardPage'
// import WorkflowPage from '@/pages/WorkflowPage'
// import ArchitecturePage from '@/pages/ArchitecturePage'
// import SettingsPage from '@/pages/SettingsPage'

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <CopilotKit runtimeUrl="/copilotkit">
        <BrowserRouter>
          <Routes>
            <Route element={<AppLayout />}>
              <Route index element={<Navigate to="/dashboard" replace />} />
              <Route path="dashboard" element={<DashboardPage />} />
              {/* <Route path="workflow" element={<WorkflowPage />} /> */}
              {/* <Route path="architecture" element={<ArchitecturePage />} /> */}
              {/* <Route path="settings" element={<SettingsPage />} /> */}
            </Route>
          </Routes>
        </BrowserRouter>
        <CopilotSidebar labels={{ title: 'Financial Copilot', initial: 'How can I help with this portfolio?' }} />
      </CopilotKit>
    </QueryClientProvider>
  )
}
