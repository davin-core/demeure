import { useEffect, useState } from 'react'
import { Button } from '@/components/ui/button'
import { Brand } from '@/components/layout/brand'
import { getHealth, type HealthResponse } from '@/lib/api'
import './App.css'

type HealthState =
  | { state: 'loading' }
  | { state: 'loaded'; health: HealthResponse }
  | { state: 'error'; message: string }

function App() {
  const [result, setResult] = useState<HealthState>({ state: 'loading' })
  const [refreshCount, setRefreshCount] = useState(0)

  useEffect(() => {
    const controller = new AbortController()

    getHealth(AbortSignal.any([controller.signal, AbortSignal.timeout(5000)]))
      .then((health) => {
        if (!controller.signal.aborted) setResult({ state: 'loaded', health })
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        setResult({
          state: 'error',
          message: error instanceof Error && error.name === 'TimeoutError'
            ? 'The API did not respond within 5 seconds.'
            : error instanceof Error ? error.message : 'Unable to reach the API.',
        })
      })

    return () => controller.abort()
  }, [refreshCount])

  const health = result.state === 'loaded' ? result.health : null
  const isOnline = health?.status === 'ok'
  const isLoading = result.state === 'loading'
  const statusLabel = isLoading ? 'Checking' : isOnline ? 'Online' : health ? health.status : 'Offline'

  return (
    <main className="shell">
      <header className="topbar">
        <Brand />
        <span className="environment"><span />LOCAL ENVIRONMENT</span>
      </header>

      <section className="overview" aria-labelledby="page-title">
        <p className="eyebrow">DEVELOPER WORKSPACE / 01</p>
        <h1 id="page-title">Your foundation<br />is in place.</h1>
        <p className="intro">The React interface and .NET API are ready to build on.</p>

        <div className={`status-panel ${isOnline ? 'is-online' : ''}`} aria-live="polite">
          <div className="status-heading">
            <span className="status-indicator" />
            <div>
              <p className="status-label">API CONNECTION</p>
              <h2>{isLoading ? 'Checking service' : isOnline ? 'All systems operational' : health ? 'API needs attention' : 'API unavailable'}</h2>
            </div>
            <Button
              variant="outline"
              size="icon"
              className="refresh-button"
              type="button"
              onClick={() => {
                setResult({ state: 'loading' })
                setRefreshCount((count) => count + 1)
              }}
              aria-label="Refresh API status"
              title="Refresh API status"
            >
              ↻
            </Button>
          </div>

          {result.state === 'error' && <p className="api-error" role="alert">{result.message}</p>}

          <div className="service-row">
            <span className="service-name">ASP.NET Core</span>
            <span className="service-detail">{health ? health.service : 'GET /api/health'}</span>
            <span className={`service-state ${isOnline ? 'online' : ''}`}>
              {statusLabel}
            </span>
          </div>
          <div className="service-row">
            <span className="service-name">React + TypeScript</span>
            <span className="service-detail">Vite development server</span>
            <span className="service-state online">Ready</span>
          </div>
        </div>
      </section>

      <footer className="footer">
        <span>DEMEURE <span className="footer-divider">/</span> <a href="/design-system" className="text-primary underline underline-offset-4">DESIGN SYSTEM</a> <span className="footer-divider">/</span> <a href="/public" className="text-primary underline underline-offset-4">PUBLIC WEBSITE</a></span>
        <span>.NET 10 <span className="footer-divider">·</span> REACT 19</span>
      </footer>
    </main>
  )
}

export default App
