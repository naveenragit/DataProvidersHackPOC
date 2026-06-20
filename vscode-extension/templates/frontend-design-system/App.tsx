// Copy to frontend/src/App.tsx — register your routes inside the AppLayout shell.
import { Routes, Route, Navigate } from 'react-router-dom'
import AppLayout from '@/components/layout/AppLayout'
import DashboardPage from '@/pages/DashboardPage'
// import WorkflowPage from '@/pages/WorkflowPage'
// import ArchitecturePage from '@/pages/ArchitecturePage'
// import SettingsPage from '@/pages/SettingsPage'

export default function App() {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<Navigate to="/dashboard" replace />} />
        <Route path="dashboard" element={<DashboardPage />} />
        {/* <Route path="workflow" element={<WorkflowPage />} /> */}
        {/* <Route path="architecture" element={<ArchitecturePage />} /> */}
        {/* <Route path="settings" element={<SettingsPage />} /> */}
      </Route>
    </Routes>
  )
}
