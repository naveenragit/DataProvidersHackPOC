import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import {
  DEFAULT_SETTINGS,
  loadSettings,
  saveSettings,
  type PrismSettings,
} from './settings'

const STORAGE_KEY = 'prism.settings'

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  localStorage.clear()
})

describe('settings', () => {
  it('round-trips valid custom settings through localStorage', () => {
    const custom: PrismSettings = {
      ...DEFAULT_SETTINGS,
      models: { ...DEFAULT_SETTINGS.models, fundamentals: 'o4-mini' },
      // Canonical order (Moodys, MorningstarDbrs, Msci) — validation preserves this order.
      providers: ['Moodys', 'Msci'],
      defaultAsOf: '2026-03-15',
      flags: { showDeterministicRule: false },
    }

    saveSettings(custom)

    expect(loadSettings()).toEqual(custom)
  })

  it('returns defaults when the stored JSON is corrupt', () => {
    localStorage.setItem(STORAGE_KEY, '{not valid json')

    expect(loadSettings()).toEqual(DEFAULT_SETTINGS)
  })

  it('drops a tampered provider value and keeps only whitelisted providers', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ providers: ['Moodys', 'Evilcorp', '../etc/passwd'] }),
    )

    expect(loadSettings().providers).toEqual(['Moodys'])
  })

  it('falls back to the default providers when none are whitelisted', () => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ providers: ['Nope', 123, null] }))

    expect(loadSettings().providers).toEqual(DEFAULT_SETTINGS.providers)
  })

  it('drops unknown model agent keys, non-whitelisted models, and invalid as-of dates', () => {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({
        models: {
          fundamentals: 'o4-mini', // valid known key + whitelisted model → kept
          providerExplainer: 'totally-made-up-model', // known key, invalid model → default
          hackerAgent: 'gpt-4o', // unknown key → dropped
        },
        defaultAsOf: 'not-a-date',
      }),
    )

    const loaded = loadSettings()

    expect(loaded.models.fundamentals).toBe('o4-mini')
    expect(loaded.models.providerExplainer).toBe(DEFAULT_SETTINGS.models.providerExplainer)
    expect((loaded.models as Record<string, string>).hackerAgent).toBeUndefined()
    expect(loaded.defaultAsOf).toBe(DEFAULT_SETTINGS.defaultAsOf)
  })
})
