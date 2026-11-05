import { useEffect, useRef, useState, type ReactNode } from 'react'
import { AppShell } from '@/components/layout/navigation'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from '@/components/ui/dialog'
import { Field } from '@/components/ui/field'
import { Icon } from '@/components/ui/icon'
import { Input, Textarea } from '@/components/ui/input'
import { NotificationProvider } from '@/components/ui/notifications'
import { Pagination } from '@/components/ui/pagination'
import { useNotification } from '@/components/ui/use-notification'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCaption, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

const sections = [
  { label: 'Foundations', href: '#foundations' },
  { label: 'Actions', href: '#actions' },
  { label: 'Forms', href: '#forms' },
  { label: 'Surfaces & data', href: '#surfaces' },
  { label: 'Feedback', href: '#feedback' },
]
const palette = [
  { name: 'Ivory', value: '#F4F4ED', token: 'background', className: 'bg-background' },
  { name: 'Forest', value: '#26352E', token: 'foreground', className: 'bg-foreground' },
  { name: 'Evergreen', value: '#34745D', token: 'primary', className: 'bg-primary' },
  { name: 'Lime', value: '#D7ED76', token: 'accent', className: 'bg-accent' },
  { name: 'Porcelain', value: '#FFFFFC', token: 'card', className: 'bg-card' },
  { name: 'Stone', value: '#D6D8CE', token: 'border', className: 'bg-border' },
]
const componentRows = [
  { name: 'Button', purpose: 'Clear, intentional actions', status: 'Interactive' },
  { name: 'Field', purpose: 'Labels, guidance and validation', status: 'Accessible' },
  { name: 'Dialog', purpose: 'Focused decisions without losing context', status: 'Interactive' },
  { name: 'Table', purpose: 'Readable, responsive structured data', status: 'Accessible' },
]

function Section({ id, number, title, description, children }: {
  id: string; number: string; title: string; description: string; children: ReactNode
}) {
  return <section id={id} aria-labelledby={`${id}-title`} className="scroll-mt-8 border-t border-border pt-10">
    <div className="mb-7 flex items-start gap-4">
      <span className="mt-1.5 font-mono text-xs text-muted-foreground">{number}</span>
      <div><h2 id={`${id}-title`} className="text-xl font-semibold tracking-tight">{title}</h2>
        <p className="mt-2 max-w-xl text-sm leading-relaxed text-muted-foreground">{description}</p></div>
    </div>
    {children}
  </section>
}

