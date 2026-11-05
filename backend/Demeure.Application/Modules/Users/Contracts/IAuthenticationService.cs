namespace Demeure.Application.Modules.Users.Contracts;

public interface IAuthenticationService
{
    Task<RegistrationResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<bool> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(bool allSessions, CancellationToken cancellationToken);
    Task<SessionResponse?> GetSessionAsync(CancellationToken cancellationToken);
    Task<bool> SelectOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
    Task SendVerificationAsync(string email, CancellationToken cancellationToken);
    Task<bool> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken);
    Task SendPasswordResetAsync(string email, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}
