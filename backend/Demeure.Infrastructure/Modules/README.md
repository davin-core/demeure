# Infrastructure modules

Use `Modules/<Module>/Persistence/` and `Modules/<Module>/Integrations/` to implement
the corresponding Application contracts. The
[architecture catalog](../../../docs/architecture.md#2-module-organization-and-ownership)
defines ownership and cross-module restrictions.

Persistence mappings, database queries and provider adapters belong here.
Apply authorized organization scope to every tenant-owned operation; do not
make another module's repositories a public integration API.

Register implementations explicitly from `AddInfrastructure`, composing
module-specific registration extensions when those modules exist. Do not introduce
a database, repository abstraction or external provider before it is requested.

The requested initial schema is composed centrally in `Persistence/DemeureModelConfiguration.cs`
to make organization-scoped and cross-module foreign keys explicit. See the
[database design and ER diagram](../../../docs/database.md). Module-owned adapters
and query services will be added only with their requested use cases.
