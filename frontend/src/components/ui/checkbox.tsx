import type { ComponentProps } from 'react'
import * as CheckboxPrimitive from '@radix-ui/react-checkbox'
import { Icon } from '@/components/ui/icon'
import { cn } from '@/lib/utils'

export function Checkbox({ className, ...props }: ComponentProps<typeof CheckboxPrimitive.Root>) {
  return <CheckboxPrimitive.Root className={cn('group relative flex size-5 shrink-0 items-center justify-center rounded-sm border border-input bg-card text-primary-foreground before:absolute before:-inset-3 before:content-[""] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 data-[state=checked]:border-primary data-[state=checked]:bg-primary data-[state=indeterminate]:bg-primary aria-invalid:border-destructive', className)} {...props}>
    <CheckboxPrimitive.Indicator><Icon name="check" className="size-3.5 group-data-[state=indeterminate]:hidden" /><Icon name="minus" className="hidden size-3.5 group-data-[state=indeterminate]:block" /></CheckboxPrimitive.Indicator>
  </CheckboxPrimitive.Root>
}
