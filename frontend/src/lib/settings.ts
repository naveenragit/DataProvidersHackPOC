// frontend/src/lib/settings.ts
//
// Local UI settings for the Prism demo, persisted to localStorage. This is local UI state
// (not server state) — `useState`/`useEffect` in the Settings page is appropriate. The
// productionization path is server-side config (`Prism__Models__*`, provider toggles),
// noted in the Settings page.
import type { Provider } from '@/types/prism'

/** Agents whose model deployment can be chosen (maps to `Prism__Models__*` server config). */
export type PrismAgentKey =
  | 'reconciliationOrchestrator'
  | 'providerExplainer'
  | 'fundamentals'
  | 'divergenceNarrator'
  | 'redFlagNarrator'

export const AGENT_LABELS: Record<PrismAgentKey, string> = {
  reconciliationOrchestrator: 'Reconciliation Orchestrator',
  providerExplainer: 'Provider Explainer Agent',
  fundamentals: 'Fundamentals Agent',
  divergenceNarrator: 'Divergence Narrator Agent',
  redFlagNarrator: 'Red-Flag Narrator Agent',
}

/** Model deployment options offered per agent (demo-side list; server config is authoritative). */
export const MODEL_OPTIONS = ['gpt-4o', 'gpt-4o-mini', 'o4-mini'] as const

/** The three real Prism providers (mirrors the C# Provider enum). */
export const ALL_PROVIDERS: Provider[] = ['Moodys', 'MorningstarDbrs', 'Msci']

export const PROVIDER_LABELS: Record<Provider, string> = {
  Moodys: "Moody's",
  MorningstarDbrs: 'Morningstar DBRS',
  Msci: 'MSCI',
}

export interface PrismSettings {
  /** Chosen model deployment per agent. */
  models: Record<PrismAgentKey, string>
  /** Providers to include in a reconciliation sweep. */
  providers: Provider[]
  /** Default as-of date for new reconciliations (yyyy-mm-dd). */
  defaultAsOf: string
  /** Feature flags. */
  flags: {
    /** Show the verbatim deterministic rule text alongside each red flag. */
    showDeterministicRule: boolean
  }
}

const STORAGE_KEY = 'prism.settings'

export const DEFAULT_SETTINGS: PrismSettings = {
  models: {
    reconciliationOrchestrator: 'gpt-4o',
    providerExplainer: 'gpt-4o-mini',
    fundamentals: 'gpt-4o-mini',
    divergenceNarrator: 'gpt-4o',
    redFlagNarrator: 'gpt-4o-mini',
  },
  providers: [...ALL_PROVIDERS],
  defaultAsOf: '',
  flags: {
    showDeterministicRule: true,
  },
}

/** Reads settings from localStorage, whitelist-validated over defaults. Returns defaults on empty/corrupt JSON. */
export function loadSettings(): PrismSettings {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return DEFAULT_SETTINGS

    const parsed = JSON.parse(raw) as unknown
    if (!parsed || typeof parsed !== 'object') return DEFAULT_SETTINGS

    const record = parsed as Record<string, unknown>
    return {
      models: sanitizeModels(record.models),
      providers: sanitizeProviders(record.providers),
      defaultAsOf: sanitizeAsOf(record.defaultAsOf),
      flags: sanitizeFlags(record.flags),
    }
  } catch {
    return DEFAULT_SETTINGS
  }
}

const AGENT_KEY_SET = new Set<string>(Object.keys(DEFAULT_SETTINGS.models))
const PROVIDER_SET = new Set<string>(ALL_PROVIDERS)
const MODEL_SET = new Set<string>(MODEL_OPTIONS)
const ISO_DATE_RE = /^\d{4}-\d{2}-\d{2}$/

/** Keep only known agent keys mapped to a whitelisted model option; every other field defaults. */
function sanitizeModels(input: unknown): Record<PrismAgentKey, string> {
  const models = { ...DEFAULT_SETTINGS.models }
  if (input && typeof input === 'object') {
    for (const [key, value] of Object.entries(input as Record<string, unknown>)) {
      if (AGENT_KEY_SET.has(key) && typeof value === 'string' && MODEL_SET.has(value)) {
        models[key as PrismAgentKey] = value
      }
    }
  }
  return models
}

/** Keep only whitelisted providers, in canonical order; fall back to defaults if none survive. */
function sanitizeProviders(input: unknown): Provider[] {
  if (!Array.isArray(input)) return [...DEFAULT_SETTINGS.providers]
  const seen = new Set<Provider>()
  for (const item of input) {
    if (typeof item === 'string' && PROVIDER_SET.has(item)) {
      seen.add(item as Provider)
    }
  }
  const result = ALL_PROVIDERS.filter((p) => seen.has(p))
  return result.length > 0 ? result : [...DEFAULT_SETTINGS.providers]
}

/** Accept only a well-formed, real ISO calendar date (yyyy-mm-dd); otherwise default. */
function sanitizeAsOf(input: unknown): string {
  if (typeof input === 'string' && ISO_DATE_RE.test(input) && !Number.isNaN(Date.parse(input))) {
    return input
  }
  return DEFAULT_SETTINGS.defaultAsOf
}

/** Accept only the known boolean flag; ignore unknown/invalid flag fields. */
function sanitizeFlags(input: unknown): PrismSettings['flags'] {
  const flags = { ...DEFAULT_SETTINGS.flags }
  if (input && typeof input === 'object') {
    const raw = input as Record<string, unknown>
    if (typeof raw.showDeterministicRule === 'boolean') {
      flags.showDeterministicRule = raw.showDeterministicRule
    }
  }
  return flags
}

/** Persists settings to localStorage. */
export function saveSettings(settings: PrismSettings): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(settings))
}
