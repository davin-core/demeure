import type { ComponentProps } from 'react'
import * as SelectPrimitive from '@radix-ui/react-select'
import { Icon } from '@/components/ui/icon'
import { controlStyles } from '@/components/ui/input'
import { cn } from '@/lib/utils'

export const Select = SelectPrimitive.Root
export const SelectValue = SelectPrimitive.Value

export function SelectTrigger({ className, children, ...props }: ComponentProps<typeof SelectPrimitive.Trigger>) {
  return <SelectPrimitive.Trigger className={cn(controlStyles, 'flex h-11 items-center justify-between gap-2 text-left data-[placeholder]:text-muted-foreground', className)} {...props}>
    {children}<SelectPrimitive.Icon><Icon name="chevron" className="size-4" /></SelectPrimitive.Icon>
  </SelectPrimitive.Trigger>
}
export function SelectContent({ children, className, ...props }: ComponentProps<typeof SelectPrimitive.Content>) {
  return <SelectPrimitive.Portal><SelectPrimitive.Content position="popper" sideOffset={6}
    className={cn('z-50 max-h-[var(--radix-select-content-available-height)] min-w-[var(--radix-select-trigger-width)] overflow-y-auto rounded-md border border-border bg-popover p-1 text-popover-foreground shadow-lg', className)} {...props}>
    <SelectPrimitive.Viewport>{children}</SelectPrimitive.Viewport>
  </SelectPrimitive.Content></SelectPrimitive.Portal>
}
export function SelectItem({ children, className, ...props }: ComponentProps<typeof SelectPrimitive.Item>) {
  return <SelectPrimitive.Item className={cn('relative flex min-h-10 cursor-pointer items-center rounded-sm py-2 pr-8 pl-3 text-sm outline-none data-[highlighted]:bg-muted data-[disabled]:pointer-events-none data-[disabled]:opacity-50', className)} {...props}>
    <SelectPrimitive.ItemText>{children}</SelectPrimitive.ItemText>
    <SelectPrimitive.ItemIndicator className="absolute right-2"><Icon name="check" className="size-4" /></SelectPrimitive.ItemIndicator>
  </SelectPrimitive.Item>
}
