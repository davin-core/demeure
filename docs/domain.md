# Demeure domain entities and lifecycle rules

This phase turns the existing Organization, User, OrganizationMembership,
Property, Unit, Tenant, Lease and Payment persistence records into validated
domain entities. The product remains Demeure; references to Logis in earlier
prompts refer to the same application.

## Construction and mutation

- Create these eight entities through public constructors. Public parameterless
  construction is not allowed; private parameterless constructors support
  persistence materialization without provider-specific code in Domain.
- Business properties and statuses have private setters. Methods validate all
  incoming values **before** modifying the entity, so a rejected update leaves
  existing business state unchanged.
- Entity IDs are generated once and are not publicly assignable. Organization
  construction optionally accepts a nonempty ID for controlled provisioning.
- Organization ownership is assigned during construction and cannot be changed.
  The shared init accessor remains available for schema-only entities introduced
  previously, but rejects empty identifiers and reassignment to another organization.
- Required text is nonblank, bounded by the existing field limits, and trimmed.
  Optional blank contact/address/reference fields normalize to null.
- User email and its uppercase normalized form update atomically in the domain.
  Identity issuer/subject are immutable; Organization slug is normalized
  to lowercase at construction and is immutable afterward.
- Email syntax, country/currency code shape and nonnegative room counts are
  validated without EF attributes or provider dependencies.
- Money remains decimal. Rent and payment amounts must be positive; deposits may
  be zero. Money must fit 18 total digits and two fractional digits without rounding.
- Dates must be supplied and lease EndDate must follow StartDate.
- Payment completion requires an explicit nondefault instant, normalized to UTC.
  It records the receipt reference and PaidAt together with Completed status.
- CreatedAt/UpdatedAt remain shared technical persistence metadata. Their existing
  public setters are retained for compatibility with the other schema entities;
  Infrastructure preserves creation time and assigns UTC update time on save.
  They are not a public business-state transition mechanism.

No new dependencies, tables, columns or migrations are needed.

## Public business methods

| Entity | Methods |
| --- | --- |
| Organization | `Rename`, `ChangeStatus` |
| User | `UpdateProfile`, `ChangeStatus` |
| OrganizationMembership | `ChangeRole`, `ChangeStatus` |
| Property | `Rename`, `UpdateAddress`, `ChangeStatus` |
| Unit | `UpdateDetails`, `ChangeStatus` |
| Tenant | `UpdateContact`, `LinkUser`, `UnlinkUser`, `ChangeStatus` |
| Lease | `UpdateTerms`, `Activate`, `Cancel`, `End`, `Terminate` |
| Payment | `Complete`, `Fail`, `Retry`, `Cancel`, `Refund` |

Update methods replace their supplied field group, not arbitrary subsets.
For example, `Tenant.UpdateContact` with null email/phone clears those optional
fields. Linking a user cannot silently replace an existing link; unlink first.
Membership role changes require an active membership and a defined role.

## Accepted lifecycle rules

Only these directed transitions are allowed. Repeating a transition to the
current state is rejected, not silently treated as successful. Undefined enum
values are rejected. Archived, removed, ended, terminated, cancelled and refunded
terminal states cannot be reopened.

| Entity | Allowed transitions |
| --- | --- |
| Organization | Active -> Suspended/Archived; Suspended -> Active/Archived |
| User | Active -> Disabled; Disabled -> Active |
| Membership | Active -> Suspended/Removed; Suspended -> Active/Removed |
| Property | Active -> Inactive/Archived; Inactive -> Active/Archived |
| Unit | Available -> Occupied/Unavailable/Archived; Occupied -> Available; Unavailable -> Available/Archived |
| Tenant | Active -> Inactive/Archived; Inactive -> Active/Archived |
| Lease | Draft -> Active/Cancelled; Active -> Ended/Terminated |
| Payment | Pending -> Completed/Failed/Cancelled; Failed -> Pending/Cancelled; Completed -> Refunded |

Archived organizations, properties, units and tenants cannot be edited.
Occupied units must be released to Available before being made unavailable or
archived. The entity does not decide whether a lease authorizes that release;
the application workflow must coordinate this.

### Lease decisions

- Lease terms are editable only while Draft.
- Activation is allowed ahead of StartDate, as selected. It is a lifecycle action,
  not an automatic check against the machine's current date.
- `End(effectiveDate)` requires an Active lease and an explicit calendar date on
  or after EndDate. A premature end is rejected without modifying status.
- `Terminate()` is the separate early-termination action for an Active lease.
- End/termination do not silently rewrite contractual dates or monthly rent.
- No implicit unit-occupancy change, tenant-status change or payment generation
  occurs. These require an authorized application workflow and transaction.

### Payment decisions

- A Failed payment must be retried to Pending before completion.
- Completed payments can only be fully refunded; no partial-refund bookkeeping or
  external payment provider is implemented.
- Refund preserves Amount, Currency, PaidAt and Reference as receipt evidence.
- Failed/cancelled pending payments do not acquire a PaidAt timestamp.
- The completion instant is explicit; no provider callback, automatic settlement,
  clock-based future-payment policy or cancellation refund is invented here.
- Payment amount, currency, due date and lease relationship are immutable after
  construction. A correction workflow is not implemented.

## Relationships and module boundaries

Relationships remain the scalar identifiers established by the database design:

- OrganizationMembership references Organization and a global User.
- Property belongs to Organization; Unit references its Property.
- Tenant optionally links to a User who has membership in the same organization.
- Lease references one primary Tenant and Unit, with an optional rental application.
- Payment references Lease; tenant/unit ownership derives from that lease.

Constructors reject empty foreign IDs, but cannot establish referenced-row existence
or authenticated membership from IDs alone. These are application/persistence
responsibilities. Composite organization foreign keys continue to enforce
same-organization relationships at the database boundary.

Do not add cross-module aggregate navigation graphs or inject repositories into
domain objects. The existing [database design](database.md) and
[architecture conventions](architecture.md) remain authoritative for isolation.

## Validation and error handling

Invalid arguments throw ArgumentException/ArgumentOutOfRangeException. Illegal
state operations throw InvalidOperationException. These are deliberate domain
failures, not silent defaults or success-shaped fallbacks.

The API does not broadly map those exception types to 400/409, because they may
also represent programming errors. Future application services must validate
commands and translate specifically identified domain failures into safe
application errors or typed results. The domain stage added no business endpoints;
the subsequent [authorization stage](authorization.md) exposes only scoped reads.

Domain validation supplements, rather than replaces, API validation, organization
authorization, uniqueness checks and database constraints. Infrastructure retains
generic storage checks for all schema entities and owns EF mappings and UTC/decimal
provider conversions. Email/slug normalization no longer waits until SaveChanges.

## Verification and remaining boundaries

Unit tests cover constructor defaults, required fields and limits, nonempty IDs,
enum validity, all lifecycle source/target combinations, atomic failed updates,
date/money boundaries, tenant login links and payment receipt preservation.
SQLite integration tests verify private-setter materialization, continued lifecycle
operations after reload, terminal-state protection, and the original foreign-key
and isolation constraints. Both PostgreSQL and SQLite migration snapshots must
remain unchanged.

The remaining schema-only entities are outside this phase and retain their previous
mutable API. Cross-aggregate rules remain outstanding: last-owner protection,
membership permissions, active lease overlap, occupancy coordination, currency
matching between payment and lease, and concurrent workflow consistency.
These must be implemented in requested application use cases, not guessed from
isolated domain IDs. Live PostgreSQL verification remains pending.
