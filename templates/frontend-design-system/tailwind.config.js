/** @type {import('tailwindcss').Config} */
// Financial Services dark theme design system.
// Copy to frontend/tailwind.config.js — do NOT change the token values.
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        surface: {
          DEFAULT: '#0f1117',
          50: '#1a1f2e',
          100: '#141824',
          200: '#0f1117',
        },
        card: '#1a1f2e',
        border: '#2a3040',
        accent: {
          DEFAULT: '#6366f1',
          hover: '#818cf8',
          muted: '#312e81',
        },
        brand: {
          gold: '#f59e0b',
          'gold-muted': '#78350f',
          teal: '#14b8a6',
          'teal-muted': '#134e4a',
        },
        status: {
          success: '#22c55e',
          warning: '#f59e0b',
          error: '#ef4444',
          info: '#3b82f6',
        },
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'monospace'],
      },
      borderRadius: {
        lg: '0.75rem',
        xl: '1rem',
        '2xl': '1.25rem',
      },
    },
  },
  plugins: [],
}
