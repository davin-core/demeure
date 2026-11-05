# Frontend-backend integration

## Scope

Demeure retains React, TypeScript and Vite with no additional dependencies.
The frontend now has one shared HTTP transport, typed authentication/resource
clients, a real login screen and reusable organization-scoped pagination.
No business list/detail/CRUD pages were added. Registration, verification and
recovery clients are available, but their screens are intentionally not implemented.

Existing `/`, `/design-system`, `/public` and four portal layout routes remain.
`/login` is the only new screen. The design-system component inventory also
demonstrates pagination; it remains explicitly labeled local-only example data,
not a production property list.

## Configuration and environments

Use [the existing template](../frontend/.env.example) to configure `.env.local`
in development:

```dotenv
VITE_API_BASE_URL=/api
API_PROXY_TARGET=http://localhost:5076
```

- `VITE_API_BASE_URL` is public, substituted at **build time**, not a runtime
  deployment secret. `/api` is recommended; `/api/v1` is also supported.
- Canonical authentication/business URLs are always versioned exactly once.
  Health URLs remain `/api/health` even when the configured base includes `/v1`.
- Empty, relative-without-leading-slash, non-HTTP(S), credential-bearing,
  query/fragment-bearing and unsupported-version bases are rejected explicitly.
- Cookie-authenticated and anonymous authentication requests require the API to
  share the frontend origin. Cross-origin health-only configuration remains
  compatible, but it does not enable cross-origin authentication or CORS.
- `API_PROXY_TARGET` is Vite development/preview configuration only. It is not
  shipped to the browser and does not configure production static hosting.
- Restart Vite when changing local environment values. Never store passwords,
  CSRF tokens, cookies or SMTP/database credentials in `VITE_*`.

For production, set the public base before building:

```powershell
$env:VITE_API_BASE_URL = "/api"
npm ci --prefix frontend
npm run build --prefix frontend
```

Serve `frontend/dist` through a real production web server:

1. Terminate HTTPS and expose the frontend and `/api` on **one origin**.
2. Reverse-proxy `/api/*` to the API, preserving the API path, cookies, status
   codes, content types, `Cache-Control`, `Retry-After` and request headers.
3. Serve static assets normally; rewrite frontend routes such as `/login` and
   `/app/dashboard` and `/landlord` to `index.html`. Never rewrite API failures to frontend HTML.
4. Set the backend production environment, permitted hosts, PostgreSQL connection,
   SMTP and durable Data Protection keys as described in
   [authentication](authentication.md). Secure session cookies require HTTPS.
5. Configure trusted forwarded headers only as part of a deliberate deployment
   setup; existing rate limits still use the direct remote address.

`npm run preview --prefix frontend` is for local production-bundle verification,
not production deployment. A versioned-base production bundle was verified
against the real local API with login, logout and unversioned health requests.

## One transport, explicit contracts

[http-client.ts](../frontend/src/lib/http-client.ts) is the only production `fetch`
implementation. Health, sessions, authentication and resources delegate to it.
Pages and hooks must not recreate API communication.

- `requestJson` / `requireJson`: GET, 200 success and a caller-supplied runtime DTO
  type guard. Only the explicit session probe permits 401 as a null session.
- `writeApi`: POST, fresh CSRF bootstrap before **every write**, exact documented
  success status, JSON DTO validation when a response body is expected.
- Same-origin cookies, `no-store`, JSON Accept headers and optional explicit
  `X-Organization-Id`.
- A ten-second timeout **per HTTP request**, composed with caller cancellation.
  Logout/login may include several requests; no shared token cache is used.
- No automatic retry of writes, login attempts or registrations. A request can
  succeed server-side even if its response is lost; a client must not blindly
  repeat an operation.
- Malformed JSON, unexpected HTTP success status and unexpected response shapes
  produce explicit errors, not default objects or success-shaped fallbacks.

[ApiError](../frontend/src/lib/api-error.ts) preserves HTTP status, validated
Problem Details field errors, trace ID and Retry-After. It renders safe API titles
for expected client failures, but never unexpected server titles/details for 5xx.
Non-JSON HTTP failures remain explicit status errors. Network/cancellation errors
are propagated; consumers ignore only deliberate supersession/unmount aborts.

