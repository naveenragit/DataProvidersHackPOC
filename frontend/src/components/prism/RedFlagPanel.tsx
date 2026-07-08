// frontend/src/components/prism/RedFlagPanel.tsx
// The full list of deterministic red flags for a dossier. Each row shows the severity, the flag
// code, the VERBATIM rule text (P2), and its cited evidence refs, with a button to open the rule
// modal. Empty = honest consensus state (no fabricated content, P1).
import { useState } from 'react'
import { Flag, ShieldCheck } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { sortFlagsBySeverity } from '@/lib/prismFormat'
import { describeEvidenceRef } from '@/lib/evidenceCatalog'
import { RuleModal } from './RuleModal'
import { SEVERITY_BADGE_CLASS, SEVERITY_BADGE_VARIANT, SEVERITY_LABEL } from './redFlagStyles'
import type { RedFlagDto } from '@/types/prism'

interface RedFlagPanelProps {
  flags: RedFlagDto[]
}

export function RedFlagPanel({ flags }: RedFlagPanelProps) {
  const [activeFlag, setActiveFlag] = useState<RedFlagDto | null>(null)
  const sorted = sortFlagsBySeverity(flags)

  return (
    <Card data-testid="red-flag-panel">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <Flag className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
          Red flags
          <span className="text-sm font-normal text-muted-foreground">({flags.length})</span>
        </CardTitle>
      </CardHeader>
      <CardContent>
        {sorted.length === 0 ? (
          <div className="flex items-center gap-2 rounded-md border border-border bg-muted/30 px-3 py-3 text-sm text-muted-foreground">
            <ShieldCheck className="h-4 w-4 flex-shrink-0 text-emerald-500" aria-hidden="true" />
            <span>No red flags — provider ratings reconcile; fully defensible.</span>
          </div>
        ) : (
          <ul className="space-y-3">
            {sorted.map((flag, index) => (
              <li
                key={`${flag.code}-${index}`}
                className="rounded-md border border-border p-3"
                data-testid="red-flag-row"
              >
                <div className="mb-1.5 flex flex-wrap items-center gap-2">
                  <Badge
                    variant={SEVERITY_BADGE_VARIANT[flag.severity]}
                    className={SEVERITY_BADGE_CLASS[flag.severity]}
                  >
                    {SEVERITY_LABEL[flag.severity]}
                  </Badge>
                  <span className="font-mono text-xs text-muted-foreground">{flag.code}</span>
                </div>
                <p className="text-sm text-foreground">{flag.rule}</p>
                {flag.evidenceRefs.length > 0 && (
                  <div className="mt-2 flex flex-wrap gap-1">
                    {flag.evidenceRefs.map((ref) => (
                      <span
                        key={ref}
                        title={ref}
                        className="rounded border border-border bg-muted/40 px-1.5 py-0.5 text-[10px] text-muted-foreground"
                      >
                        {describeEvidenceRef(ref).label}
                      </span>
                    ))}
                  </div>
                )}
                <div className="mt-2">
                  <Button size="sm" variant="outline" onClick={() => setActiveFlag(flag)}>
                    View rule &amp; evidence
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>

      <RuleModal
        flag={activeFlag}
        open={activeFlag !== null}
        onOpenChange={(open) => {
          if (!open) setActiveFlag(null)
        }}
      />
    </Card>
  )
}
