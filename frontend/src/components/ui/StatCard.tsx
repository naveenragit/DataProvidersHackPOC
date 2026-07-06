// frontend/src/components/ui/StatCard.tsx
// KPI tile built on the shadcn Card primitive.
import type { LucideIcon } from 'lucide-react'
import { Card, CardContent } from '@/components/ui/card'
import { cn } from '@/lib/utils'

interface StatCardProps {
  label: string
  value: string | number
  icon: LucideIcon
  iconClassName?: string // e.g. 'text-primary', 'text-success'
  delta?: string // e.g. '+2.4%'
  deltaPositive?: boolean
}

export function StatCard({
  label,
  value,
  icon: Icon,
  iconClassName = 'text-primary',
  delta,
  deltaPositive,
}: StatCardProps) {
  return (
    <Card>
      <CardContent className="flex flex-col gap-1 p-4">
        <div className="mb-2 flex items-center justify-between">
          <div className="text-xs text-muted-foreground">{label}</div>
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-muted">
            <Icon size={15} className={iconClassName} />
          </div>
        </div>
        <div className="text-2xl font-semibold text-foreground">{value}</div>
        {delta && (
          <div className={cn('text-xs font-medium', deltaPositive ? 'text-success' : 'text-danger')}>
            {delta}
          </div>
        )}
      </CardContent>
    </Card>
  )
}
