import { Fragment } from 'react'
import { Icon } from '@/components/ui/icon'

export type BreadcrumbItem = { label: string; href?: string }

export function Breadcrumbs({ items }: { items: BreadcrumbItem[] }) {
  return <nav aria-label="Breadcrumb"><ol className="flex min-w-0 flex-wrap items-center gap-2 text-xs text-muted-foreground">
    {items.map((item, index) => <Fragment key={`${item.label}-${index}`}>
      {index > 0 && <li aria-hidden="true"><Icon name="chevron" className="size-3 -rotate-90" /></li>}
      <li className="min-w-0 break-words">{index === items.length - 1
        ? <span aria-current="page" className="text-foreground">{item.label}</span>
        : item.href ? <a href={item.href} className="hover:text-primary">{item.label}</a> : <span>{item.label}</span>}</li>
    </Fragment>)}
  </ol></nav>
}
