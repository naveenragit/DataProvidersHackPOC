// frontend/src/components/prism/MorningstarContextPanel.tsx
// Live Morningstar analyst-research companion to the rating reconciliation. Prism's issuers are
// illustrative (fictional) and don't exist in Morningstar's listed-security universe, so — to keep
// this panel RELATED to the issuer on screen — it defaults to live research for a real, comparable
// company in the SAME sector (a curated "sector comparable"), clearly labeled as context about that
// company, not the issuer, and never a buy/sell/hold view (P4). A secondary lookup lets you explore
// any other listed security. Wired to GET /api/v1/market-context/morningstar (real MCP call, headless
// token reuse); every provider state comes back as a 200 with a `status` (fail-soft).
import { useState, type FormEvent, type ReactNode } from 'react'
import {
  AlertTriangle,
  Building2,
  ExternalLink,
  Info,
  Loader2,
  Radio,
  Search,
  SearchX,
} from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { useMorningstarContext } from '@/hooks/useMorningstarContext'
import { comparableForSector } from '@/lib/marketContextComparables'
import { ApiError } from '@/lib/apiClient'
import type { MorningstarContextResponse } from '@/types/prism'

interface MorningstarContextPanelProps {
  /** The reconciled issuer's id — used for labeling and to reset the panel per issuer. */
  issuerId: string
  /** The issuer's legal name, for human-friendly labeling. */
  issuerName?: string
  /** The issuer's sector — selects a curated real comparable when the issuer has no ticker. */
  sector?: string
  /** The issuer's real ticker — when present, research is shown for the actual issuer. */
  ticker?: string
}

function formatDate(iso: string | null): string | null {
  if (!iso) return null
  const date = new Date(iso)
  return Number.isNaN(date.getTime())
    ? null
    : date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

export function MorningstarContextPanel({ issuerId, issuerName, sector, ticker }: MorningstarContextPanelProps) {
  const issuerTicker = ticker?.trim() ?? ''
  const comparable = comparableForSector(sector)
  const initialId = issuerTicker || comparable?.ticker || ''
  const [input, setInput] = useState(initialId)
  const [identifier, setIdentifier] = useState(initialId)
  const { data, isFetching, isError, error } = useMorningstarContext(identifier)
  const issuerLabel = issuerName ?? issuerId
  const sectorLabel = sector ? sector.toLowerCase() : ''

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const next = input.trim()
    if (next) {
      setIdentifier(next)
    }
  }

  return (
    <Card data-testid="morningstar-context-panel">
      <CardHeader className="space-y-3">
        <CardTitle className="flex flex-wrap items-center gap-2">
          Market Context
          <Badge variant="secondary" className="gap-1 font-medium">
            <Radio className="h-3 w-3 text-emerald-400" aria-hidden="true" />
            Live · Morningstar
          </Badge>
        </CardTitle>
        <p className="text-sm text-muted-foreground">
          {issuerTicker ? (
            <>
              Live Morningstar analyst research for{' '}
              <span className="font-medium text-foreground">{issuerLabel}</span> (
              <span className="font-mono">{issuerTicker}</span>), shown as independent market context
              alongside the reconciliation. The provider credit ratings above are illustrative; this
              equity research is live. Not investment advice.
            </>
          ) : comparable ? (
            <>
              <span className="font-medium text-foreground">{issuerLabel}</span> is an illustrative{' '}
              {sectorLabel} issuer, so it isn&rsquo;t listed in Morningstar&rsquo;s universe. For sector
              context, this shows <span className="font-medium text-foreground">live</span> Morningstar
              analyst research on <span className="font-medium text-foreground">{comparable.name}</span>{' '}
              (<span className="font-mono">{comparable.ticker}</span>) — a comparable real-world{' '}
              {sectorLabel} name. It describes that company, not {issuerLabel}, and is not investment
              advice.
            </>
          ) : (
            <>
              Live Morningstar analyst research for any real listed security, shown as independent
              sector context alongside the reconciliation. Prism&rsquo;s issuers are illustrative, so
              try a real ticker like <span className="font-mono">AAPL</span>.
            </>
          )}
        </p>
        <form onSubmit={handleSubmit} className="flex items-center gap-2">
          <Input
            value={input}
            onChange={(event) => setInput(event.target.value)}
            placeholder="Look up another ticker, ISIN, or company"
            aria-label="Morningstar identifier"
            className="max-w-xs"
          />
          <Button type="submit" variant="outline" className="gap-2" disabled={isFetching || !input.trim()}>
            {isFetching ? (
              <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
            ) : (
              <Search className="h-4 w-4" aria-hidden="true" />
            )}
            Look up
          </Button>
        </form>
      </CardHeader>

      <CardContent className="space-y-4">
        {identifier.trim().length === 0 ? (
          <StateNote tone="muted" icon={<Info className="h-4 w-4" />}>
            Enter a real ticker, ISIN, or company name above to load live Morningstar research.
          </StateNote>
        ) : isFetching ? (
          <div className="flex items-center gap-3 p-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin text-primary" aria-hidden="true" />
            <span>
              Fetching live Morningstar research for <span className="font-mono">{identifier}</span>…
            </span>
          </div>
        ) : isError ? (
          <StateNote tone="warn" icon={<AlertTriangle className="h-4 w-4" />}>
            Couldn&rsquo;t look that up ({error instanceof ApiError ? error.code : 'UNKNOWN'}). Try a
            valid ticker, ISIN, or company name.
          </StateNote>
        ) : data ? (
          <ResultBody data={data} />
        ) : null}

        <p className="border-t border-border/60 pt-3 text-xs text-muted-foreground">
          {data?.disclaimer ??
            'Third-party Morningstar analyst research, shown for context only — not investment advice.'}
        </p>
      </CardContent>
    </Card>
  )
}

