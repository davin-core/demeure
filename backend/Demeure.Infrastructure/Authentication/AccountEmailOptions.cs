namespace Demeure.Infrastructure.Authentication;

public sealed class AccountEmailOptions
{
    public string Delivery { get; set; } = "Smtp";
    public string From { get; set; } = "";
    public string PickupDirectory { get; set; } = "";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
}
