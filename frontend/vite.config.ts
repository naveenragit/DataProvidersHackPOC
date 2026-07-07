// Copy to frontend/vite.config.ts
// The `@` alias lets every component import as `@/components/...` / `@/pages/...`.
// Proxies:
//   /api        → the C# ASP.NET Core backend (REST)
//   /copilotkit → the CopilotKit Node runtime sidecar (AI copilot)
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: process.env.VITE_BACKEND_URL ?? 'http://localhost:8000',
        changeOrigin: true,
      },
      '/copilotkit': {
        target: process.env.VITE_COPILOT_URL ?? 'http://localhost:4000',
        changeOrigin: true,
      },
    },
  },
})
