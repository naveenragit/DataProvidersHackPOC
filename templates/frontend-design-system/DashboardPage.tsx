// Copy to frontend/src/pages/DashboardPage.tsx — adapt the data to your domain.
// CANONICAL "look": StatCard grid + two-column shadcn Cards.
//
// Server state note: this sample uses static arrays for clarity. In a real page, fetch with a
// TanStack Query hook (never useEffect) — e.g. `const { data, isPending } = useDashboard()` —
// and render isPending / isError states.
//
// shadcn primitives used here (add once): npx shadcn@latest add card badge
import { Activity, ArrowRight, Shield, TrendingUp, Users } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { PageHeader } from '@/components/ui/PageHeader'
import { StatCard } from '@/components/ui/StatCard'

const STATS = [
  { label: 'Total Clients', value: 128, icon: Users, iconClassName: 'text-primary' },
  { label: 'Assets Under Mgmt', value: '$842.6M', icon: TrendingUp, iconClassName: 'text-success' },
  { label: 'Active Agents', value: 13, icon: Activity, iconClassName: 'text-brand-teal' },
  { label: 'Compliance Flags', value: 2, icon: Shield, iconClassName: 'text-warning' },
]

const RECENT = [
  { name: 'Sarah Chen', meta: 'aggressive · active', aum: '$8.5M' },
  { name: 'Marcus Webb', meta: 'moderate · active', aum: '$3.1M' },
  { name: 'Aisha Khan', meta: 'conservative · prospect', aum: '$1.2M' },
]

const ACTIVITY = [
  { event: 'recommendation_generated', agent: 'RecommendationAgent', time: '14:19' },
  { event: 'portfolio_rebalanced', agent: 'RebalanceAgent', time: '13:58' },
  { event: 'compliance_check_passed', agent: 'ComplianceAgent', time: '13:42' },
]

export default function DashboardPage() {
  return (
    <div className="max-w-7xl space-y-6">
      <PageHeader
        title="Good morning, Advisor"
        subtitle="Financial services platform powered by Azure AI Foundry · Microsoft Agent Framework"
      />

      {/* Stat grid */}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {STATS.map((s) => (
          <StatCard key={s.label} {...s} />
        ))}
      </div>

      {/* Two columns */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Recent clients */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0">
            <CardTitle className="text-sm">Recent Clients</CardTitle>
            <Link to="/clients" className="flex items-center gap-1 text-xs text-primary hover:underline">
              View all <ArrowRight size={11} />
            </Link>
          </CardHeader>
          <CardContent className="space-y-2">
            {RECENT.map((c) => (
              <div key={c.name} className="flex items-center justify-between rounded-lg px-3 py-2 transition-colors hover:bg-muted">
                <div className="flex items-center gap-3">
                  <div className="flex h-8 w-8 items-center justify-center rounded-full border border-primary/30 bg-primary/15 text-xs font-semibold text-primary">
                    {c.name.split(' ').map((n) => n[0]).join('')}
                  </div>
                  <div>
                    <div className="text-sm text-foreground">{c.name}</div>
                    <div className="text-xs capitalize text-muted-foreground">{c.meta}</div>
                  </div>
                </div>
                <div className="text-sm font-medium text-foreground">{c.aum}</div>
              </div>
            ))}
          </CardContent>
        </Card>

        {/* Recent activity */}
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0">
            <CardTitle className="text-sm">Recent Agent Activity</CardTitle>
            <Link to="/audit" className="flex items-center gap-1 text-xs text-primary hover:underline">
              Full audit <ArrowRight size={11} />
            </Link>
          </CardHeader>
          <CardContent className="space-y-2">
            {ACTIVITY.map((a, i) => (
              <div key={i} className="flex items-start gap-3 py-2">
                <div className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-primary" />
                <div className="min-w-0 flex-1">
                  <div className="truncate text-xs font-medium text-foreground">{a.event}</div>
                  <div className="text-[11px] text-muted-foreground">{a.agent}</div>
                </div>
                <div className="shrink-0 text-[11px] text-muted-foreground">{a.time}</div>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
