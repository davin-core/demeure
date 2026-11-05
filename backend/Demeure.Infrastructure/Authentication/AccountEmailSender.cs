using System.Net;
using System.Net.Mail;
using Demeure.Application.Modules.Notifications.Contracts;
using Microsoft.Extensions.Options;

namespace Demeure.Infrastructure.Authentication;

internal sealed class AccountEmailSender(IOptions<AccountEmailOptions> options) : IAccountEmailSender
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var message = new MailMessage(settings.From, recipient, subject, body);
        using var client = new SmtpClient();
        if (settings.Delivery == "Pickup")
        {
            Directory.CreateDirectory(settings.PickupDirectory);
            client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            client.PickupDirectoryLocation = settings.PickupDirectory;
        }
        else
        {
            client.Host = settings.Host;
            client.Port = settings.Port;
            client.EnableSsl = true;
            client.UseDefaultCredentials = false;
            if (!string.IsNullOrWhiteSpace(settings.UserName))
                client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }
        await client.SendMailAsync(message, cancellationToken);
    }
}
