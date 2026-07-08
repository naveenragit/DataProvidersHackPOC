// frontend/src/pages/ReconciliationPage.tsx
// The Prism reconciliation experience. Reads ?issuer= (deep-linked from the Issuers grid), runs a
// real POST /api/v1/reconciliations via TanStack Query, and renders the returned dossier: verdict
// board, divergence decomposition, deterministic red flags (banner + panel + rule modal), and the
// export affordance. Honest loading/empty/error states only — no fabricated data (P1). The UI never
// computes a notch, gap, or flag; it renders what the API returns (P2).
import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Loader2, RefreshCw } from 'lucide-react'
import { PageHeader } from '@/components/ui/PageHeader'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { DecompositionWaterfall } from '@/components/prism/DecompositionWaterfall'
import { DivergenceBoard } from '@/components/prism/DivergenceBoard'
import { DossierPanel } from '@/components/prism/DossierPanel'
import { MorningstarContextPanel } from '@/components/prism/MorningstarContextPanel'
import { RedFlagBanner } from '@/components/prism/RedFlagBanner'
import { RedFlagPanel } from '@/components/prism/RedFlagPanel'
import { ScopeNotice } from '@/components/prism/ScopeNotice'
import { useIssuers } from '@/hooks/useIssuers'
import { useReconciliationRun } from '@/hooks/useReconciliation'
import { ApiError } from '@/lib/apiClient'
import { widestDivergence } from '@/lib/prismFormat'
import { loadSettings } from '@/lib/settings'

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

export default function ReconciliationPage() {
  const [searchParams] = useSearchParams()
  const issuerId = searchParams.get('issuer')

  const [asOf, setAsOf] = useState<string>(() => loadSettings().defaultAsOf || todayIso())

  // The sweep auto-runs as a query keyed on (issuer, as-of); `refetch()` re-runs it on demand.
  const { data: dossier, isPending, isFetching, isError, error, refetch } =
    useReconciliationRun(issuerId, asOf)

  // Issuer facts (name, sector) for the market-context companion — from the cached issuer cast.
  const { data: issuers } = useIssuers()
  const issuer = useMemo(
    () => issuers?.find((item) => item.issuerId === issuerId) ?? null,
    [issuers, issuerId],
  )

  function handleRerun() {
    if (!issuerId) return
    void refetch()
  }

  const widest = useMemo(
    () => (dossier ? widestDivergence(dossier.divergences) : undefined),
    [dossier],
  )

  return (
    <div className="max-w-5xl space-y-6">
      <PageHeader
        title="Reconciliation"
        subtitle="Explain how provider ratings for an issuer diverge — and why."
      />

      {!issuerId ? (
        <Card>
          <CardHeader>
            <CardTitle>Pick an issuer to reconcile</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3 text-sm text-muted-foreground">
            <p>Choose a corporate-bond issuer to run a reconciliation sweep across its providers.</p>
            <Button asChild variant="outline">
              <Link to="/issuers">Go to Issuers</Link>
            </Button>
          </CardContent>
        </Card>
      ) : (
        <>
          {/* Issuer header + as-of control */}
          <Card>
            <CardContent className="flex flex-wrap items-end justify-between gap-4 p-4">
              <div>
                <span className="text-xs uppercase tracking-wide text-muted-foreground">Issuer</span>
                <p className="text-lg text-foreground">{issuer?.legalName ?? issuerId}</p>
                {issuer && (
                  <p className="font-mono text-xs text-muted-foreground">
                    {issuer.ticker} · {issuer.sector}
                  </p>
                )}
              </div>
              <div className="flex items-end gap-3">
                <div className="space-y-1">
                  <Label htmlFor="asof">As-of date</Label>
                  <Input
                    id="asof"
                    type="date"
                    value={asOf}
                    max={todayIso()}
                    onChange={(e) => setAsOf(e.target.value)}
                    className="w-44"
                  />
                </div>
                <Button
                  variant="outline"
                  onClick={handleRerun}
                  disabled={isFetching}
                  className="gap-2"
                >
                  <RefreshCw className={`h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} aria-hidden="true" />
                  Re-run
                </Button>
              </div>
            </CardContent>
          </Card>

          <ScopeNotice />

          {isPending ? (
            <Card>
              <CardContent className="flex items-center gap-3 p-8 text-muted-foreground">
                <Loader2 className="h-5 w-5 animate-spin text-primary" aria-hidden="true" />
                <span className="text-sm">
                  Reconciling provider ratings for <span className="font-mono">{issuerId}</span> as of{' '}
                  {asOf}…
                </span>
              </CardContent>
            </Card>
          ) : isError ? (
            <Card className="border-destructive/40">
              <CardHeader>
                <CardTitle className="text-destructive">Reconciliation failed</CardTitle>
              </CardHeader>
              <CardContent className="space-y-2">
                <p className="text-sm text-muted-foreground">
                  The reconciliation ran against the real{' '}
                  <span className="font-mono">/api/v1/reconciliations</span> endpoint and did not
                  complete. Nothing is shown rather than fabricated results.
                </p>
                <p className="rounded-md border border-destructive/30 bg-destructive/10 px-3 py-2 font-mono text-xs text-destructive">
                  Error code: {error instanceof ApiError ? error.code : 'UNKNOWN'}
                </p>
                <Button variant="outline" onClick={handleRerun} className="gap-2">
                  <RefreshCw className="h-4 w-4" aria-hidden="true" />
                  Try again
                </Button>
              </CardContent>
            </Card>
          ) : dossier ? (
            <>
              <RedFlagBanner flags={dossier.flags} />
              <DivergenceBoard dossier={dossier} />
              {widest && <DecompositionWaterfall divergence={widest} />}
              <RedFlagPanel flags={dossier.flags} />
              <DossierPanel dossier={dossier} issuerName={issuer?.legalName} />
              <MorningstarContextPanel
                key={dossier.issuerId}
                issuerId={dossier.issuerId}
                issuerName={issuer?.legalName}
                sector={issuer?.sector}
                ticker={issuer?.ticker}
              />
            </>
          ) : null}
        </>
      )}
    </div>
  )
}
