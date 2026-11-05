import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { ApiError } from '@/lib/api'
import { getSession, logoutSession } from '@/lib/session-api'
import { SessionContext, type SessionState } from './session-context'
import { eligibleMemberships, type WorkspaceKind } from './permissions'
import { sessionExpiredEvent } from '@/lib/http-client'

export function SessionProvider({ children, workspace }: { children: ReactNode; workspace?: WorkspaceKind }) {
  const [state, setState] = useState<SessionState>({ status: 'loading' })
  const [selectedOrganizationId, setSelectedOrganizationId] = useState<string>()
  const userId = useRef<string | null>(null)
  const pending = useRef<AbortController | null>(null)
  const execute = useCallback(async (operation: 'refresh' | 'logout') => {
    pending.current?.abort()
    const controller = new AbortController()
    pending.current = controller
    const signal = AbortSignal.any([controller.signal, AbortSignal.timeout(10000)])
    try {
      if (operation === 'logout') {
        await logoutSession(signal)
        if (!controller.signal.aborted) {
          userId.current = null
          setSelectedOrganizationId(undefined)
          setState({ status: 'anonymous', message: 'You have signed out of this browser session.' })
        }
      } else {
        const session = await getSession(signal)
        if (!controller.signal.aborted) {
          const firstId = session && workspace ? eligibleMemberships(workspace, session)[0]?.organizationId : undefined
          if (!session || userId.current !== session.userId) setSelectedOrganizationId(firstId)
          else setSelectedOrganizationId((current) => current ?? firstId)
          userId.current = session?.userId ?? null
          setState(session ? { status: 'authenticated', session } : { status: 'anonymous' })
        }
      }
    } catch (error) {
      if (controller.signal.aborted) return
      if (operation === 'logout' && error instanceof ApiError && error.status === 401) {
        userId.current = null
        setSelectedOrganizationId(undefined)
        setState({ status: 'anonymous', message: 'Your session has expired. Please sign in again.' })
      } else {
        setState({ status: 'error', message: error instanceof Error && error.name === 'TimeoutError'
          ? 'The session request timed out. Please retry.'
          : error instanceof Error ? error.message : 'Unable to verify your session. Please retry.' })
      }
    } finally {
      if (pending.current === controller) pending.current = null
    }
  }, [workspace])
  const refresh = useCallback(() => {
    setState({ status: 'loading' })
    return execute('refresh')
  }, [execute])
  const logout = useCallback(() => {
    setState({ status: 'loading' })
    return execute('logout')
  }, [execute])
  const selectOrganization = useCallback((id: string) => {
    setSelectedOrganizationId(id)
    return refresh()
  }, [refresh])

  useEffect(() => {
    // eslint-disable-next-line react/set-state-in-effect -- Synchronize the external cookie session; updates occur after awaiting the API.
    void execute('refresh')
    const refreshOnFocus = () => { if (!pending.current) void refresh() }
    const expire = () => {
      pending.current?.abort()
      pending.current = null
      userId.current = null
      setSelectedOrganizationId(undefined)
      setState({ status: 'anonymous', message: 'Your session has expired. Please sign in again.' })
    }
    window.addEventListener('focus', refreshOnFocus)
    window.addEventListener(sessionExpiredEvent, expire)
    return () => {
      window.removeEventListener('focus', refreshOnFocus)
      window.removeEventListener(sessionExpiredEvent, expire)
      pending.current?.abort()
    }
  }, [execute, refresh])

  return <SessionContext.Provider value={{ state, refresh, logout, selectedOrganizationId, selectOrganization }}>{children}</SessionContext.Provider>
}
