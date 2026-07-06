// frontend/src/pages/ArchitecturePage.tsx
// Static architecture summary (no fetching). Reflects the planned Prism system:
// a deterministic C# core that the LLM agents narrate and cite — reconciliation, not trading.
import { Bot, Calculator, Cloud, Database, ShieldCheck } from 'lucide-react'
import { PageHeader } from '@/components/ui/PageHeader'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

interface CatalogItem {
  name: string
  detail: string
}

const AGENTS: CatalogItem[] = [
  { name: 'ReconciliationOrchestrator', detail: 'Coordinates the sweep and merges narration with the deterministic core.' },
  { name: 'ProviderExplainerAgent', detail: 'Retrieves each provider verdict with methodology citations.' },
  { name: 'FundamentalsAgent', detail: 'Assembles the issuer fundamentals snapshot from EDGAR / FRED.' },
  { name: 'DivergenceNarratorAgent', detail: 'Narrates the decomposed notch gaps — cites, never computes.' },
  { name: 'RedFlagNarratorAgent', detail: 'Narrates deterministic red flags with their evidence references.' },
]

const ENGINES: CatalogItem[] = [
  { name: 'NotchLadder', detail: 'Canonical letter → notch mapping across providers.' },
  { name: 'DivergenceDecomposer', detail: 'Splits each notch gap into Weighting / Input / MethodologyAdjustment with an exact invariant.' },
  { name: 'RedFlagEngine', detail: 'Stale-input, missing-coverage, outlier, and methodology-conflict rules (date-only UTC).' },
  { name: 'ReconciliationScoring', detail: 'Deterministic consensus summary and 0..1 confidence score.' },
]

const AZURE_SERVICES: CatalogItem[] = [
  { name: 'Azure AI Foundry', detail: 'Hosts the narration agents (Microsoft Agent Framework).' },
  { name: 'Azure AI Search', detail: 'Index prism-ratings — provider rating cards + methodology docs.' },
  { name: 'Azure Cosmos DB', detail: 'Database prism — persisted reconciliation dossiers.' },
]

const DATA_SOURCES: CatalogItem[] = [
  { name: 'SEC EDGAR', detail: 'XBRL company facts — fundamentals and latest filing date.' },
  { name: 'FRED', detail: 'Reference macro / spread series.' },
  { name: "Moody's", detail: 'Provider rating verdicts (real API).' },
  { name: 'Morningstar DBRS', detail: 'Provider rating verdicts (real API).' },
  { name: 'MSCI (synthetic)', detail: 'Labeled synthetic provider slot for the third view.' },
]

function CatalogCard({
  title,
  icon: Icon,
  items,
}: {
  title: string
  icon: typeof Bot
  items: CatalogItem[]
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2 text-sm">
          <Icon className="h-4 w-4 text-primary" />
          {title}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {items.map((item) => (
          <div key={item.name} className="space-y-0.5">
            <div className="font-mono text-xs text-foreground">{item.name}</div>
            <div className="text-xs text-muted-foreground">{item.detail}</div>
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

export default function ArchitecturePage() {
  return (
    <div className="max-w-7xl space-y-6">
      <PageHeader
        title="Architecture"
        subtitle="How Prism reconciles corporate-bond credit ratings across providers."
      />

      <Card className="border-primary/30 bg-primary/5">
        <CardContent className="flex items-start gap-3 p-5">
          <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-primary" />
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <span className="text-sm font-semibold text-foreground">Deterministic core</span>
              <Badge variant="outline" className="border-primary/40 text-primary">
                Reconciliation, not trading
              </Badge>
            </div>
            <p className="text-sm text-muted-foreground">
              The notch math and red-flag rules live in plain C# (<span className="font-mono">Analysis/</span>).
              The LLM agents only narrate and cite — they never compute the verdict or make an investment
              decision. Every claim in a dossier is traced to a provider card or a filing.
            </p>
          </div>
        </CardContent>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <CatalogCard title="Foundry AI Agents" icon={Bot} items={AGENTS} />
        <CatalogCard title="Deterministic Analysis engines" icon={Calculator} items={ENGINES} />
        <CatalogCard title="Azure services" icon={Cloud} items={AZURE_SERVICES} />
        <CatalogCard title="Data sources" icon={Database} items={DATA_SOURCES} />
      </div>
    </div>
  )
}
