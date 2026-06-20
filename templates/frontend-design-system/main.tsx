// Copy to frontend/src/main.tsx
// CRITICAL: the `import './index.css'` line is what loads Tailwind + the theme.
// Without it, the app renders as unstyled raw HTML.
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import App from './App'
import './index.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
)
