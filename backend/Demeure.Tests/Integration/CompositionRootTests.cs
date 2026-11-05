using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Demeure.Tests.Integration;

public sealed class CompositionRootTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void StartupRejectsSingletonsThatCaptureScopedDependencies(string environment)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseTestDatabase();
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ScopedDependency>();
                services.AddSingleton<InvalidSingleton>();
            });
        });

        var exception = Assert.Throws<AggregateException>(() => factory.CreateClient());

        Assert.Contains("Cannot consume scoped service", exception.ToString());
    }

    private sealed class ScopedDependency;
    private sealed class InvalidSingleton(ScopedDependency dependency)
    {
        public ScopedDependency Dependency { get; } = dependency;
    }
}
