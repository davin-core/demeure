# Presentation modules

Use `Modules/<Module>/Contracts/` for HTTP request/response DTOs and
`Modules/<Module>/<Module>Endpoints.cs` for minimal API route mapping.
The [architecture conventions](../../../docs/architecture.md) define routes,
validation, responses, error handling and module ownership.

Map requested module endpoints explicitly from the composition root. Handlers
delegate to Application interfaces, forward cancellation, use typed HTTP results,
and never inject repositories or expose database entities.

Organization-scoped endpoints require authenticated membership/context and
cross-organization integration tests before they are exposed. Health endpoints
remain outside the business module tree. Create module folders only with real
requested endpoints, not placeholders.
