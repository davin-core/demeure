using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Demeure.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Demeure.Tests.Integration;

public sealed class HealthEndpointTests
{
    [Theory]
    [InlineData("/api/health")]
    [InlineData("/api/health/live")]
    public async Task HealthyEndpointsReturnTheExistingContract(string path)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(new HealthResponse("ok", "demeure-api"),
            await response.Content.ReadFromJsonAsync<HealthResponse>());
    }

    [Theory]
    [InlineData(HealthStatus.Unhealthy, HttpStatusCode.ServiceUnavailable, "unhealthy")]
    [InlineData(HealthStatus.Degraded, HttpStatusCode.OK, "degraded")]
    public async Task ReadinessReflectsDependencyHealthWithoutExposingDetails(
        HealthStatus status, HttpStatusCode expectedCode, string expectedStatus)
    {
        await using var factory = CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddHealthChecks().AddCheck(
                "test-dependency", () => new HealthCheckResult(status, "private dependency details"))));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedCode, response.StatusCode);
        Assert.Equal(new HealthResponse(expectedStatus, "demeure-api"),
            JsonSerializer.Deserialize<HealthResponse>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.DoesNotContain("private dependency details", body);
    }

    [Fact]
    public async Task LivenessDoesNotRunDependencyChecks()
    {
        var executions = 0;
        await using var factory = CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddHealthChecks().AddCheck("test-dependency", () =>
            {
                Interlocked.Increment(ref executions);
                return HealthCheckResult.Unhealthy();
            })));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, executions);
    }

    [Fact]
    public async Task OpenApiDocumentsBothHealthEndpoints()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/health", out var readiness));
        Assert.True(paths.TryGetProperty("/api/health/live", out var liveness));
        Assert.True(readiness.GetProperty("get").GetProperty("responses").TryGetProperty("503", out _));
        Assert.True(liveness.GetProperty("get").GetProperty("responses").TryGetProperty("200", out _));
    }

    [Fact]
    public async Task OpenApiIsNotExposedInProduction()
    {
        await using var factory = CreateFactory("Production");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TestOnlyConventionEndpointsAreNotExposedByTheApi()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/__tests/errors/unexpected");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoutesReturnProblemDetails()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/does-not-exist");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, document.RootElement.GetProperty("status").GetInt32());
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment(environment).UseTestDatabase());
}
