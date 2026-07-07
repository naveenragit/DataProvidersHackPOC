// frontend/src/components/prism/MissingCoverageCard.tsx
// Honest placeholder for a provider that publishes no rating for the issuer. Corroborated by a
// MISSING_COVERAGE red flag from the API — we render an explicit "no coverage" slot rather than
// hiding the gap or fabricating a verdict (P1).
import { SearchX } from 'lucide-react'
import { Card, CardContent, CardHeader } from '@/components/ui/card'
import { PROVIDER_LABELS } from '@/lib/settings'
import type { Provider } from '@/types/prism'

interface MissingCoverageCardProps {
  provider: Provider
}

export function MissingCoverageCard({ provider }: MissingCoverageCardProps) {
  return (
    <Card
      className="border-dashed bg-muted/30"
      data-testid="missing-coverage-card"
      data-provider={provider}
    >
      <CardHeader className="pb-2">
        <span className="text-sm font-medium text-muted-foreground">{PROVIDER_LABELS[provider]}</span>
      </CardHeader>
      <CardContent className="flex items-center gap-2 text-sm text-muted-foreground">
        <SearchX className="h-4 w-4 flex-shrink-0" aria-hidden="true" />
        <span>No rating published</span>
      </CardContent>
    </Card>
  )
}
