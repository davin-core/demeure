using Demeure.Application;
using Demeure.Infrastructure;
using Demeure.Infrastructure.Persistence;
using Demeure.Application.Common.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Demeure.Tests.Unit;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void LayerRegistrationIsComposableWithoutAServiceLocator()
    {
        var services = new ServiceCollection();

        Assert.Same(services, services.AddApplication());
        Assert.Same(services, services.AddInfrastructure("Sqlite", "Data Source=:memory:"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    [Fact]
    public void LayerRegistrationRejectsNullCollections()
    {
        Assert.Throws<ArgumentNullException>(() => Demeure.Application.DependencyInjection.AddApplication(null!));
        Assert.Throws<ArgumentNullException>(() => Demeure.Infrastructure.DependencyInjection.AddInfrastructure(null!, "Sqlite", "Data Source=:memory:"));
    }

    [Theory]
    [InlineData("Sqlite", "Data Source=:memory:;Foreign Keys=True", "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData("PostgreSQL", "Host=localhost;Database=not_connected;Username=test", "Npgsql.EntityFrameworkCore.PostgreSQL")]
    public void DatabaseRegistrationResolvesTheSelectedScopedContextAndFailsClosed(
        string provider, string connection, string expectedProvider)
    {
        var services = new ServiceCollection().AddInfrastructure(provider, connection);
        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true, ValidateOnBuild = true
        });
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DemeureDbContext>();

        Assert.Equal(expectedProvider, context.Database.ProviderName);
        Assert.Same(context, scope.ServiceProvider.GetRequiredService<DemeureDbContext>());
        Assert.Throws<ForbiddenException>(() => context.CurrentOrganizationId);
    }

    [Theory]
    [InlineData("PostgreSQL", null)]
    [InlineData("Sqlite", "")]
    [InlineData("Unknown", "Data Source=:memory:")]
    public void DatabaseRegistrationRejectsMissingOrInvalidConfiguration(string provider, string? connection)
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfrastructure(provider, connection));
    }
}
