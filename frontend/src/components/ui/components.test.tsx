import { renderToStaticMarkup } from 'react-dom/server'
import { describe, expect, it } from 'vitest'
import { Brand } from '@/components/layout/brand'
import { Navigation } from '@/components/layout/navigation'
import { Alert, AlertDescription, AlertTitle } from './alert'
import { Badge } from './badge'
import { Button } from './button'
import { Card, CardTitle } from './card'
import { Checkbox } from './checkbox'
import { Dialog, DialogTrigger } from './dialog'
import { Field } from './field'
import { Input, Textarea } from './input'
import { NotificationProvider } from './notifications'
import { Select, SelectTrigger, SelectValue } from './select'
import { Table, TableCaption, TableHead, TableHeader, TableRow } from './table'
import { useNotification } from './use-notification'

describe('design-system component contracts', () => {
  it('defaults buttons to non-submit and preserves disabled and accent states', () => {
    const html = renderToStaticMarkup(<Button variant="accent" disabled>Preview</Button>)
    expect(html).toContain('type="button"')
    expect(html).toContain('disabled=""')
    expect(html).toContain('bg-accent')
  })

  it('supports real links without adding a button type', () => {
    const html = renderToStaticMarkup(<Button asChild><a href="/design-system">Explore</a></Button>)
    expect(html).toContain('<a ')
    expect(html).toContain('href="/design-system"')
    expect(html).not.toContain('type="button"')
  })

  it('connects field labels, descriptions and errors to their control', () => {
    const html = renderToStaticMarkup(<Field label="Email" description="Help text" error="Invalid email" required>
      {(props) => <Input {...props} type="email" required />}
    </Field>)
    const controlId = html.match(/<input[^>]*\sid="([^"]+)"/)?.[1]
    expect(controlId).toBeTruthy()
    expect(html).toContain(`for="${controlId}"`)
    expect(html).toContain(`aria-describedby="${controlId}-description ${controlId}-error"`)
    expect(html).toContain('aria-invalid="true"')
    expect(html).toContain('role="alert"')
    expect(html).toContain('required=""')
  })

  it('does not mark fields without errors invalid or add absent guidance', () => {
    const html = renderToStaticMarkup(<Field label="Notes">{(props) => <Textarea {...props} />}</Field>)
    expect(html).toContain('aria-invalid="false"')
    expect(html).not.toContain('aria-describedby=')
    expect(html).not.toContain('role="alert"')
  })

  it('uses polite feedback and urgent errors with overridable roles', () => {
    expect(renderToStaticMarkup(<Alert><AlertTitle>Info</AlertTitle><AlertDescription>Details</AlertDescription></Alert>)).toContain('role="status"')
    expect(renderToStaticMarkup(<Alert variant="destructive">Error</Alert>)).toContain('role="alert"')
    expect(renderToStaticMarkup(<Alert variant="destructive" role="note">Example</Alert>)).toContain('role="note"')
  })

  it('retains table semantics and keyboard-accessible overflow', () => {
    const html = renderToStaticMarkup(<Table scrollLabel="Component inventory"><TableCaption>Examples</TableCaption><TableHeader><TableRow><TableHead aria-sort="ascending">Name</TableHead></TableRow></TableHeader></Table>)
    expect(html).toContain('role="region"')
    expect(html).toContain('aria-label="Component inventory"')
    expect(html).toContain('tabindex="0"')
    expect(html).toContain('scope="col"')
    expect(html).toContain('aria-sort="ascending"')
    expect(html).toContain('<caption')
  })

  it('provides a labeled landmark and identifies the current navigation item', () => {
    const html = renderToStaticMarkup(<Navigation items={[{ label: 'Forms', href: '#forms' }, { label: 'Actions', href: '#actions' }]} activeHref="#forms" />)
    expect(html).toContain('aria-label="Main navigation"')
    expect(html.match(/aria-current="page"/g)).toHaveLength(1)
    expect(html).toContain('href="#forms"')
  })

  it('renders the correct product name and home destination', () => {
    const html = renderToStaticMarkup(<Brand />)
    expect(html).toContain('aria-label="Demeure home"')
    expect(html).toContain('href="/"')
    expect(html).toContain('demeure')
    expect(html).not.toContain('Denmeure')
  })

  it('renders card heading and semantic badge styling', () => {
    const html = renderToStaticMarkup(<Card><CardTitle>Reusable</CardTitle><Badge variant="success">Available</Badge></Card>)
    expect(html).toContain('<h3')
    expect(html).toContain('text-success')
    expect(html).toContain('data-slot="card"')
  })

  it('preserves Radix checkbox mixed and disabled states', () => {
    const html = renderToStaticMarkup(<Checkbox checked="indeterminate" disabled aria-label="Select all" />)
    expect(html).toContain('aria-checked="mixed"')
    expect(html).toContain('data-state="indeterminate"')
    expect(html).toContain('disabled=""')
  })

  it('connects select and dialog triggers to their accessible primitives', () => {
    const select = renderToStaticMarkup(<Select value="green"><SelectTrigger aria-label="Tone"><SelectValue /></SelectTrigger></Select>)
    expect(select).toContain('role="combobox"')
    expect(select).toContain('aria-expanded="false"')
    const dialog = renderToStaticMarkup(<Dialog><DialogTrigger>Open preview</DialogTrigger></Dialog>)
    expect(dialog).toContain('aria-haspopup="dialog"')
    expect(dialog).toContain('aria-expanded="false"')
  })

  it('fails explicitly when a notification consumer has no provider', () => {
    function Consumer() { useNotification(); return null }
    expect(() => renderToStaticMarkup(<Consumer />)).toThrow('useNotification must be used within NotificationProvider.')
    expect(() => renderToStaticMarkup(<NotificationProvider><Consumer /></NotificationProvider>)).not.toThrow()
  })
})
