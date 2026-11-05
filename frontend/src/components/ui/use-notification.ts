import { createContext, useContext } from 'react'

export type Notice = { title: string; description?: string; variant?: 'success' | 'error' | 'info' }
export const NotificationContext = createContext<((notice: Notice) => void) | null>(null)

export function useNotification() {
  const notify = useContext(NotificationContext)
  if (!notify) throw new Error('useNotification must be used within NotificationProvider.')
  return notify
}
