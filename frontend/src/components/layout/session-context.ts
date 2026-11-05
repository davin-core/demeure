import { createContext, useContext } from 'react'
import type { Session } from '@/lib/session-api'

export type SessionState =
  | { status: 'loading' }
  | { status: 'anonymous'; message?: string }
  | { status: 'error'; message: string }
  | { status: 'authenticated'; session: Session }

export const SessionContext = createContext<{
  state: SessionState; refresh: () => Promise<void>; logout: () => Promise<void>
  selectedOrganizationId?: string; selectOrganization: (id: string) => Promise<void>
} | null>(null)

export function useSession() {
  const context = useContext(SessionContext)
  if (!context) throw new Error('useSession must be used within SessionProvider.')
  return context
}
