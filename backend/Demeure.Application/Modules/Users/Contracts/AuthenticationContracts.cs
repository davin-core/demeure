using System.ComponentModel.DataAnnotations;

namespace Demeure.Application.Modules.Users.Contracts;

/// <summary>Registers a local account and a new organization owned by that account.</summary>
public sealed record RegisterRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public required string Email { get; init; }
    [Required, MinLength(12), MaxLength(128)]
    public required string Password { get; init; }
    [Required, MaxLength(200)]
    public required string DisplayName { get; init; }
    [Required, MaxLength(200)]
    public required string OrganizationName { get; init; }
    [Required, MaxLength(100), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public required string OrganizationSlug { get; init; }
}

/// <summary>Credentials used to establish an authenticated cookie session.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public required string Email { get; init; }
    [Required, MaxLength(128)]
    public required string Password { get; init; }
}

/// <summary>Requests a verification or password reset email without exposing account existence.</summary>
public sealed record EmailRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public required string Email { get; init; }
}

/// <summary>Confirms an account email using a purpose-bound, expiring Identity token.</summary>
public sealed record ConfirmEmailRequest
{
    [Required]
    public required Guid UserId { get; init; }
    [Required, MaxLength(4096)]
    public required string Token { get; init; }
}

/// <summary>Resets a password using a purpose-bound, expiring Identity token.</summary>
public sealed record ResetPasswordRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public required string Email { get; init; }
    [Required, MaxLength(4096)]
    public required string Token { get; init; }
    [Required, MinLength(12), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>Identifies the new account and organization; registration does not sign in.</summary>
public sealed record RegistrationResponse(Guid UserId, Guid OrganizationId);

/// <summary>Describes an authorized active organization membership.</summary>
public sealed record MembershipResponse(Guid OrganizationId, string OrganizationName, string Role);

/// <summary>Describes the current authenticated session without exposing credentials.</summary>
public sealed record SessionResponse(Guid UserId, string Email, string DisplayName,
    IReadOnlyList<string> GlobalRoles, IReadOnlyList<MembershipResponse> Memberships);

/// <summary>Provides a request token for the X-CSRF-TOKEN header; the paired cookie is HttpOnly.</summary>
public sealed record CsrfResponse(string Token);
