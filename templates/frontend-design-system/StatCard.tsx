// Copy to frontend/src/components/ui/StatCard.tsx
// KPI tile used in dashboard grids. Renders label + big value with an accent icon chip.
import type { LucideIcon } from 'lucide-react'

interface StatCardProps {
  label: string
  value: string | number
  icon: LucideIcon
  iconColor?: string   // e.g. 'text-accent', 'text-green-400'
  iconBg?: string      // e.g. 'bg-accent/10', 'bg-green-900/20'
  delta?: string       // e.g. '+2.4%'
  deltaPositive?: boolean
}

export default function StatCard({
  label, value, icon: Icon,
  iconColor = 'text-accent', iconBg = 'bg-accent/10',
  delta, deltaPositive,
}: StatCardProps) {
  return (
    <div className="stat-card">
      <div className="flex items-center justify-between mb-2">
        <div className="stat-label">{label}</div>
        <div className={`w-8 h-8 rounded-lg ${iconBg} flex items-center justify-center`}>
          <Icon size={15} className={iconColor} />
        </div>
      </div>
      <div className="stat-value">{value}</div>
      {delta && (
        <div className={`stat-delta ${deltaPositive ? 'text-green-400' : 'text-red-400'}`}>
          {delta}
        </div>
      )}
    </div>
  )
}
