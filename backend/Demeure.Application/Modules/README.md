# Application modules

Use `Modules/<Module>/Contracts/` for explicit module/service interfaces and
immutable application DTOs, and `Modules/<Module>/UseCases/<UseCase>/` for workflows.
The [architecture catalog](../../../docs/architecture.md#2-module-organization-and-ownership)
defines module ownership and allowed collaboration.

Depend on Domain and purpose-specific contracts. Do not depend on ASP.NET Core
or persistence providers. Validate contextual rules and organization authorization,
and forward cancellation to asynchronous integrations.

Add explicit registrations through the layer's `AddApplication` extension as
implementations are introduced. Keep module internals internal where possible.
Create only folders containing requested implementations.