function ResultBody({ data }: { data: MorningstarContextResponse }) {
  if (data.status === 'NotCovered') {
    return <StateNote tone="muted" icon={<SearchX className="h-4 w-4" />}>{data.message}</StateNote>
  }
  if (data.status === 'Disabled') {
    return <StateNote tone="muted" icon={<Info className="h-4 w-4" />}>{data.message}</StateNote>
  }
  if (data.status === 'ReloginRequired' || data.status === 'Unavailable') {
    return <StateNote tone="warn" icon={<AlertTriangle className="h-4 w-4" />}>{data.message}</StateNote>
  }

  // status === 'Ok'
  return (
    <div className="space-y-4">
      {data.investment && (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg border border-border bg-muted/30 px-3 py-2">
          <Building2 className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
          <span className="font-medium text-foreground">{data.investment.name}</span>
          {data.investment.ticker && (
            <span className="font-mono text-xs text-muted-foreground">{data.investment.ticker}</span>
          )}
          {data.investment.exchange && (
            <span className="text-xs text-muted-foreground">{data.investment.exchange}</span>
          )}
        </div>
      )}

      {data.sections.length === 0 ? (
        <StateNote tone="muted" icon={<Info className="h-4 w-4" />}>{data.message}</StateNote>
      ) : (
        <ul className="space-y-3">
          {data.sections.map((section, index) => (
            <li key={`${index}-${section.title}`} className="rounded-lg border border-border bg-background/40 p-3">
              <div className="flex items-start justify-between gap-3">
                <h4 className="text-sm font-semibold text-foreground">{section.title}</h4>
                {formatDate(section.publishedAt) && (
                  <span className="whitespace-nowrap text-xs text-muted-foreground">
                    {formatDate(section.publishedAt)}
                  </span>
                )}
              </div>
              <p className="mt-1.5 text-sm leading-relaxed text-muted-foreground">{section.excerpt}</p>
              {section.url && (
                <a
                  href={section.url}
                  target="_blank"
                  rel="noreferrer noopener"
                  className="mt-2 inline-flex items-center gap-1 text-xs font-medium text-primary hover:underline"
                >
                  Read on Morningstar
                  <ExternalLink className="h-3 w-3" aria-hidden="true" />
                </a>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

function StateNote({
  tone,
  icon,
  children,
}: {
  tone: 'muted' | 'warn'
  icon: ReactNode
  children: ReactNode
}) {
  const toneClass = tone === 'warn' ? 'text-amber-500' : 'text-muted-foreground'
  return (
    <div className="flex items-start gap-2 text-sm text-muted-foreground">
      <span className={`mt-0.5 flex-shrink-0 ${toneClass}`} aria-hidden="true">
        {icon}
      </span>
      <span>{children}</span>
    </div>
  )
}
