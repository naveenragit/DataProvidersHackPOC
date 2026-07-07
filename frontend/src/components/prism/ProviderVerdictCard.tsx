// frontend/src/components/prism/ProviderVerdictCard.tsx
// One provider's verdict for an issuer: native letter, notch, the as-of dates behind it, and the
// methodology document it cites. Renders API values verbatim (P2); dates are formatted in UTC to
// stay consistent with the deterministic rule text.
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { formatUtcDate } from '@/lib/prismFormat'
import { PROVIDER_LABELS } from '@/lib/settings'
import type { ProviderVerdictDto } from '@/types/prism'

interface ProviderVerdictCardProps {
  verdict: ProviderVerdictDto
  /** Highlight this card as part of the widest-gap pair. */
  highlighted?: boolean
}

export function ProviderVerdictCard({ verdict, highlighted = false }: ProviderVerdictCardProps) {
  return (
    <Card
      className={highlighted ? 'border-primary/60 ring-1 ring-primary/40' : undefined}
      data-testid="provider-verdict-card"
      data-provider={verdict.provider}
    >
      <CardHeader className="flex-row items-center justify-between space-y-0 pb-2">
        <span className="text-sm font-medium text-muted-foreground">
          {PROVIDER_LABELS[verdict.provider]}
        </span>
        <Badge variant="outline" className="font-mono text-xs">
          notch {verdict.notch}
        </Badge>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="text-2xl font-semibold tracking-tight text-foreground">{verdict.letter}</div>
        <dl className="space-y-1 text-xs text-muted-foreground">
          <div className="flex justify-between gap-2">
            <dt>Rating as-of</dt>
            <dd className="font-mono text-foreground">{formatUtcDate(verdict.asOfDate)}</dd>
          </div>
          <div className="flex justify-between gap-2">
            <dt>Input as-of</dt>
            <dd className="font-mono text-foreground">{formatUtcDate(verdict.inputAsOfDate)}</dd>
          </div>
          <div className="flex justify-between gap-2">
            <dt>Methodology</dt>
            <dd className="truncate font-mono text-foreground" title={verdict.methodologyDocId}>
              {verdict.methodologyDocId}
            </dd>
          </div>
        </dl>
      </CardContent>
    </Card>
  )
}