function Gallery() {
  const notify = useNotification()
  const [activeHref, setActiveHref] = useState(window.location.hash || '#foundations')
  const [email, setEmail] = useState('')
  const emailInput = useRef<HTMLInputElement>(null)
  const [tone, setTone] = useState('evergreen')
  const [checked, setChecked] = useState(true)
  const [submitted, setSubmitted] = useState(false)
  const [formSaved, setFormSaved] = useState(false)
  const [sortAscending, setSortAscending] = useState(true)
  const [tablePage, setTablePage] = useState(1)
  const [dialogOpen, setDialogOpen] = useState(false)
  const [dialogName, setDialogName] = useState('Demeure workspace')
  const isEmailValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)
  const emailError = submitted && !isEmailValid ? 'Enter a valid email address to preview this form.' : undefined

  useEffect(() => {
    const update = () => setActiveHref(window.location.hash || '#foundations')
    window.addEventListener('hashchange', update)
    const previous = document.title
    document.title = 'Demeure | Design system'
    return () => { window.removeEventListener('hashchange', update); document.title = previous }
  }, [])

  return <AppShell items={sections} activeHref={activeHref}>
    <div className="mb-12 grid gap-8 xl:grid-cols-[1fr_240px] xl:items-end">
      <div>
        <p className="mb-5 text-[10px] font-semibold tracking-[0.16em] text-muted-foreground uppercase">Demeure design system / Edition 01</p>
        <h1 className="max-w-2xl font-display text-[clamp(2.75rem,5vw,4.5rem)] leading-[1.02] tracking-[-0.025em]">A quieter kind<br />of confidence.</h1>
        <p className="mt-6 max-w-lg text-base leading-relaxed text-muted-foreground">Thoughtful foundations for the places people call home. Warm surfaces, precise details, and clarity at every step.</p>
        <div className="mt-7 flex flex-wrap items-center gap-3">
          <Button asChild variant="accent"><a href="#foundations">Explore the system <Icon name="arrow" /></a></Button>
          <Badge>Version 1.0</Badge>
        </div>
      </div>
      <div className="relative hidden overflow-hidden rounded-lg border border-border bg-muted/40 p-6 xl:block">
        <div aria-hidden="true" className="absolute inset-0 bg-[linear-gradient(to_right,var(--border)_1px,transparent_1px)] bg-size-[40px_100%] opacity-50" />
        <div className="relative"><span className="grid size-14 place-items-center rounded-full bg-accent font-display text-3xl">D</span>
          <p className="mt-8 font-display text-2xl leading-tight">Built around<br />human spaces.</p>
          <p className="mt-4 font-mono text-[9px] tracking-wider text-muted-foreground">MINIMAL / NOT IMPERSONAL</p></div>
      </div>
    </div>
    <div className="mb-10 flex flex-wrap items-center gap-2 border-y border-border py-4 text-xs text-muted-foreground">
      <Icon name="info" className="size-4" />
      <p>Interactive component examples only. Nothing here creates accounts, saves settings, or changes property data.</p>
    </div>
    <div className="space-y-12">
      <Section id="foundations" number="01" title="Foundations" description="An architectural palette: natural light, grounded greens, and a small spark of optimism.">
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">
          {palette.map((color) => <Card key={color.name} className="overflow-hidden">
            <div aria-hidden="true" className={`h-20 border-b border-border ${color.className}`} />
            <div className="p-3"><p className="text-sm font-medium">{color.name}</p><p className="mt-1 font-mono text-[10px] text-muted-foreground">{color.value}</p></div>
          </Card>)}
        </div>
        <div className="mt-6 grid gap-4 xl:grid-cols-2">
          <Card><CardHeader><CardTitle>Typography with a point of view</CardTitle><CardDescription>Editorial moments meet practical everyday reading.</CardDescription></CardHeader>
            <CardContent className="space-y-5">
              <div><p className="mb-2 font-mono text-[10px] text-muted-foreground">DISPLAY / GEORGIA</p><p className="font-display text-4xl tracking-tight">Room to breathe.</p></div>
              <div><p className="mb-2 font-mono text-[10px] text-muted-foreground">INTERFACE / APTOS / SEGOE UI</p><p className="text-sm leading-relaxed">Clear labels. Comfortable reading. Purposeful hierarchy.</p></div>
              <p className="font-mono text-[11px] text-muted-foreground">METADATA / CASCADIA CODE / CONSOLAS</p>
            </CardContent>
          </Card>
          <Card><CardHeader><CardTitle>Rhythm & edges</CardTitle><CardDescription>A four-pixel rhythm. Restrained curves, never decorative noise.</CardDescription></CardHeader>
            <CardContent><div className="flex flex-wrap items-end gap-5">
              {[4, 8, 16, 24, 32, 48].map((space) => <div key={space} className="text-center"><div className="mx-auto w-5 rounded-sm bg-primary/70" style={{ height: space }} /><p className="mt-3 font-mono text-[10px] text-muted-foreground">{space}px</p></div>)}
            </div><div className="mt-7 flex flex-wrap gap-4">
              {[{ label: '2px', css: 'rounded-sm' }, { label: '4px', css: 'rounded-md' }, { label: '6px', css: 'rounded-lg' }, { label: '10px', css: 'rounded-xl' }].map((radius) => <div key={radius.label} className={`grid size-12 place-items-center border border-border bg-muted/40 font-mono text-[10px] text-muted-foreground ${radius.css}`}>{radius.label}</div>)}
            </div></CardContent>
          </Card>
        </div>
      </Section>
      <Section id="actions" number="02" title="Purposeful actions" description="One clear primary action. Secondary choices stay quiet; important moments deserve a little lime.">
        <Card><CardContent className="space-y-6 pt-6">
          <div className="flex flex-wrap gap-3">
            <Button onClick={() => notify({ title: 'Primary action preview', description: 'A confirmation of the component interaction.', variant: 'success' })}>Primary action <Icon name="arrow" /></Button>
            <Button variant="accent" onClick={() => notify({ title: 'Accent action preview', variant: 'info' })}>Accent action</Button>
            <Button variant="outline" onClick={() => notify({ title: 'Secondary action preview' })}>Outline</Button>
            <Button variant="secondary" onClick={() => notify({ title: 'Quiet action preview' })}>Secondary</Button>
            <Button variant="ghost" onClick={() => notify({ title: 'Ghost action preview' })}>Ghost</Button>
            <Button variant="destructive" onClick={() => notify({ title: 'Destructive style preview', description: 'No records were deleted.', variant: 'error' })}>Destructive</Button>
          </div>
          <div className="flex flex-wrap items-center gap-3 border-t border-border pt-5">
            <Button size="sm" variant="outline" onClick={() => notify({ title: 'Small button preview' })}>Small</Button>
            <Button size="lg" variant="outline" onClick={() => notify({ title: 'Large button preview' })}>Comfortable</Button>
            <Button variant="outline" size="icon" className="size-11" aria-label="Preview notification" onClick={() => notify({ title: 'An accessible icon action', variant: 'info' })}><Icon name="bell" /></Button>
            <Button disabled>Unavailable</Button>
            <Button variant="link" asChild><a href="#forms">View form patterns</a></Button>
          </div>
        </CardContent></Card>
      </Section>
      <Section id="forms" number="03" title="Forms that feel considered" description="Visible labels, helpful guidance, and specific errors. Not a guessing game.">
        <div className="grid gap-5 xl:grid-cols-[1.2fr_1fr]">
          <Card><CardHeader><CardTitle>Example preferences</CardTitle><CardDescription>A local-only preview. Try submitting an invalid email.</CardDescription></CardHeader>
            <form noValidate onSubmit={(event) => { event.preventDefault(); setSubmitted(true); setFormSaved(false)
              if (isEmailValid) { setFormSaved(true); notify({ title: 'Form preview complete', description: 'Your example values were validated locally. Nothing was saved.', variant: 'success' }) }
              else emailInput.current?.focus()
            }}>
              <CardContent className="space-y-5">
                <Field label="Email address" required description="Used only for this component demonstration." error={emailError}>{(props) => <Input {...props} ref={emailInput} required type="email" value={email} onChange={(event) => { setEmail(event.target.value); setFormSaved(false) }} placeholder="you@example.com" autoComplete="off" />}</Field>
                <Field label="Interface tone">{(props) => <Select value={tone} onValueChange={setTone}>
                  <SelectTrigger {...props}><SelectValue /></SelectTrigger>
                  <SelectContent><SelectItem value="evergreen">Evergreen</SelectItem><SelectItem value="ivory">Warm ivory</SelectItem><SelectItem value="lime">Lime accent</SelectItem></SelectContent>
                </Select>}</Field>
                <Field label="Notes" description="A comfortable space for longer thoughts.">{(props) => <Textarea {...props} placeholder="What makes a space feel like home?" />}</Field>
                <div className="flex items-center gap-3"><Checkbox id="preview-reminders" checked={checked} onCheckedChange={(value) => setChecked(value === true)} /><label htmlFor="preview-reminders" className="text-sm">Show example reminders</label></div>
                {formSaved && <Alert variant="success"><AlertTitle>Preview validated</AlertTitle><AlertDescription>No settings were persisted.</AlertDescription></Alert>}
              </CardContent>
              <CardFooter><Button type="submit">Validate example <Icon name="check" /></Button><p className="text-xs text-muted-foreground">No network request</p></CardFooter>
            </form>
          </Card>
          <div className="space-y-5">
            <Card><CardHeader><CardTitle>Readable states</CardTitle><CardDescription>State is communicated with text, not color alone.</CardDescription></CardHeader>
              <CardContent className="space-y-5">
                <Field label="Disabled field" description="Unavailable until the relevant workflow allows it.">{(props) => <Input {...props} disabled value="Not editable in this example" />}</Field>
                <Field label="Error-state example" error="This field shows the validation style.">{(props) => <Input {...props} defaultValue="Needs attention" />}</Field>
              </CardContent>
            </Card>
            <div className="rounded-lg bg-muted/50 p-6"><p className="font-display text-2xl">Less friction.<br />More understanding.</p><p className="mt-3 text-sm leading-relaxed text-muted-foreground">Always explain what is needed, why it matters, and how to move forward.</p></div>
          </div>
        </div>
      </Section>
      <Section id="surfaces" number="04" title="A place for everything" description="Light surfaces, clear boundaries, and tables that give information room to breathe.">
        <div className="mb-5 grid gap-4 sm:grid-cols-2">
          <Card><CardHeader><div className="mb-2 flex items-center justify-between"><Icon name="layers" className="text-primary" /><Badge variant="success">Reusable</Badge></div><CardTitle>Calm by default</CardTitle><CardDescription>Cards group related content. Shadows stay subtle; borders do most of the work.</CardDescription></CardHeader><CardFooter><Button variant="link" asChild className="h-auto p-0"><a href="#feedback">Explore feedback <Icon name="arrow" /></a></Button></CardFooter></Card>
          <Card className="bg-accent/25"><CardHeader><Icon name="grid" className="mb-2 text-primary" /><CardTitle>Flexible by design</CardTitle><CardDescription>Spacing and shared tokens hold the system together across every screen size.</CardDescription></CardHeader><CardFooter className="border-foreground/10"><span className="font-mono text-[10px] text-muted-foreground">320PX AND UP / RESPONSIVE</span></CardFooter></Card>
        </div>
        <Card className="overflow-hidden"><Table>
          <TableCaption>Component-library examples, not rental or resident records. Scroll horizontally on smaller screens.</TableCaption>
          <TableHeader><TableRow><TableHead aria-sort={sortAscending ? 'ascending' : 'descending'}><button type="button" className="inline-flex min-h-11 items-center gap-2 rounded-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onClick={() => { setSortAscending((value) => !value); setTablePage(1) }}>Component <Icon name="chevron" className={`size-3 ${sortAscending ? 'rotate-180' : ''}`} /><span className="sr-only">, sort {sortAscending ? 'descending' : 'ascending'}</span></button></TableHead><TableHead>Purpose</TableHead><TableHead>Pattern</TableHead></TableRow></TableHeader>
          <TableBody>{[...componentRows].sort((a, b) => sortAscending ? a.name.localeCompare(b.name) : b.name.localeCompare(a.name)).slice((tablePage - 1) * 2, tablePage * 2).map((row) => <TableRow key={row.name}><TableCell className="font-medium">{row.name}</TableCell><TableCell className="min-w-60 text-muted-foreground">{row.purpose}</TableCell><TableCell><Badge variant={row.status === 'Accessible' ? 'success' : 'default'}>{row.status}</Badge></TableCell></TableRow>)}</TableBody>
        </Table></Card>
        <div className="mt-4"><Pagination page={tablePage} pageSize={2} totalCount={componentRows.length} onPageChange={setTablePage} /></div>
      </Section>
      <Section id="feedback" number="05" title="Keep people in the loop" description="Honest status, gentle guidance, and focused dialogs. Every message has a purpose.">
        <div className="grid gap-4 sm:grid-cols-2">
          <Alert variant="success"><AlertTitle>Everything is in place</AlertTitle><AlertDescription>Success should feel reassuring, not distracting.</AlertDescription></Alert>
          <Alert variant="warning"><AlertTitle>A little attention needed</AlertTitle><AlertDescription>Explain the next step before a problem becomes a blocker.</AlertDescription></Alert>
          <Alert variant="info"><AlertTitle>A helpful detail</AlertTitle><AlertDescription>Give context right where people need it.</AlertDescription></Alert>
          <Alert variant="destructive"><AlertTitle>Something needs correcting</AlertTitle><AlertDescription>Describe the issue clearly and offer a way forward.</AlertDescription></Alert>
        </div>
        <Card className="mt-5"><CardHeader><CardTitle>Focused, never disconnected</CardTitle><CardDescription>Dialogs trap focus, close on Escape, and return you to where you started. Notifications remain until dismissed.</CardDescription></CardHeader>
          <CardContent className="flex flex-wrap gap-3">
            <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
              <DialogTrigger asChild><Button variant="outline"><Icon name="plus" />Open example dialog</Button></DialogTrigger>
              <DialogContent><Badge variant="accent" className="mb-5">Component preview</Badge><DialogTitle>A space of your own.</DialogTitle><DialogDescription>This is an example dialog, not a workspace creation flow. Your changes stay in this browser session only.</DialogDescription>
                <form className="mt-6 space-y-6" onSubmit={(event) => { event.preventDefault(); setDialogOpen(false); notify({ title: 'Dialog preview complete', description: 'No workspace was created.', variant: 'success' }) }}>
                  <Field label="Example workspace name" required>{(props) => <Input {...props} required value={dialogName} onChange={(event) => setDialogName(event.target.value)} />}</Field>
                  <div className="flex flex-wrap justify-end gap-3"><DialogClose asChild><Button variant="outline">Cancel</Button></DialogClose><Button type="submit">Confirm preview <Icon name="arrow" /></Button></div>
                </form>
              </DialogContent>
            </Dialog>
            <Button variant="secondary" onClick={() => notify({ title: 'A useful update', description: 'Persistent, dismissible feedback. No example data was saved.', variant: 'success' })}><Icon name="bell" />Preview notification</Button>
          </CardContent>
        </Card>
      </Section>
    </div>
  </AppShell>
}

export default function DesignSystem() {
  return <NotificationProvider><Gallery /></NotificationProvider>
}
