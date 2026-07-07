// frontend/src/components/prism/DivergenceBoard.tsx
// The provider verdict board: the deterministic consensus summary + confidence, then one card per
// provider (a verdict, or an explicit "no coverage" slot). The widest-gap pair is highlighted.
// Every value comes from the dossier (P2); the widest-pair pick is a display derivation.
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { widestDivergence } from '@/lib/prismFormat'
import { ALL_PROVIDERS } from '@/lib/settings'
import type { DossierResponse, Provider } from '@/types/prism'
import { ConfidenceMeter } from './ConfidenceMeter'
import { MissingCoverageCard } from './MissingCoverageCard'
import { ProviderVerdictCard } from './ProviderVerdictCard'

interface DivergenceBoardProps {
  dossier: DossierResponse
}

export function DivergenceBoard({ dossier }: DivergenceBoardProps) {
  const verdictByProvider = new Map(dossier.verdicts.map((v) => [v.provider, v]))

  const widest = widestDivergence(dossier.divergences)
  const highlighted = new Set<Provider>()
  if (widest && Math.abs(widest.notchGap) >= 1) {
    highlighted.add(widest.a)
    highlighted.add(widest.b)
  }

  return (
    <Card data-testid="divergence-board">
      <CardHeader className="flex-row items-center justify-between gap-4 space-y-0 pb-3">
        <div>
          <CardTitle className="text-base">Provider verdicts</CardTitle>
          <p className="mt-1 text-sm text-muted-foreground" data-testid="consensus-summary">
            {dossier.consensusSummary}
          </p>
        </div>
        <ConfidenceMeter confidence={dossier.confidence} />
      </CardHeader>
      <CardContent>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {ALL_PROVIDERS.map((provider) => {
            const verdict = verdictByProvider.get(provider)
            return verdict ? (
              <ProviderVerdictCard
                key={provider}
                verdict={verdict}
                highlighted={highlighted.has(provider)}
              />
            ) : (
              <MissingCoverageCard key={provider} provider={provider} />
            )
          })}
        </div>
      </CardContent>
    </Card>
  )
}
