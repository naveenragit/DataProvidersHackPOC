// frontend/src/pages/ReconciliationPage.tsx
// Placeholder shell — the full streaming reconciliation experience lands in package 10.
// Reads the optional ?issuer= query param so the Issuers grid can deep-link here.
import { useSearchParams } from 'react-router-dom'
import { Scale } from 'lucide-react'
import { PageHeader } from '@/components/ui/PageHeader'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

export default function ReconciliationPage() {
  const [searchParams] = useSearchParams()
  const issuerId = searchParams.get('issuer')

  return (
    <div className="max-w-4xl space-y-6">
      <PageHeader
        title="Reconciliation"
        subtitle="Explain how provider ratings for an issuer diverge — and why."
      />

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Scale className="h-4 w-4 text-primary" />
            Reconciliation workspace
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <p className="text-sm text-muted-foreground">
            This is where the Prism reconciliation experience mounts. It walks a provider-by-provider
            comparison, decomposes each notch gap, and surfaces deterministic red flags with cited
            evidence. The interactive pipeline arrives in the Prism UI package.
          </p>

          {issuerId ? (
            <div className="rounded-md border border-border bg-muted/40 px-3 py-2 text-sm">
              <span className="text-muted-foreground">Selected issuer: </span>
              <span className="font-mono text-foreground">{issuerId}</span>
              <span className="text-muted-foreground"> — run pipeline (coming in the Prism UI package).</span>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              Pick an issuer on the Issuers page to begin a reconciliation.
            </p>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
