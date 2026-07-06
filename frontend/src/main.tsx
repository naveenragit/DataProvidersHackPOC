// frontend/src/main.tsx
// CRITICAL: the `import './index.css'` line loads Tailwind + the shadcn theme variables.
// Without it, the app renders as unstyled raw HTML. All providers live in App.tsx.
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
