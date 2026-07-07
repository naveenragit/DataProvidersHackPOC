// frontend/src/components/prism/DecompositionWaterfall.tsx
// Renders one provider pair's notch-gap decomposition. The gap and its Weighting / Input /
// MethodologyAdjustment buckets are computed by the backend (P2) — here we only choose the honest
// visual treatment:
//   • gap == 0            → a "full consensus" state (no chart).
//   • residual-dominated  → a "methodology-driven divergence" framing (the whole gap is a
//                           methodology residual) instead of a misleading rich waterfall.
//   • otherwise           → a labelled recharts bar chart (≤4 bars: the three buckets + net gap).
// A text legend of `bucket: notches` always renders (accessibility + deterministic reference).
import { Bar, BarChart, Cell, ReferenceLine, ResponsiveContainer, XAxis, YAxis } from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { bucketOf, isResidualDominated, residualShare } from '@/lib/prismFormat'
import { PROVIDER_LABELS } from '@/lib/settings'
import type { Bucket, PairDivergenceDto } from '@/types/prism'
import { DeferredNarrationNote } from './DeferredNarrationNote'

interface DecompositionWaterfallProps {
  divergence: PairDivergenceDto
}

const BUCKET_ORDER: Bucket[] = ['Weighting', 'Input', 'MethodologyAdjustment']

const BUCKET_LABEL: Record<Bucket, string> = {
  Weighting: 'Weighting',
  Input: 'Input',
  MethodologyAdjustment: 'Methodology',
}

const BUCKET_COLOR: Record<Bucket, string> = {
  Weighting: '#6366f1',
  Input: '#0ea5e9',
  MethodologyAdjustment: '#f59e0b',
}

const NET_COLOR = '#94a3b8'

export function DecompositionWaterfall({ divergence }: DecompositionWaterfallProps) {
  const aLabel = PROVIDER_LABELS[divergence.a]
  const bLabel = PROVIDER_LABELS[divergence.b]

  const legend = (
    <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-xs" data-testid="waterfall-legend">
      {BUCKET_ORDER.map((bucket) => {
        const notches = bucketOf(divergence, bucket)?.notches ?? 0
        return (
          <li key={bucket} className="flex items-center gap-1.5">
            <span
              className="inline-block h-2 w-2 rounded-sm"
              style={{ backgroundColor: BUCKET_COLOR[bucket] }}
              aria-hidden="true"
            />
            <span className="text-muted-foreground">{BUCKET_LABEL[bucket]}:</span>
            <span className="font-mono text-foreground">{notches}</span>
          </li>
        )
      })}
    </ul>
  )

  return (
    <Card data-testid="decomposition-waterfall">
      <CardHeader className="pb-3">
        <CardTitle className="text-base">
          Divergence decomposition
          <span className="ml-2 text-sm font-normal text-muted-foreground">
            {aLabel} vs {bLabel} · {Math.abs(divergence.notchGap)}-notch gap
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent>
        {divergence.notchGap === 0 ? (
          <p
            className="rounded-md border border-border bg-muted/30 px-3 py-3 text-sm text-muted-foreground"
            data-testid="waterfall-consensus"
          >
            No divergence — {aLabel} and {bLabel} agree (0-notch gap). Nothing to decompose.
          </p>
        ) : isResidualDominated(divergence) ? (
          <div data-testid="waterfall-residual" className="space-y-3">
            <p className="text-sm text-foreground">
              This {Math.abs(divergence.notchGap)}-notch gap is a{' '}
              <span className="font-medium text-amber-500">methodology-driven divergence</span>:{' '}
              {Math.round(residualShare(divergence) * 100)}% of it is a methodology residual, not
              mechanically attributable to factor weighting or input-timing differences.
            </p>
            <div
              className="h-2 w-full overflow-hidden rounded-full bg-muted"
              role="img"
              aria-label={`Methodology residual accounts for ${Math.round(residualShare(divergence) * 100)} percent of the gap`}
            >
              <div
                className="h-full rounded-full bg-amber-500"
                style={{ width: `${Math.min(100, Math.round(residualShare(divergence) * 100))}%` }}
              />
            </div>
            <p className="text-xs text-muted-foreground">
              A full weighting/input waterfall is only meaningful when providers expose comparable
              factor weights and vintages. For letter-only inputs, Prism reports the gap honestly as
              a methodology residual rather than inventing a breakdown.
            </p>
          </div>
        ) : (
          <div data-testid="waterfall-chart">
            <ResponsiveContainer width="100%" height={220}>
              <BarChart
                data={[
                  ...BUCKET_ORDER.map((bucket) => ({
                    name: BUCKET_LABEL[bucket],
                    notches: bucketOf(divergence, bucket)?.notches ?? 0,
                    color: BUCKET_COLOR[bucket],
                  })),
                  { name: 'Net gap', notches: divergence.notchGap, color: NET_COLOR },
                ]}
                margin={{ top: 8, right: 8, bottom: 8, left: 8 }}
              >
                <XAxis dataKey="name" tick={{ fontSize: 12, fill: '#94a3b8' }} />
                <YAxis allowDecimals tick={{ fontSize: 12, fill: '#94a3b8' }} />
                <ReferenceLine y={0} stroke="#475569" />
                <Bar dataKey="notches" radius={[3, 3, 0, 0]} label={{ position: 'top', fontSize: 11, fill: '#e2e8f0' }}>
                  {BUCKET_ORDER.map((bucket) => (
                    <Cell key={bucket} fill={BUCKET_COLOR[bucket]} />
                  ))}
                  <Cell fill={NET_COLOR} />
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        )}

        {legend}
        <DeferredNarrationNote subject="this decomposition" className="mt-3" />
      </CardContent>
    </Card>
  )
}
