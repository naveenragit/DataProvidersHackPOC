// frontend/src/components/prism/DossierPanel.tsx
// Assembled dossier summary + export. Export opens the REAL server-rendered printable HTML
// (GET /api/v1/reconciliations/{id}/export, package 08) in a new tab, where the browser prints to
// PDF. No fabricated content — every value is from the dossier (P1/P2).
import { FileDown, FileText } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatUtcDate } from '@/lib/prismFormat'
import type { DossierResponse } from '@/types/prism'

interface DossierPanelProps {
  dossier: DossierResponse
}

export function DossierPanel({ dossier }: DossierPanelProps) {
  function handleExport() {
    const url = `/api/v1/reconciliations/${encodeURIComponent(dossier.id)}/export`
    window.open(url, '_blank', 'noopener,noreferrer')
  }

  return (
    <Card data-testid="dossier-panel">
      <CardHeader className="pb-3">
        <CardTitle className="flex items-center gap-2 text-base">
          <FileText className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
          Reconciliation dossier
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-4">
          <div>
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">Issuer</dt>
            <dd className="font-mono text-foreground">{dossier.issuerId}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">As-of</dt>
            <dd className="font-mono text-foreground">{formatUtcDate(dossier.asOf)}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">Verdicts</dt>
            <dd className="text-foreground">{dossier.verdicts.length}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-muted-foreground">Red flags</dt>
            <dd className="text-foreground">{dossier.flags.length}</dd>
          </div>
        </dl>

        <div className="rounded-md border border-border bg-muted/30 px-3 py-2">
          <span className="text-xs uppercase tracking-wide text-muted-foreground">Dossier id</span>
          <p className="break-all font-mono text-xs text-foreground">{dossier.id}</p>
        </div>

        <div className="flex items-center gap-2">
          <Button onClick={handleExport} className="gap-2">
            <FileDown className="h-4 w-4" aria-hidden="true" />
            Export dossier
          </Button>
          <span className="text-xs text-muted-foreground">
            Opens the printable dossier — use your browser&rsquo;s Print to save as PDF.
          </span>
        </div>
      </CardContent>
    </Card>
  )
}