Backend validation remains authoritative. Frontend helpers provide immediate
guidance and map PascalCase/camelCase server field keys to controls with
`fieldError`. Do not log passwords, reset/verification tokens or request bodies.

## Authentication and protected layouts

[auth-api.ts](../frontend/src/lib/auth-api.ts) implements:

- Login: validate credentials, fresh CSRF, POST login, then GET and validate the
  real session **before** returning permissions. A login 204 alone never becomes
  a fabricated session.
- Owner registration: validate fields/password guidance; return the 201 DTO.
  Registration does not sign in, and delivery errors may occur after account
  persistence; see the backend recovery semantics before retrying.
- Verification/recovery email requests: 202 without account-existence claims.
- Email confirmation and password reset: explicit validated requests and 204.
- Logout-all: existing CSRF-protected API action. Consumers must refresh session
  state after invoking it; the current profile menu retains ordinary logout.

The [login screen](../frontend/src/pages/login.tsx) has labeled email/password
fields, client/server errors, first-invalid-field focus, submission/loading states,
double-submit prevention, cancellation and generic failed-sign-in information.
Login does not impose new-password complexity on existing credentials. Passwords
are cleared after failed submission and never persisted in browser storage.
Successful cookie login revalidates the real session and opens an authorized
layout. The backend controls email verification, lockout and rate limiting.

`returnTo` accepts only an **exact authorized existing layout route**. External,
protocol-relative, nested, query-bearing and unauthorized destinations are
ignored. With no eligible portal the user returns to `/public`; platform-only
administrators go to explicit `/administration`.
Owners/managers default to `/app/dashboard`; the exact legacy `/landlord`
destination resolves to that canonical route. Dashboard query-bearing return
destinations remain disallowed.

Anonymous portal gates now offer a login link retaining the requested portal.
They remain inline sign-in-required states rather than automatic redirects,
preserving existing error/expired-session UX. Role checks still deny manually
changing the frontend URL to an unauthorized portal.

An authenticated resource/write 401 dispatches a shared session-expired event.
The SessionProvider cancels outstanding session work, clears selected organization
state and unmounts protected children/navigation. A failed anonymous login does
not dispatch this event, and 403 means denied permission, not expired identity.
Backend authorization, membership scopes and RLS remain authoritative.

## Resource and pagination contracts

[resource-api.ts](../frontend/src/lib/resource-api.ts) supports typed single and
collection reads for all seven existing endpoints:

`properties`, `units`, `tenants`, `rental-applications`, `leases`, `payments`,
`maintenance-requests`.

Each response is checked for its discriminator, fields, statuses and organization;
single reads also validate the returned resource ID. Dates remain ISO calendar
date strings. Monetary JSON values are numbers for display; do not perform billing
or precision-sensitive monetary arithmetic in these clients. Backend decimal
values remain the source of truth.

Collection queries match [API conventions](api.md):

- One-based page, default 1; pageSize default 20, range 1-100.
- The computed offset must fit Int32; invalid values are rejected, not clamped.
- Exact resource-specific status filter; sortBy `id`, `createdAt`, `status`;
  sortDirection `asc`, `desc`.
- Validated `items`, `page`, `pageSize`, `totalCount`, `totalPages`; totalPages must
  equal ceiling(totalCount/pageSize). Unsafe JavaScript integer counts are rejected.
- Zero-result and beyond-end pages are valid with empty items. Duplicate IDs,
  mismatched requested status, foreign-organization records and inconsistent
  item counts fail response validation.

The reusable hook requires an authorized organization context:

```tsx
const [page, setPage] = useState(1)
const { state, retry } = useResourcePage('properties', {
  page, pageSize: 20, sortBy: 'createdAt', sortDirection: 'desc',
})

// Render explicit state.status === 'loading' / 'error' / 'loaded'.
// For the loaded state:
<Pagination {...state.data} onPageChange={setPage} />
```

