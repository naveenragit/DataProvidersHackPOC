// frontend/src/pages/SettingsPage.tsx
// Demo settings persisted to localStorage (local UI state — useState/useEffect is fine here).
// Server-side config (`Prism__Models__*`, provider coverage) is the productionization path.
import { useState } from 'react'
import { PageHeader } from '@/components/ui/PageHeader'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  AGENT_LABELS,
  ALL_PROVIDERS,
  MODEL_OPTIONS,
  PROVIDER_LABELS,
  loadSettings,
  saveSettings,
  type PrismAgentKey,
  type PrismSettings,
} from '@/lib/settings'
import type { Provider } from '@/types/prism'

const AGENT_KEYS = Object.keys(AGENT_LABELS) as PrismAgentKey[]

export default function SettingsPage() {
  const [settings, setSettings] = useState<PrismSettings>(() => loadSettings())

  function persist(next: PrismSettings) {
    setSettings(next)
    saveSettings(next)
  }

  function setModel(agent: PrismAgentKey, model: string) {
    persist({ ...settings, models: { ...settings.models, [agent]: model } })
  }

  function toggleProvider(provider: Provider) {
    const selected = new Set(settings.providers)
    if (selected.has(provider)) {
      selected.delete(provider)
    } else {
      selected.add(provider)
    }
    persist({ ...settings, providers: ALL_PROVIDERS.filter((p) => selected.has(p)) })
  }

  function setDefaultAsOf(value: string) {
    persist({ ...settings, defaultAsOf: value })
  }

  function toggleDeterministicRule() {
    persist({
      ...settings,
      flags: { ...settings.flags, showDeterministicRule: !settings.flags.showDeterministicRule },
    })
  }

  return (
    <div className="max-w-3xl space-y-6">
      <PageHeader
        title="Settings"
        subtitle="Configure the reconciliation experience for this demo."
      />

      {/* Model per agent */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Model per agent</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {AGENT_KEYS.map((key) => (
            <div key={key} className="flex items-center justify-between gap-4">
              <Label id={`model-label-${key}`}>{AGENT_LABELS[key]}</Label>
              <Select value={settings.models[key]} onValueChange={(v) => setModel(key, v)}>
                <SelectTrigger className="w-48" aria-labelledby={`model-label-${key}`}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MODEL_OPTIONS.map((model) => (
                    <SelectItem key={model} value={model}>
                      {model}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          ))}
          <p className="text-xs text-muted-foreground">
            Maps to server-side <span className="font-mono">Prism__Models__*</span> configuration in
            production.
          </p>
        </CardContent>
      </Card>

      {/* Providers to include */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Providers to include</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex flex-wrap gap-2">
            {ALL_PROVIDERS.map((provider) => {
              const active = settings.providers.includes(provider)
              return (
                <Button
                  key={provider}
                  variant={active ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => toggleProvider(provider)}
                  aria-pressed={active}
                >
                  {PROVIDER_LABELS[provider]}
                </Button>
              )
            })}
          </div>
          <p className="text-xs text-muted-foreground">
            Providers included in a reconciliation sweep. All three are on by default.
          </p>
        </CardContent>
      </Card>

      {/* Default as-of date */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Default as-of date</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          <Label htmlFor="default-as-of">As-of date for new reconciliations</Label>
          <Input
            id="default-as-of"
            type="date"
            className="w-48"
            value={settings.defaultAsOf}
            onChange={(e) => setDefaultAsOf(e.target.value)}
          />
        </CardContent>
      </Card>

      {/* Endpoints (read-only) */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Endpoints</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label htmlFor="api-endpoint">REST API</Label>
            <Input id="api-endpoint" disabled value="/api/v1" className="font-mono text-xs" />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="copilot-endpoint">Copilot runtime</Label>
            <Input id="copilot-endpoint" disabled value="/copilotkit" className="font-mono text-xs" />
          </div>
          <p className="text-xs text-muted-foreground sm:col-span-2">
            Endpoints are proxied by Vite to the C# API (:8000) and the CopilotKit sidecar (:4000).
          </p>
        </CardContent>
      </Card>

      {/* Feature flags */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm">Feature flags</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="flex items-center justify-between gap-4">
            <div className="space-y-0.5">
              <Label id="flag-rule-label">Show deterministic rule text</Label>
              <p className="text-xs text-muted-foreground">
                Display the verbatim rule behind each red flag.
              </p>
            </div>
            <Button
              aria-labelledby="flag-rule-label"
              variant={settings.flags.showDeterministicRule ? 'default' : 'outline'}
              size="sm"
              onClick={toggleDeterministicRule}
              aria-pressed={settings.flags.showDeterministicRule}
            >
              {settings.flags.showDeterministicRule ? 'On' : 'Off'}
            </Button>
          </div>
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">
        Settings persist to your browser (localStorage) for the demo. In production they map to
        server-side configuration.
      </p>
    </div>
  )
}
