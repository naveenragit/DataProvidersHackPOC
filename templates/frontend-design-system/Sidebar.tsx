// Copy to frontend/src/components/layout/Sidebar.tsx
// Professional dark sidebar matching the design system (w-60, bg-surface-100).
// Feature group is app-specific; Architecture and Settings groups are REQUIRED.
import { NavLink } from 'react-router-dom'
import type { LucideIcon } from 'lucide-react'
import {
  LayoutDashboard,
  GitBranch,
  Network,
  Settings,
  ChevronRight,
} from 'lucide-react'
import clsx from 'clsx'

interface NavItem {
  to: string
  icon: LucideIcon
  label: string
}

interface NavGroup {
  label: string
  items: NavItem[]
}

// Replace/extend the feature group for your app. Keep Architecture + Settings.
const NAV_GROUPS: NavGroup[] = [
  {
    label: 'Workspace',
    items: [
      { to: '/', icon: LayoutDashboard, label: 'New Assessment' },
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
    items: [
      { to: '/settings', icon: Settings, label: 'Settings' },
    ],
  },
]

export default function Sidebar() {
  return (
    <aside className="flex flex-col w-60 min-h-screen bg-surface-100 border-r border-border shrink-0">
      {/* Brand */}
      <div className="flex items-center gap-3 px-5 h-16 border-b border-border">
        <div className="w-8 h-8 rounded-lg bg-accent flex items-center justify-center text-white font-bold text-sm">
          F
        </div>
        <div>
          <div className="text-sm font-semibold text-gray-100">Financial AI</div>
          <div className="text-[10px] text-gray-500 uppercase tracking-wider">Platform</div>
        </div>
      </div>

      {/* Navigation */}
      <nav className="flex-1 py-3 px-3 overflow-y-auto space-y-1">
        {NAV_GROUPS.map((group) => (
          <div key={group.label} className="pt-3">
            <div className="px-3 pb-1.5">
              <span className="text-[9px] font-bold uppercase tracking-widest text-gray-600">
                {group.label}
              </span>
            </div>
            <div className="space-y-0.5">
              {group.items.map(({ to, icon: Icon, label }) => (
                <NavLink
                  key={to}
                  to={to}
                  end={to === '/'}
                  className={({ isActive }) =>
                    clsx(
                      'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors group',
                      isActive
                        ? 'bg-accent/20 text-accent-hover'
                        : 'text-gray-400 hover:text-gray-100 hover:bg-surface-50',
                    )
                  }
                >
                  {({ isActive }) => (
                    <>
                      <Icon
                        size={16}
                        className={clsx(
                          isActive ? 'text-accent-hover' : 'text-gray-500 group-hover:text-gray-300',
                        )}
                      />
                      <span className="flex-1">{label}</span>
                      {isActive && <ChevronRight size={12} className="text-accent" />}
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
