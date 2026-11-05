using Demeure.Domain.Common;

namespace Demeure.Domain.Modules.Users.Entities;

public sealed class User : Entity
{
    private User() { }

    public User(string identityIssuer, string identitySubject, string email, string displayName)
    {
        IdentityIssuer = DomainGuard.Text(identityIssuer, 300, nameof(identityIssuer));
        IdentitySubject = DomainGuard.Text(identitySubject, 200, nameof(identitySubject));
        UpdateProfile(email, displayName);
    }

    public string IdentityIssuer { get; private set; } = "";
    public string IdentitySubject { get; private set; } = "";
    public string Email { get; private set; } = "";
    public string NormalizedEmail { get; private set; } = "";
    public string DisplayName { get; private set; } = "";
    public UserStatus Status { get; private set; } = UserStatus.Active;

    public void UpdateProfile(string email, string displayName)
    {
        var validatedEmail = DomainGuard.Email(email, nameof(email));
        var validatedName = DomainGuard.Text(displayName, 200, nameof(displayName));
        Email = validatedEmail;
        NormalizedEmail = validatedEmail.ToUpperInvariant();
        DisplayName = validatedName;
    }

    public void ChangeStatus(UserStatus status)
    {
        DomainGuard.Transition(Status, status, (Status, status) is
            (UserStatus.Active, UserStatus.Disabled) or (UserStatus.Disabled, UserStatus.Active));
        Status = status;
    }
}

public enum UserStatus { Active, Disabled }
