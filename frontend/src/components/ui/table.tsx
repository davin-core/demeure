import type { ComponentProps } from 'react'
import { cn } from '@/lib/utils'

export function Table({ className, scrollLabel = 'Scrollable table', ...props }: ComponentProps<'table'> & { scrollLabel?: string }) {
  return <div role="region" aria-label={scrollLabel} tabIndex={0} className="w-full overflow-x-auto focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring"><table className={cn('w-full caption-bottom text-left text-sm', className)} {...props} /></div>
}
export function TableHeader(props: ComponentProps<'thead'>) {
  return <thead className="bg-muted/60 text-xs text-muted-foreground" {...props} />
}
export function TableBody(props: ComponentProps<'tbody'>) {
  return <tbody {...props} />
}
export function TableRow({ className, ...props }: ComponentProps<'tr'>) {
  return <tr className={cn('border-b border-border transition-colors last:border-0 hover:bg-muted/40', className)} {...props} />
}
export function TableHead({ className, scope = 'col', ...props }: ComponentProps<'th'>) {
  return <th scope={scope} className={cn('h-12 px-5 font-medium whitespace-nowrap', className)} {...props} />
}
export function TableCell({ className, ...props }: ComponentProps<'td'>) {
  return <td className={cn('px-5 py-4', className)} {...props} />
}
export function TableCaption({ className, ...props }: ComponentProps<'caption'>) {
  return <caption className={cn('px-5 py-3 text-left text-xs text-muted-foreground', className)} {...props} />
}
