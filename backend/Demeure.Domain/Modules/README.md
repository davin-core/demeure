# Domain modules

Use `Modules/<Module>/` and namespaces `Demeure.Domain.Modules.<Module>`.
The [architecture catalog](../../../docs/architecture.md#2-module-organization-and-ownership)
defines the canonical module names, ownership and organization-isolation rules.

Domain code owns entities, value objects and pure invariants. It has no HTTP,
DI, persistence or external SDK dependencies. Do not expose mutable aggregates
across module boundaries.

Create each module folder only with its first requested domain implementation;
do not add empty entities or speculative base classes.
