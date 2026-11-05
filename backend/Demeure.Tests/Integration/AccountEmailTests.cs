using Demeure.Application.Modules.Notifications.Contracts;
using Demeure.Infrastructure.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Demeure.Tests.Integration;

public sealed class AccountEmailTests
{
    [Fact]
    public async Task DevelopmentPickupWritesARealEmailWithoutAProductionSenderFallback()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"demeure-email-test-{Guid.NewGuid():N}");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development").UseTestDatabase()
                .UseSetting("AccountEmail:Delivery", "Pickup")
                .UseSetting("AccountEmail:PickupDirectory", directory);
        });
        try
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IAccountEmailSender>().SendAsync(
                "recipient@example.test", "Demeure delivery test", "A real local email message.", CancellationToken.None);
            var message = Assert.Single(Directory.GetFiles(directory, "*.eml"));
            var content = await File.ReadAllTextAsync(message);
            Assert.Contains("recipient@example.test", content);
            Assert.Contains("Demeure delivery test", content);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                foreach (var file in Directory.GetFiles(directory, "*.eml")) File.Delete(file);
                Directory.Delete(directory);
            }
        }
    }

    [Fact]
    public void ProductionRefusesPickupDeliveryInsteadOfPretendingToSendEmail()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production").UseTestDatabase()
                .UseSetting("AccountEmail:Delivery", "Pickup")
                .UseSetting("AccountEmail:PickupDirectory", Path.GetTempPath()));

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }

    [Fact]
    public void ProductionRequiresExplicitSmtpConfiguration()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production").UseTestDatabase()
                .UseSetting("AccountEmail:Host", ""));

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}
