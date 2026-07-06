// frontend/src/components/ErrorBoundary.tsx
//
// Top-level error boundary (arch-10). Catches render/runtime errors thrown anywhere below it
// and renders a shadcn-styled fallback card with a reload action. The raw error + component
// stack are logged to the developer console only — they are NEVER shown to the user, so we
// don't leak internals or stack traces into the UI (SEC-03 / arch-06).
import { Component, type ErrorInfo, type ReactNode } from 'react'
import { AlertTriangle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

interface ErrorBoundaryProps {
  children: ReactNode
}

interface ErrorBoundaryState {
  hasError: boolean
}

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props)
    this.state = { hasError: false }
  }

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    // Developer-only diagnostics; deliberately not surfaced to the user.
    console.error('Unhandled UI error:', error, info.componentStack)
  }

  private readonly handleReload = (): void => {
    window.location.reload()
  }

  render(): ReactNode {
    if (this.state.hasError) {
      return (
        <div className="flex min-h-screen items-center justify-center bg-background p-6">
          <Card className="max-w-md border-destructive/40">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-destructive">
                <AlertTriangle className="h-5 w-5" />
                Something went wrong
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <p className="text-sm text-muted-foreground">
                The interface hit an unexpected error and can&rsquo;t continue. Reloading usually
                clears it. If the problem persists, contact your administrator.
              </p>
              <Button onClick={this.handleReload}>Reload</Button>
            </CardContent>
          </Card>
        </div>
      )
    }

    return this.props.children
  }
}
