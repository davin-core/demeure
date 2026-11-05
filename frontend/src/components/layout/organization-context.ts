import { createContext, useContext } from 'react'
import type { Membership } from '@/lib/session-api'

export const OrganizationContext = createContext<Membership | null>(null)

export function useOrganization() {
  const membership = useContext(OrganizationContext)
  if (!membership) throw new Error('useOrganization requires an authorized organization layout.')
  return membership
}
