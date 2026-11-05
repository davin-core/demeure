namespace Demeure.Api.Contracts;

/// <summary>Reports aggregate service health without exposing internal dependency details.</summary>
public sealed record HealthResponse(string Status, string Service);
