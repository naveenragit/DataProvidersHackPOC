// Copy to frontend/src/pages/DashboardPage.tsx — adapt the data to your domain.
// This is the CANONICAL "look": stat-card grid + two-column cards with section titles.
// It demonstrates the exact composition of the reference design system.
import { TrendingUp, Users, Shield, Activity, ArrowRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import PageHeader from '@/components/ui/PageHeader'
import StatCard from '@/components/ui/StatCard'

const STATS = [
  { label: 'Total Clients', value: 128, icon: Users, iconColor: 'text-accent', iconBg: 'bg-accent/10' },
  { label: 'Assets Under Mgmt', value: '$842.6M', icon: TrendingUp, iconColor: 'text-green-400', iconBg: 'bg-green-900/20' },
  { label: 'Active Agents', value: 13, icon: Activity, iconColor: 'text-brand-teal', iconBg: 'bg-teal-900/20' },
  { label: 'Compliance Flags', value: 2, icon: Shield, iconColor: 'text-yellow-400', iconBg: 'bg-yellow-900/20' },
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
    <div className="space-y-6 max-w-7xl">
      <PageHeader
        title="Good morning, Advisor"
        subtitle="Financial services platform powered by Azure AI Foundry · MAF orchestration"
      />

      {/* Health banner */}
      <div className="flex items-center gap-3 px-4 py-2.5 bg-surface-50 border border-border rounded-xl text-xs text-gray-400">
        <span className="w-2 h-2 rounded-full bg-green-400 animate-pulse" />
        <span className="text-gray-300 font-medium">System OK</span>
        <span className="text-border">|</span>
        <span>Cosmos DB: <span className="text-green-400">connected</span></span>
        <span className="text-border">|</span>
        <span>AI Search: <span className="text-green-400">connected</span></span>
      </div>

      {/* Stat grid */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
        {STATS.map((s) => (
          <StatCard key={s.label} {...s} />
        ))}
      </div>

      {/* Two columns */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Recent clients */}
        <div className="card">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold text-gray-200">Recent Clients</h3>
            <Link to="/clients" className="text-xs text-accent hover:text-accent-hover flex items-center gap-1">
              View all <ArrowRight size={11} />
            </Link>
          </div>
          <div className="space-y-2">
            {RECENT.map((c) => (
              <div key={c.name} className="flex items-center justify-between py-2 px-3 rounded-lg hover:bg-surface-50 transition-colors group">
                <div className="flex items-center gap-3">
                  <div className="w-8 h-8 rounded-full bg-accent/20 border border-accent/30 flex items-center justify-center text-accent text-xs font-semibold">
                    {c.name.split(' ').map((n) => n[0]).join('')}
                  </div>
                  <div>
                    <div className="text-sm text-gray-200 group-hover:text-white">{c.name}</div>
                    <div className="text-xs text-gray-500 capitalize">{c.meta}</div>
                  </div>
                </div>
                <div className="text-sm font-medium text-gray-300">{c.aum}</div>
              </div>
            ))}
          </div>
        </div>

        {/* Recent activity */}
        <div className="card">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-semibold text-gray-200">Recent Agent Activity</h3>
            <Link to="/audit" className="text-xs text-accent hover:text-accent-hover flex items-center gap-1">
              Full audit <ArrowRight size={11} />
            </Link>
          </div>
          <div className="space-y-2">
            {ACTIVITY.map((a, i) => (
              <div key={i} className="flex items-start gap-3 py-2">
                <div className="w-1.5 h-1.5 rounded-full bg-accent mt-1.5 shrink-0" />
                <div className="flex-1 min-w-0">
                  <div className="text-xs text-gray-300 truncate font-medium">{a.event}</div>
                  <div className="text-[11px] text-gray-600">{a.agent}</div>
                </div>
                <div className="text-[11px] text-gray-600 shrink-0">{a.time}</div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
