# Demeure application layouts

## Scope and routes

These are reusable page frames, not business screens. Each landing route explains
that its layout is ready and no business pages have been implemented. There are
no seeded users, property statistics, fake notification records or new business
API endpoints.

| Route | Layout | Access |
| --- | --- | --- |
| `/public` | Public website header, content slot, footer, mobile drawer | Everyone; authenticated workspace links are permission-aware |
| `/landlord` | Landlord dashboard sidebar and top bar | OrganizationOwner or PropertyManager in the selected organization |
| `/tenant` | Tenant portal sidebar and top bar | Tenant in the selected organization |
| `/contractor` | Contractor portal sidebar and top bar | Contractor in the selected organization |
| `/administration` | Administration sidebar and top bar | Exact global SuperAdministrator role |

Trailing slashes are supported. `/` remains the real developer API workspace;
`/design-system` remains the component gallery. Unknown paths preserve the
existing workspace fallback; nested business routes are not registered.
The [integration stage](frontend-integration.md) adds `/login` and sign-in links
to anonymous portal gates, plus shared transport and API-triggered session-expiry
handling; this preserves the layout-only scope for business routes.
Production hosting must serve `index.html` for frontend paths while preserving
API routing.

The [route dispatcher](../frontend/src/app-router.tsx) lazy-loads the application
layouts. Normal anchors provide navigation; a routing library is still
unnecessary for five layout landing routes. Add nested routing when requested
business screens need it.

## Session and authorization boundaries

[session-api.ts](../frontend/src/lib/session-api.ts) uses the existing canonical
`GET /api/v1/auth/session` DTO. It validates account fields, GUIDs, memberships
and duplicate organization identifiers before exposing a session. Unknown
membership roles are accepted as data but grant no portal access. Ordinary
Members legitimately receive an empty role string from the backend.

Session requests use same-origin cookies and `cache: "no-store"`. The existing
`VITE_API_BASE_URL` may be `/api` or already versioned `/api/v1`; session URLs do
not duplicate the version. An absolute same-origin base is supported, but
cross-origin cookie configuration is explicitly rejected. Keep the frontend and
API on one origin through the development proxy or production reverse proxy.
The original health client behavior is unchanged.

[SessionProvider](../frontend/src/components/layout/session-provider.tsx):

- Loads the real session on entry and revalidates on browser-window focus.
- Shows explicit loading, anonymous and error states. Only 401 means anonymous;
  network, timeout, malformed response, 403 and server failures remain errors.
- Uses a ten-second request timeout, cancellation on unmount and cancellation of
  superseded operations. An aborted older response cannot restore stale access.
- Hides protected navigation and unmounts protected content during verification,
  failures and sign-out; no success-shaped fallback or cached business data.
- Keeps identity, tokens and organization context out of localStorage.

Frontend permission checks are **UX controls, not authoritative authorization**.
They never replace backend live membership checks, assignment/self-service
predicates, organization headers or PostgreSQL RLS. Direct API identifier changes
remain subject to the existing [authorization](authorization.md).

### Organization selection

As requested, the first eligible membership is automatically selected when a
portal is entered. Selection remains stable until explicitly changed, even if
membership ordering changes. The selector lists active memberships from the
session; organizations belonging to a different portal role are disabled and
labeled accordingly.

Switching re-fetches the session before rendering authorized content. A selected
organization that was removed or lost its required role produces an access
error, **not an automatic substitution with another organization**. The user can
select a remaining authorized organization to recover. A new identity or
successful logout clears the previous selection.

Selection is currently in memory for the current layout route; normal full-page
navigation reloads the session and selects that portal's first eligible
membership. There is no organization list/search endpoint, browser persistence,
membership management or server impersonation switch.

Authorized organization content receives the selected `Membership` through
[useOrganization](../frontend/src/components/layout/organization-context.ts).
Future resource callers must use its `organizationId` as `X-Organization-Id` and
include it in any organization-scoped cache keys. Clear data on context changes;
do not let session-wide role names authorize another organization.

```tsx
function OrganizationPage() {
  const membership = useOrganization()
  // Future real API calls must select this membership.organizationId explicitly.
  return <h1>{membership.organizationName}</h1>
}

<SessionProvider workspace="landlord">
  <LandlordLayout breadcrumbs={[
    { label: 'Demeure', href: '/public' },
    { label: 'Landlord dashboard' },
  ]}>
    <OrganizationPage />
  </LandlordLayout>
</SessionProvider>
```

The hook throws if no authorized organization layout provides it.
Administration deliberately does not provide an organization context. Its
platform scope grants no automatic organization access or impersonation; future
platform reads still require the separate audited, reason-required API actions.

## Navigation and menus

The shared [workspace layout](../frontend/src/components/layout/workspace-layout.tsx)
exports `LandlordLayout`, `TenantLayout`, `ContractorLayout` and
`AdministrationLayout`. Each accepts children and optional breadcrumb items.
[PublicLayout](../frontend/src/components/layout/public-layout.tsx) shares the
brand, breadcrumbs and account controls but remains accessible anonymously.

- **Sidebar:** desktop from 1024px; mobile modal drawer below 1024px. Real links
  identify the current route, and the drawer closes on navigation.
