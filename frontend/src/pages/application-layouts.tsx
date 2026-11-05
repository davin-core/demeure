import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { PublicLayout } from '@/components/layout/public-layout'
import { SessionProvider } from '@/components/layout/session-provider'
import { AdministrationLayout, ContractorLayout, LandlordLayout, TenantLayout } from '@/components/layout/workspace-layout'
import { workspaceDefinitions, type WorkspaceKind } from '@/components/layout/permissions'

function LayoutLanding({ kind }: { kind: WorkspaceKind | 'public' }) {
  const title = kind === 'public' ? 'Public website' : workspaceDefinitions[kind].title
  return <div className="space-y-8">
    <div><p className="mb-5 text-[10px] font-semibold tracking-[0.16em] text-muted-foreground uppercase">Demeure / Application foundation</p>
      <h1 className="font-display text-[clamp(2.5rem,5vw,4rem)] leading-tight tracking-tight">{title}</h1>
      <p className="mt-4 max-w-xl text-base leading-relaxed text-muted-foreground">{kind === 'public' ? 'A welcoming frame for the Demeure website.' : workspaceDefinitions[kind].description}</p>
    </div>
    <Card className="max-w-2xl"><CardHeader><Badge variant="accent" className="w-fit">Layout ready</Badge><CardTitle>A foundation, not a business page.</CardTitle>
      <CardDescription>This route demonstrates the application layout. Individual business pages have not been implemented.</CardDescription></CardHeader>
      <CardContent><p className="text-sm leading-relaxed text-muted-foreground">{kind === 'public'
        ? 'Public content can be added inside this layout without changing the shared navigation and footer.'
        : 'Navigation reflects your current permissions. Coming-soon items are deliberately unavailable; no business records are queried or displayed.'}</p></CardContent>
    </Card>
  </div>
}

export default function ApplicationLayouts({ kind }: { kind: WorkspaceKind | 'public' }) {
  const content = <LayoutLanding kind={kind} />
  return <SessionProvider workspace={kind === 'public' ? undefined : kind}>{kind === 'public' ? <PublicLayout>{content}</PublicLayout>
    : kind === 'landlord' ? <LandlordLayout>{content}</LandlordLayout>
    : kind === 'tenant' ? <TenantLayout>{content}</TenantLayout>
    : kind === 'contractor' ? <ContractorLayout>{content}</ContractorLayout>
    : <AdministrationLayout>{content}</AdministrationLayout>}
  </SessionProvider>
}
