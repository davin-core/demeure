import { availableWorkspaces } from '@/components/layout/permissions'
import type { Session } from './session-api'

export function loginDestination(session: Session, returnTo: string | null) {
  if (returnTo === '/landlord') returnTo = '/app/dashboard'
  const paths = availableWorkspaces(session).map((workspace) => workspace.path)
  return returnTo && paths.includes(returnTo) ? returnTo : paths[0] ?? '/public'
}