- **Coming-soon items:** permitted future destinations are visible but have no
  href, click handler or keyboard tab stop. They are labeled unavailable and do
  not create business routes.
- **Landlord:** properties, units, tenants, applications, leases, payments and
  maintenance. PropertyManager wording specifies assigned-property scope.
- **Tenant:** only own tenancy, applications, leases, payments and maintenance.
- **Contractor:** only assigned maintenance jobs.
- **Administration:** organizations, users and audited platform access are
  disabled future destinations; they do not imply new platform permissions.
- **Profile disclosure:** real display name/email, initials derived from the
  real name, authorized workspace links and functional sign-out. Profile editing
  is explicitly unavailable.
- **Notifications disclosure:** states the feed is not available yet. No
  fake empty-feed claim, unread count, polling or fabricated notifications.
- **Breadcrumbs:** labeled navigation landmark with only the final item marked
  current; caller-provided optional real links.

Account popups use native `details`/`summary` disclosures, not ARIA application
menus. Their content uses normal links and buttons with native Tab behavior;
Enter/Space, Escape/focus restoration, outside click and focus departure close
behavior are supported. This avoids an additional dropdown dependency. Mobile
popup bounds are viewport-constrained. Radix Dialog continues to own mobile
drawer focus trapping and Escape behavior.

### Sign-out

Every sign-out obtains a new `GET /api/v1/auth/csrf` token, then submits
`POST /api/v1/auth/logout` with the paired cookie and `X-CSRF-TOKEN`. Only the
documented 204 is success. Errors are shown explicitly and protected content is
not restored from stale state; retry revalidates the session. A logout 401 shows
session-expired information. On success, the account menu/organization content
are removed and the sign-in-required state is shown.

CSRF tokens are neither cached nor persisted, so every subsequent unsafe
operation must bootstrap its own fresh token after identity changes. Logout is
the existing current-browser logout, not logout-all or server-side per-session
replay revocation. See [authentication](authentication.md) for cookie semantics.

## Verification and limitations

```powershell
npm run lint --prefix frontend
npm test --prefix frontend
npm run build --prefix frontend
```

Tests cover session/CSRF DTO validation and HTTP failures, same-origin and
versioned configuration, cancellation forwarding, role matrices, direct portal
URL denial, selected-organization context, no organization substitution,
disabled links, public access, breadcrumbs and missing-provider failures.
Final validation: lint passed without warnings, all **96 frontend tests passed**
(53 added for this stage), and the TypeScript/Vite production build passed.
One narrow lint suppression documents the initial asynchronous session request,
which synchronizes an external cookie session rather than deriving UI state.

Browser verification uses intercepted **test-only fixtures** for authenticated
role states; none are part of production components. It covers all five layouts
at 320, 375, 768, 1024 and 1440px without horizontal page overflow, mobile focus
traps, profile/notification popup bounds and Escape, role-aware selection,
membership revocation, stable default selection and fresh-CSRF logout
success/failure. Separately, the real local API/proxy returns 401 and gates all
four anonymous portals, while public content remains usable and the original
workspace reports real healthy API status. No test accounts were created.

Registration/recovery screens, notification delivery/feed, profile editing,
business pages, nested navigation and membership provisioning remain unimplemented
by design. Actual screen-reader and real-device testing are still recommended
before release. Fully authenticated cookie/login verification was subsequently
completed in the [integration stage](frontend-integration.md) against an isolated
temporary database; existing backend Identity integration tests remain unchanged.

## Change inventory

Created:

- [session-api.ts](../frontend/src/lib/session-api.ts) and
  [session-api.test.ts](../frontend/src/lib/session-api.test.ts).
- [permissions.ts](../frontend/src/components/layout/permissions.ts).
- [session-context.ts](../frontend/src/components/layout/session-context.ts) and
  [session-provider.tsx](../frontend/src/components/layout/session-provider.tsx).
- [organization-context.ts](../frontend/src/components/layout/organization-context.ts).
- [disclosure.tsx](../frontend/src/components/layout/disclosure.tsx),
  [account-menus.tsx](../frontend/src/components/layout/account-menus.tsx) and
  [breadcrumbs.tsx](../frontend/src/components/layout/breadcrumbs.tsx).
- [workspace-layout.tsx](../frontend/src/components/layout/workspace-layout.tsx) and
  [public-layout.tsx](../frontend/src/components/layout/public-layout.tsx).
- [application-layouts.tsx](../frontend/src/pages/application-layouts.tsx).
- [layouts.test.tsx](../frontend/src/components/layout/layouts.test.tsx).
- This guide.

Modified:

- [api.ts](../frontend/src/lib/api.ts): share existing configured base URL.
- [app-router.tsx](../frontend/src/app-router.tsx): lazy layout routes.
- [App.tsx](../frontend/src/App.tsx) and [App.css](../frontend/src/App.css):
  public-layout discovery link and responsive footer wrapping.
- [README](../README.md), [design-system guide](design-system.md),
  [authentication guide](authentication.md) and
  [architecture](architecture.md): layout discovery and conventions.

No dependency, backend schema, migration or endpoint changes.