This is usage guidance, not an implemented business page.
[useResourcePage](../frontend/src/lib/use-resource-page.ts) sends the selected
organization header and includes resource, organization, page, filter, sort and
retry generation in its result identity. Changing any identity immediately hides
the previous data. Superseded/unmounted requests are cancelled and late responses
cannot restore old organization content. Errors never expose a prior success page.
The caller owns page selection and should reset page to 1 when filters/sorts
change. No persistent cache, background polling or query library is introduced.

[Pagination](../frontend/src/components/ui/pagination.tsx) consumes actual server
counts; it disables previous/next at boundaries and during loading, supports zero
results and explicitly labels beyond-end pages. It does not paginate business
records in memory. Only the clearly labeled design-system inventory example
uses local pagination.

## Verification

```powershell
npm run lint --prefix frontend
npm test --prefix frontend
npm run build --prefix frontend
```

Unit/component tests cover URL configuration, versioning, safe failures,
Problem Details, CSRF, cookies, cancellation composition, response shapes,
authentication validation, exact permission-aware redirects, resource isolation,
status allowlists, monetary/date validation, pagination thresholds, zero/beyond
pages, duplicate IDs and component boundaries.
Final validation: lint passed without warnings, **166 frontend tests passed**
(70 added for this stage), and the TypeScript/Vite production build passed.

Browser checks used a freshly migrated **isolated temporary SQLite database**:
real owner registration, real pickup-email token verification, failed login,
successful cookie login, session validation, organization-scoped empty collection
queries, foreign-organization 403 and real CSRF logout. The default and explicitly
versioned production-bundle API bases were verified. Test account/database/email
files were cleaned up; the normal local database was not changed.
All isolated verification servers were stopped.

Test-only in-browser harnesses verified 20+1 pagination, late old-organization
response rejection and authenticated API 401 removal of protected content. These
fixtures are not production data or application code. Login form labels/errors,
focus and 320-1440px overflow checks passed.

The [landlord dashboard](dashboard.md) now consumes the shared client for real
organization/property-scoped summaries, with date filters and chart contracts.
No page contains its own fetch implementation.

Outstanding: registration/recovery screens and business CRUD pages remain intentionally
unimplemented; PostgreSQL live tests still require a server. Production HTTPS,
reverse-proxy configuration, SMTP and durable Data Protection are deployment tasks.

## Change inventory

Created:

- [api-error.ts](../frontend/src/lib/api-error.ts),
  [http-client.ts](../frontend/src/lib/http-client.ts) and
  [http-client.test.ts](../frontend/src/lib/http-client.test.ts).
- [auth-api.ts](../frontend/src/lib/auth-api.ts),
  [auth-api.test.ts](../frontend/src/lib/auth-api.test.ts),
  [login-navigation.ts](../frontend/src/lib/login-navigation.ts) and
  [login.tsx](../frontend/src/pages/login.tsx).
- [resource-api.ts](../frontend/src/lib/resource-api.ts),
  [resource-api.test.ts](../frontend/src/lib/resource-api.test.ts) and
  [use-resource-page.ts](../frontend/src/lib/use-resource-page.ts).
- [pagination.tsx](../frontend/src/components/ui/pagination.tsx) and
  [pagination.test.tsx](../frontend/src/components/ui/pagination.test.tsx).
- This guide.

Modified:

- [api.ts](../frontend/src/lib/api.ts),
  [api.test.ts](../frontend/src/lib/api.test.ts),
  [session-api.ts](../frontend/src/lib/session-api.ts) and
  [session-api.test.ts](../frontend/src/lib/session-api.test.ts): shared transport.
- [app-router.tsx](../frontend/src/app-router.tsx): login route.
- [session-provider.tsx](../frontend/src/components/layout/session-provider.tsx),
  [workspace-layout.tsx](../frontend/src/components/layout/workspace-layout.tsx) and
  [public-layout.tsx](../frontend/src/components/layout/public-layout.tsx):
  expired-session handling and login links.
- [design-system.tsx](../frontend/src/pages/design-system.tsx): pagination example.
- [.env.example](../frontend/.env.example): development/production conventions.
- [README](../README.md), [authentication](authentication.md),
  [architecture](architecture.md), [layouts](layouts.md) and
  [design system](design-system.md): current integration documentation.

No dependency, backend endpoint, schema or migration changes.
