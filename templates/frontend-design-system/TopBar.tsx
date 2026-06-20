// Copy to frontend/src/components/layout/TopBar.tsx
// The top bar (h-16) carries the page title + a platform badge and live status pills.
// Map each route base to a human title in TITLES.
import { useLocation } from 'react-router-dom'

const TITLES: Record<string, string> = {
  '/': 'Dashboard',
  '/dashboard': 'Dashboard',
  '/workflow': 'Workflow',
  '/architecture': 'Architecture',
  '/settings': 'Settings',
}

export default function TopBar() {
  const { pathname } = useLocation()
  const base = '/' + pathname.split('/')[1]
  const title = TITLES[base] ?? 'Financial AI'

  return (
    <header className="h-16 flex items-center justify-between px-6 bg-surface-100 border-b border-border shrink-0">
      <div className="flex items-center gap-3">
        <h1 className="text-base font-semibold text-gray-100">{title}</h1>
        <span className="badge-gold">Azure AI Foundry</span>
      </div>

      <div className="flex items-center gap-3">
        <div className="flex items-center gap-1.5 bg-surface-50 border border-border rounded-full px-3 py-1">
          <span className="w-1.5 h-1.5 rounded-full bg-green-400 animate-pulse" />
          <span className="text-xs text-gray-400">Agents online</span>
        </div>
      </div>
    </header>
  )
}
