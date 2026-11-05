using Microsoft.AspNetCore.Hosting;

namespace Demeure.Tests;

internal static class TestDatabaseConfiguration
{
    internal static IWebHostBuilder UseTestDatabase(this IWebHostBuilder builder) =>
        builder
            .UseSetting("Database:Provider", "Sqlite")
            .UseSetting("ConnectionStrings:Demeure", "Data Source=:memory:;Foreign Keys=True")
            .UseSetting("AccountEmail:Delivery", "Smtp")
            .UseSetting("AccountEmail:Host", "localhost")
            .UseSetting("AccountEmail:From", "no-reply@example.test");
}
