using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Demeure.Tests.Integration;

public sealed class ApiDocumentationTests
{
    [Fact]
    public async Task OpenApiDescribesCanonicalRoutesPagingAuthenticationAndHeadersWithoutAliasDuplication()
    {
        await using var factory = CreateFactory("Development");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("1.0", root.GetProperty("info").GetProperty("version").GetString());
        var paths = root.GetProperty("paths");
        Assert.False(paths.TryGetProperty("/api/properties", out _));
        Assert.False(paths.TryGetProperty("/api/auth/login", out _));
        foreach (var resource in new[] { "properties", "units", "tenants", "rental-applications", "leases", "payments", "maintenance-requests" })
        {
            var list = paths.GetProperty($"/api/v1/{resource}").GetProperty("get");
            Assert.True(list.GetProperty("responses").TryGetProperty("200", out _));
            Assert.True(list.GetProperty("responses").TryGetProperty("400", out _));
            var parameters = list.GetProperty("parameters").EnumerateArray().ToArray();
            foreach (var name in new[] { "page", "pageSize", "status", "sortBy", "sortDirection", "X-Organization-Id" })
                Assert.Contains(parameters, value => value.GetProperty("name").GetString() == name);
            Assert.True(list.GetProperty("security")[0].TryGetProperty("SessionCookie", out _));
        }
        var login = paths.GetProperty("/api/v1/auth/login").GetProperty("post");
        Assert.Contains(login.GetProperty("parameters").EnumerateArray(),
            value => value.GetProperty("name").GetString() == "X-CSRF-TOKEN" && value.GetProperty("required").GetBoolean());
        Assert.False(login.TryGetProperty("security", out var security) && security.GetArrayLength() > 0);
        Assert.True(login.GetProperty("responses").TryGetProperty("429", out _));
        var recovery = paths.GetProperty("/api/v1/auth/forgot-password").GetProperty("post");
        Assert.True(recovery.GetProperty("responses").TryGetProperty("400", out _));
        Assert.True(recovery.GetProperty("responses").TryGetProperty("500", out _));
        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("SessionCookie");
        Assert.Equal("cookie", scheme.GetProperty("in").GetString());
        Assert.Equal("Demeure.Session", scheme.GetProperty("name").GetString());
        var schemas = root.GetProperty("components").GetProperty("schemas").EnumerateObject().ToArray();
        Assert.Contains(schemas, schema => schema.Value.TryGetProperty("properties", out var properties) &&
            properties.TryGetProperty("items", out _) && properties.TryGetProperty("totalCount", out _) &&
            properties.TryGetProperty("totalPages", out _));
    }

    [Theory]
    [InlineData("/swagger/index.html", "text/html")]
    [InlineData("/swagger/swagger-ui-bundle.js", "text/javascript")]
    [InlineData("/swagger/swagger-ui.css", "text/css")]
    [InlineData("/swagger/swagger-init.js", "text/javascript")]
    public async Task SwaggerAssetsAreLocalAndDevelopmentOnly(string path, string contentType)
    {
        await using var development = CreateFactory("Development");
        using var client = development.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        if (path.EndsWith(".html", StringComparison.Ordinal))
        {
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("/swagger/swagger-ui-bundle.js", html);
            Assert.DoesNotContain("https://", html);
            Assert.True(response.Headers.Contains("Content-Security-Policy"));
        }
        await using var production = CreateFactory("Production");
        using var deployed = production.CreateClient();
        using var hidden = await deployed.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment(environment).UseTestDatabase());
}
