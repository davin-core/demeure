import { useCallback, useRef, useState, type ReactNode } from 'react'
import { Icon } from '@/components/ui/icon'
import { NotificationContext, type Notice } from '@/components/ui/use-notification'
import { cn } from '@/lib/utils'

type Entry = Notice & { id: number }

export function NotificationProvider({ children }: { children: ReactNode }) {
  const [entries, setEntries] = useState<Entry[]>([])
  const nextId = useRef(0)
  const notify = useCallback((notice: Notice) => {
    const entry = { ...notice, id: ++nextId.current }
    setEntries((current) => [...current, entry])
  }, [])
  return <NotificationContext.Provider value={notify}>
    {children}
    <div aria-label="Notifications" className="fixed right-4 bottom-4 left-4 z-[60] flex max-h-[calc(100dvh_-_2rem)] flex-col gap-3 overflow-y-auto sm:left-auto sm:w-96">
      {entries.map((entry) => <div key={entry.id}
        className="flex items-start gap-3 rounded-lg border border-border bg-card p-4 text-card-foreground shadow-lg">
        <Icon name={entry.variant === 'success' ? 'check' : 'info'} className={cn('mt-1 size-5 shrink-0',
          entry.variant === 'error' ? 'text-destructive' : entry.variant === 'success' ? 'text-success' : 'text-info')} />
        <div className="min-w-0 flex-1" role={entry.variant === 'error' ? 'alert' : 'status'}>
          <p className="text-sm font-semibold">{entry.title}</p>
          {entry.description && <p className="mt-1 text-xs leading-relaxed text-muted-foreground">{entry.description}</p>}
        </div>
        <button type="button" aria-label={`Dismiss ${entry.title}`} onClick={() => setEntries((current) => current.filter((notice) => notice.id !== entry.id))}
          className="-mt-2 -mr-2 inline-flex size-11 shrink-0 items-center justify-center rounded-md hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
          <Icon name="close" className="size-4" />
        </button>
      </div>)}
    </div>
  </NotificationContext.Provider>
}
