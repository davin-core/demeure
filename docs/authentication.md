# Demeure authentication

ASP.NET Core Identity handles local accounts, password hashing, verification,
recovery, lockout and cookie authentication. Application exposes DTOs and
`IAuthenticationService`; Infrastructure implements Identity/EF/email adapters;
Api owns routing, CSRF, rate limits and authorization policies. Domain User remains
framework-independent. The [frontend integration](frontend-integration.md) now
implements a login screen, shared cookie/CSRF clients and session-aware protected
layouts. Registration/recovery screens and JWTs are not implemented.

The [application layouts](layouts.md) now consume the real session DTO for
permission-aware menus and organization selection. Their sign-out action uses
a freshly bootstrapped CSRF token and the existing logout endpoint. This adds
no registration/recovery screens and does not change backend authentication.

Canonical routes now use `/api/v1/auth`; all `/api/auth` examples below remain
supported compatibility aliases with the same cookies and CSRF rules. New clients
should prefer v1. See [API conventions](api.md) for route compatibility and local
Swagger UI, which obtains fresh CSRF tokens when executing writes.

## Local setup

Start with the [database setup](database.md#local-sqlite-setup), including the
`AddIdentityAuthentication` migration, then run the API in Development:

```powershell
dotnet run --project backend\Demeure.Api
```

Development uses real `.eml` pickup delivery to `backend/Demeure.Api/EmailMessages`
under the API content root. Open the new message in a mail client or text editor
to obtain the user ID/verification token or password reset token. These files
contain sensitive recovery credentials: restrict filesystem access, do not commit
them, and delete them when no longer needed. This is not mock email functionality.

The API's development HTTP listener is a loopback-only convenience; Secure cookies
are required outside Development. Never expose the Development listener publicly.
Production rejects pickup delivery or missing SMTP configuration at startup.

## HTTP and CSRF flow

Use one cookie-retaining client throughout a flow:

1. `GET /api/auth/csrf` returns `{ "token": "..." }` and a paired HttpOnly
   `Demeure.Csrf` cookie. Do not confuse that cookie with the response token.
2. Include the response token as `X-CSRF-TOKEN` on **every POST/PUT/PATCH/DELETE
   under `/api`**, even anonymous registration, login and account recovery.
   Preserve the cookie alongside it. Missing/invalid tokens return 400 Problem Details.
3. Register the owner, obtain the actual verification token from the delivered
   email, and confirm the email. Registration and confirmation do not sign in.
4. Login creates the HttpOnly `Demeure.Session` cookie.
5. Fetch a **new CSRF token after login, logout, logout-all, or changing identity**.
   An anonymous token is not valid for a signed-in user.
6. `GET /api/auth/session` returns the current account and active memberships.
   A session or CSRF token response is non-cacheable. Never store session cookies
   or password/reset tokens in browser localStorage.

Example anonymous bootstrap and registration from the repository root:

```powershell
$base = "http://localhost:5076"
$csrf = Invoke-RestMethod "$base/api/auth/csrf" -SessionVariable session
$headers = @{ "X-CSRF-TOKEN" = $csrf.token }
$request = @{
  email = "your-email@example.com"
  password = "REPLACE_WITH_A_STRONG_UNIQUE_PASSWORD"
  displayName = "Your name"
  organizationName = "Your organization"
  organizationSlug = "your-organization"
} | ConvertTo-Json
Invoke-RestMethod "$base/api/auth/register" -Method Post -WebSession $session `
  -Headers $headers -ContentType "application/json" -Body $request
```

The placeholders above must be replaced deliberately; no accounts are seeded.
The [HTTP request file](../backend/Demeure.Api/Demeure.Api.http) covers all
endpoints. Its client must retain cookies, and the CSRF placeholder must be
updated from each bootstrap response after an identity change.
Frontend clients use same-origin requests (`credentials: "same-origin"`)
through the existing Vite `/api` proxy. Cross-origin CORS is not enabled.

## Endpoint contracts

Requests use JSON with camelCase property names. Successes have no envelope;
failures use safe `application/problem+json` with `status`, path-only `instance`
and `traceId`. Shape/policy validation adds `errors`. Auth failures never redirect.

| Method/path under `/api/auth` | Request body | Success |
| --- | --- | --- |
| GET `/csrf` | None | 200 `{ token }` + paired cookie |
| POST `/register` | `email`, `password`, `displayName`, `organizationName`, `organizationSlug` | 201 `{ userId, organizationId }`; Location `/api/auth/session` becomes usable after login |
| POST `/login` | `email`, `password` | 204 + session cookie; generic 401 on failure |
| GET `/session` | None; authenticated | 200 `{ userId, email, displayName, globalRoles, memberships }` |
| POST `/logout` | None; authenticated + CSRF | 204, clears current browser cookie |
| POST `/logout-all` | None; authenticated + CSRF | 204, rotates security stamp and clears cookie |
| POST `/verification-email` | `email` | 202, no account-existence disclosure |
| POST `/confirm-email` | `userId`, `token` from verification email | 204; invalid/expired/used token gives 400 |
| POST `/forgot-password` | `email` | 202, no account-existence disclosure |
| POST `/reset-password` | `email`, `token` from reset email, `newPassword` | 204; invalid/expired token or invalid password gives 400 |

`memberships` contains `{ organizationId, organizationName, role }` entries.
Registration deliberately returns safe 400 field errors for unavailable email
or slug. Only the recovery/resend endpoints have non-enumerating success responses;
this does not promise constant-time processing or hide all registration information.

Registration saves Domain User, Organization, owner membership and Identity
account in one transaction. Invalid password/duplicate registration cannot leave
partial ownership records. Email delivery happens **after commit**: delivery
failure is logged and returns 500, but the account/organization remain persisted.
Use `/verification-email` after resolving delivery rather than registering again.
No silent fallback or durable background queue/outbox is implemented.

## Passwords and sessions

- Identity PBKDF2 hashing: 210,000 iterations with per-password salt; no plaintext
  password persistence. Standard Identity verification supports rehashing.
- New/reset passwords: 12-128 characters, at least six distinct characters and
  lowercase, uppercase, digit and nonalphanumeric characters.
- Five failed password checks lock a confirmed account for 15 minutes.
  Disabled users and unverified emails cannot sign in. Failure responses are generic.
- Confirmation/reset tokens are purpose-bound Data Protection tokens with a
  one-hour lifetime, Base64Url encoded for transport. They are not JWTs.
  Already-confirmed verification is rejected; successful reset changes the stamp
  so the reset token cannot be reused. Reset also clears lockout/failure count.
- Session cookie: HttpOnly, SameSite=Strict, Secure outside Development,
  nonpersistent browser cookie, fixed eight-hour protected ticket, no sliding
  renewal. Security-stamp validation refreshes claims without extending expiry.
- Security stamp and active/confirmed account status are checked on each request.
  Password reset and logout-all revoke other sessions on their next request.
  This intentionally trades additional database work for prompt revocation.
- Ordinary logout deletes only this browser's cookie; a previously copied cookie
  remains cryptographically valid until ticket expiry or all-session/stamp
  revocation. No server-side per-session registry or single-session replay
  revocation is claimed. Use logout-all to invalidate all tickets.
- JWT access/refresh tokens and refresh-token rotation are not applicable.

## Role and organization boundaries

Only public **owner registration** is available. It creates a new organization,
assigns its active `Owner` membership to the registering user and never accepts
client-selected role, organization ID, or another organization's ownership.

| Policy / API role | Source |
| --- | --- |
| SuperAdministrator | Explicitly provisioned trusted Identity global role |
| OrganizationOwner | Active membership role Owner |
| PropertyManager | Active membership role Administrator or Manager |
| Tenant | Active membership role Tenant |
| Contractor | Active membership role Contractor |

No bootstrap administrator, production seed account, public role-management
endpoint, invitation workflow, or tenant/contractor registration is added.
Trusted future provisioning must use Identity `RoleManager`/`UserManager` for
SuperAdministrator and organization memberships for organization roles.
Global Identity OrganizationOwner/PropertyManager/Tenant/Contractor role claims
are filtered out and cannot grant tenant access.

`X-Organization-Id` is optional, but if supplied must be one nonempty GUID and
match an active membership in an active organization. Malformed values give
400; unauthorized values give 403. Without the header, a sole authorized active
membership is selected; zero/multiple memberships leave context unavailable.
Membership suspension/removal and organization suspension take effect next request.

Organization policies require both the dynamically resolved role and the
validated organization claim. SuperAdministrator does **not** implicitly bypass
tenant filters or acquire owner access to arbitrary organizations.
Future tenant endpoints must use explicit policies and scoped use cases; role
names alone do not provide resident/unit/document-level permissions.
The protected read API now implements manager assignments, resident record
ownership and contractor job scopes. Separate platform reads require a stored
global role, explicit target and committed reason-bearing audit. See
[authorization](authorization.md), including PostgreSQL RLS and bootstrap exclusions.
Trusted registration/membership lookups narrowly bypass EF filters with explicit
authenticated-user predicates, not arbitrary tenant-data access.

## Rate limiting and logging

Registration, login, confirmation, verification resend, forgot-password and reset
share one fixed window of **10 requests per minute per remote IP per process**,
with no queue. Rejection is 429 Problem Details with Retry-After when available.
Malformed DTOs and failed CSRF can consume quota; rate limiting precedes validation.
Session/logout use authentication/CSRF, not this anonymous sensitive-route quota.

The limiter uses the direct connection remote address, never an unchecked client
header. Behind a proxy, requests may share that proxy's quota until trusted proxy
handling is configured. This is not a distributed rate limiter; use deployment
edge limits for multi-instance protection.

Request logs use route templates, status, elapsed time and trace IDs. Authorized
organization scope is added to downstream logs. Passwords, cookies, request bodies,
email/reset tokens and raw query strings are never explicitly logged. Protect and
retain operational logs according to deployment policy.

## Production checklist

1. Run and verify reviewed PostgreSQL migrations against a real server using a
   least-privilege runtime database role. SQLite verification is not PostgreSQL execution.
2. Host frontend and `/api` on the same HTTPS origin and configure actual AllowedHosts.
   Keep Development disabled. Terminate TLS securely; when using a reverse proxy,
   configure only trusted forwarded headers/networks before relying on client IP
   or original scheme. No unrestricted forwarded-header middleware is enabled here.
3. Configure SMTP with STARTTLS (typically port 587), a provider-authorized From
   address, and credentials via the deployment secret manager:

   ```text
   AccountEmail__Delivery=Smtp
   AccountEmail__Host=<your-smtp-host>
   AccountEmail__Port=587
   AccountEmail__From=<verified-sender>
   AccountEmail__UserName=<secret>
   AccountEmail__Password=<secret>
   ```

   Port 465 implicit TLS is not the BCL sender's STARTTLS mode. Validate real
   SMTP connectivity/delivery and sender-domain configuration before launch.
   Development can instead use `dotnet user-secrets set <key> <value> --project
   backend\Demeure.Api`; the API has a development UserSecretsId.
4. Persist and encrypt ASP.NET Core Data Protection keys at rest using the
   deployment's supported key store/protector. Keep application name `Demeure`,
   share the protected key ring across replicas, restrict access and retain keys
   for outstanding tickets/tokens. The current code uses framework defaults:
   it does not configure a production key volume or encryption provider.
   Losing keys invalidates cookies/recovery tokens; ephemeral replicas cannot
   reliably share sessions. Never include real key material in source control.
5. Establish controlled SuperAdministrator/membership provisioning. No public
   endpoint or default credentials exist for this.
6. Configure edge rate limits, SMTP monitoring, backup/restore and recovery.
   Durable email delivery, MFA and role-management workflows are not implemented
   and must be separately requested before adding them.

## Verification

Tests use actual Identity accounts and isolated SQLite databases, not a fake
authentication scheme. Coverage includes transactional ownership, password policy,
verification/recovery, cookie flags/expiry, CSRF identity changes, role separation,
cross-organization denial, membership changes, lockout, per-IP rate limits and
global stamp revocation. Separate tests exercise actual `.eml` pickup delivery,
reject unsafe production configuration and prove explicit email-failure recovery.
Live PostgreSQL and production SMTP delivery remain deployment verification tasks.

## Authentication change inventory

### Created

- Application contracts: [AuthenticationContracts.cs](../backend/Demeure.Application/Modules/Users/Contracts/AuthenticationContracts.cs),
  [IAuthenticationService.cs](../backend/Demeure.Application/Modules/Users/Contracts/IAuthenticationService.cs),
  [ApplicationRoles.cs](../backend/Demeure.Application/Modules/Users/Contracts/ApplicationRoles.cs),
  [RequestValidationException.cs](../backend/Demeure.Application/Common/Exceptions/RequestValidationException.cs),
  [IAccountEmailSender.cs](../backend/Demeure.Application/Modules/Notifications/Contracts/IAccountEmailSender.cs).
- Infrastructure authentication: [IdentityAccount.cs](../backend/Demeure.Infrastructure/Authentication/IdentityAccount.cs),
  [AuthenticationService.cs](../backend/Demeure.Infrastructure/Authentication/AuthenticationService.cs),
  [IdentityClaimsFactory.cs](../backend/Demeure.Infrastructure/Authentication/IdentityClaimsFactory.cs),
  [OrganizationContext.cs](../backend/Demeure.Infrastructure/Authentication/OrganizationContext.cs),
  [AccountEmailOptions.cs](../backend/Demeure.Infrastructure/Authentication/AccountEmailOptions.cs),
  [AccountEmailSender.cs](../backend/Demeure.Infrastructure/Authentication/AccountEmailSender.cs),
  [DependencyInjection.cs](../backend/Demeure.Infrastructure/Authentication/DependencyInjection.cs).
- Presentation: [AuthEndpoints.cs](../backend/Demeure.Api/Modules/Users/AuthEndpoints.cs),
  [AuthenticationConfiguration.cs](../backend/Demeure.Api/Extensions/AuthenticationConfiguration.cs),
  [CsrfMiddleware.cs](../backend/Demeure.Api/Middleware/CsrfMiddleware.cs),
  [OrganizationContextMiddleware.cs](../backend/Demeure.Api/Middleware/OrganizationContextMiddleware.cs).
- Integration tests: [AuthenticationTests.cs](../backend/Demeure.Tests/Integration/AuthenticationTests.cs),
  [AccountEmailTests.cs](../backend/Demeure.Tests/Integration/AccountEmailTests.cs).
- PostgreSQL [AddIdentityAuthentication migration](../backend/Demeure.Infrastructure/Persistence/Migrations/PostgreSql/20261105182601_AddIdentityAuthentication.cs)
  and [designer](../backend/Demeure.Infrastructure/Persistence/Migrations/PostgreSql/20261105182601_AddIdentityAuthentication.Designer.cs);
  SQLite [migration](../backend/Demeure.Infrastructure/Persistence/Migrations/Sqlite/20261105182604_AddIdentityAuthentication.cs)
  and [designer](../backend/Demeure.Infrastructure/Persistence/Migrations/Sqlite/20261105182604_AddIdentityAuthentication.Designer.cs).
- This authentication guide.

### Updated

- Presentation composition/configuration: [Program.cs](../backend/Demeure.Api/Program.cs),
  [ApiExceptionHandler.cs](../backend/Demeure.Api/Middleware/ApiExceptionHandler.cs),
  [Demeure.Api.csproj](../backend/Demeure.Api/Demeure.Api.csproj),
  [appsettings.json](../backend/Demeure.Api/appsettings.json),
  [appsettings.Development.json](../backend/Demeure.Api/appsettings.Development.json),
  [Demeure.Api.http](../backend/Demeure.Api/Demeure.Api.http).
- Persistence/dependencies: [Demeure.Infrastructure.csproj](../backend/Demeure.Infrastructure/Demeure.Infrastructure.csproj),
  [DemeureDbContext.cs](../backend/Demeure.Infrastructure/Persistence/DemeureDbContext.cs),
  [DemeureModelConfiguration.cs](../backend/Demeure.Infrastructure/Persistence/DemeureModelConfiguration.cs),
  [PostgreSQL snapshot](../backend/Demeure.Infrastructure/Persistence/Migrations/PostgreSql/DemeureDbContextModelSnapshot.cs),
  [SQLite snapshot](../backend/Demeure.Infrastructure/Persistence/Migrations/Sqlite/SqliteDemeureDbContextModelSnapshot.cs).
- Domain roles: [OrganizationMembership.cs](../backend/Demeure.Domain/Modules/Organizations/Entities/OrganizationMembership.cs).
- Existing test configuration/regressions: [TestDatabaseConfiguration.cs](../backend/Demeure.Tests/TestDatabaseConfiguration.cs),
  [ApiConventionTests.cs](../backend/Demeure.Tests/Integration/ApiConventionTests.cs),
  [HealthEndpointTests.cs](../backend/Demeure.Tests/Integration/HealthEndpointTests.cs),
  [CompositionRootTests.cs](../backend/Demeure.Tests/Integration/CompositionRootTests.cs),
  [PersistenceTests.cs](../backend/Demeure.Tests/Integration/PersistenceTests.cs),
  [PostgreSqlModelTests.cs](../backend/Demeure.Tests/Unit/PostgreSqlModelTests.cs).
- Documentation/environment: [README.md](../README.md), [architecture.md](architecture.md),
  [database.md](database.md), [domain.md](domain.md), [backend environment reference](../backend/.env.example),
  [.gitignore](../.gitignore).
