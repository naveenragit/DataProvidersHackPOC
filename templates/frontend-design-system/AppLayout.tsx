// Copy to frontend/src/components/layout/AppLayout.tsx
// Two-column app shell: fixed dark sidebar + (top bar over scrollable content).
// Matches the reference design: full-height, no body scroll, content scrolls in <main>.
import { Outlet } from 'react-router-dom'
import Sidebar from './Sidebar'
import TopBar from './TopBar'

export default function AppLayout() {
  return (
    <div className="flex h-screen overflow-hidden bg-surface text-gray-100">
      <Sidebar />
      <div className="flex flex-col flex-1 min-w-0 overflow-hidden">
        <TopBar />
        <main className="flex-1 overflow-y-auto p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
