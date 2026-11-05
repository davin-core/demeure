import { lazy, Suspense } from 'react'
import App from './App'
import type { WorkspaceKind } from './components/layout/permissions'

const DesignSystem = lazy(() => import('./pages/design-system'))
const ApplicationLayouts = lazy(() => import('./pages/application-layouts'))
const LoginPage = lazy(() => import('./pages/login'))
const layouts: Record<string, WorkspaceKind | 'public'> = {
  '/public': 'public', '/landlord': 'landlord', '/tenant': 'tenant', '/contractor': 'contractor', '/administration': 'administration',
}

export default function AppRouter() {
  const path = window.location.pathname.replace(/\/$/, '')
  const kind = layouts[path]
  if (path !== '/design-system' && path !== '/login' && !kind) return <App />
  return <Suspense fallback={<p className="p-8 text-muted-foreground" role="status">Loading Demeure...</p>}>
    {path === '/login' ? <LoginPage /> : kind ? <ApplicationLayouts kind={kind} /> : <DesignSystem />}
  </Suspense>
}
