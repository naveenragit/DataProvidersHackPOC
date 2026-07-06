/// <reference types="vite/client" />

// Typed Vite build-time env. Only `VITE_*` keys are exposed to the client bundle.
interface ImportMetaEnv {
  /**
   * CopilotKit Node runtime sidecar URL (package 07). When unset the app falls back to the
   * Vite-proxied `/copilotkit` path. Set in production to the deployed sidecar origin.
   */
  readonly VITE_COPILOT_URL?: string
}
