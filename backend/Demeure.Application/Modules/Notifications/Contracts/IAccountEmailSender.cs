namespace Demeure.Application.Modules.Notifications.Contracts;

public interface IAccountEmailSender
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken);
}
