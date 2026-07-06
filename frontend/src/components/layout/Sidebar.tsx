// frontend/src/components/layout/Sidebar.tsx
// Professional dark sidebar (w-60) for Prism. The Prism feature group plus the REQUIRED
// Architecture and Settings groups. Styled with shadcn tokens + the cn() helper.
import { NavLink } from 'react-router-dom'
import type { LucideIcon } from 'lucide-react'
import { Building2, Scale, GitBranch, Network, Settings, ChevronRight } from 'lucide-react'
import { cn } from '@/lib/utils'

interface NavItem {
  to: string
  icon: LucideIcon
  label: string
}

interface NavGroup {
  label: string
  items: NavItem[]
}

// Prism navigation. Keep the Architecture + Settings groups in every app.
const NAV_GROUPS: NavGroup[] = [
  {
    label: 'Prism',
    items: [
      { to: '/issuers', icon: Building2, label: 'Issuers' },
      { to: '/reconciliation', icon: Scale, label: 'Reconciliation' },
    ],
  },
  {
    label: 'Architecture',
    items: [
      { to: '/workflow', icon: GitBranch, label: 'Workflow' },
      { to: '/architecture', icon: Network, label: 'Architecture' },
    ],
  },
  {
    label: 'Settings',
    items: [{ to: '/settings', icon: Settings, label: 'Settings' }],
  },
]

export function Sidebar() {
  return (
    <aside className="flex w-60 min-h-screen shrink-0 flex-col border-r border-border bg-card">
      {/* Brand */}
      <div className="flex h-16 items-center gap-3 border-b border-border px-5">
        <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-sm font-bold text-primary-foreground">
          P
        </div>
        <div>
          <div className="text-sm font-semibold text-foreground">Prism</div>
          <div className="text-[10px] uppercase tracking-wider text-muted-foreground">
            Reconciliation
          </div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-3">
        {NAV_GROUPS.map((group) => (
          <div key={group.label} className="pt-3">
            <div className="px-3 pb-1.5">
              <span className="text-[9px] font-bold uppercase tracking-widest text-muted-foreground/70">
                {group.label}
              </span>
            </div>
            <div className="space-y-0.5">
              {group.items.map(({ to, icon: Icon, label }) => (
                <NavLink
                  key={to}
                  to={to}
                  className={({ isActive }) =>
                    cn(
                      'group flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
                      isActive
                        ? 'bg-primary/15 text-primary'
                        : 'text-muted-foreground hover:bg-muted hover:text-foreground',
                    )
                  }
                >
                  {({ isActive }) => (
                    <>
                      <Icon
                        size={16}
                        className={cn(
                          isActive
                            ? 'text-primary'
                            : 'text-muted-foreground group-hover:text-foreground',
                        )}
                      />
                      <span className="flex-1">{label}</span>
                      {isActive && <ChevronRight size={12} className="text-primary" />}
                    </>
                  )}
                </NavLink>
              ))}
            </div>
          </div>
        ))}
      </nav>
    </aside>
  )
}
