// frontend/src/components/prism/RuleModal.tsx
// The "we didn't rig it" surface. Shows a red flag's VERBATIM deterministic rule text (P2) plus its
// cited evidence rows (e.g. the Msci rating card + the EDGAR filing that trip STALE_INPUT). No
// narrative is invented — when the narrator field is empty we say so (P1).
import { Badge } from '@/components/ui/badge'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { DeferredNarrationNote } from './DeferredNarrationNote'
import { SEVERITY_BADGE_CLASS, SEVERITY_BADGE_VARIANT, SEVERITY_LABEL } from './redFlagStyles'
import type { RedFlagDto } from '@/types/prism'

interface RuleModalProps {
  flag: RedFlagDto | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function RuleModal({ flag, open, onOpenChange }: RuleModalProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        {flag ? (
          <>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Badge
                  variant={SEVERITY_BADGE_VARIANT[flag.severity]}
                  className={SEVERITY_BADGE_CLASS[flag.severity]}
                >
                  {SEVERITY_LABEL[flag.severity]}
                </Badge>
                <span className="font-mono text-sm">{flag.code}</span>
              </DialogTitle>
              <DialogDescription>
                Deterministic rule and cited evidence — reproduced exactly as the reconciliation
                engine emitted them.
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-4">
              <section>
                <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Rule
                </h3>
                <pre className="whitespace-pre-wrap rounded-md border border-border bg-muted/40 p-3 font-mono text-sm text-foreground">
                  {flag.rule}
                </pre>
              </section>

              <section>
                <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  Evidence
                </h3>
                {flag.evidenceRefs.length > 0 ? (
                  <ul className="space-y-1">
                    {flag.evidenceRefs.map((ref) => (
                      <li
                        key={ref}
                        className="rounded-md border border-border bg-background px-3 py-2 font-mono text-xs text-foreground"
                      >
                        {ref}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-xs text-muted-foreground">No evidence references on this flag.</p>
                )}
              </section>

              {flag.narrative ? (
                <section>
                  <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                    Narrative
                  </h3>
                  <p className="text-sm text-foreground">{flag.narrative}</p>
                </section>
              ) : (
                <DeferredNarrationNote subject="this red flag" />
              )}
            </div>
          </>
        ) : null}
      </DialogContent>
    </Dialog>
  )
}
