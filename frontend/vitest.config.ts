// frontend/vitest.config.ts
// Separate from vite.config.ts (kept pristine per template) so the vitest `defineConfig`
// owns the test block. Re-declares the `@` → ./src alias and boots jsdom.
import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
    css: false,
  },
})
