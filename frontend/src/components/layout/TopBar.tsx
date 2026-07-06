// frontend/src/components/layout/TopBar.tsx
// Top bar (h-16): page title + platform badge + a live status pill.
import { useLocation } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'

const TITLES: Record<string, string> = {
  '/issuers': 'Issuers',
  '/reconciliation': 'Reconciliation',
  '/workflow': 'Workflow',
  '/architecture': 'Architecture',
  '/settings': 'Settings',
}

export function TopBar() {
  const { pathname } = useLocation()
  const base = '/' + pathname.split('/')[1]
  const title = TITLES[base] ?? 'Prism'

  return (
    <header className="flex h-16 shrink-0 items-center justify-between border-b border-border bg-card px-6">
      <div className="flex items-center gap-3">
        <h1 className="text-base font-semibold text-foreground">{title}</h1>
        <Badge variant="outline" className="border-primary/40 text-primary">
          Prism · Azure AI Foundry
        </Badge>
      </div>

      <div className="flex items-center gap-1.5 rounded-full border border-border bg-muted px-3 py-1">
        <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-success" />
        <span className="text-xs text-muted-foreground">Agents online</span>
      </div>
    </header>
  )
}
