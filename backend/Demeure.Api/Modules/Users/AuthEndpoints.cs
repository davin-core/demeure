using Demeure.Application.Modules.Users.Contracts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Demeure.Api.Modules.Users;

/// <summary>Maps cookie authentication and account-recovery endpoints.</summary>
public static class AuthEndpoints
{
    /// <summary>Maps the authentication API without frontend screens.</summary>
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth").WithTags("Authentication");
        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery, CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new CsrfResponse(antiforgery.GetAndStoreTokens(context).RequestToken!));
        }).WithName("GetCsrfToken").WithSummary("Get a CSRF token for subsequent writes");

        group.MapPost("/register", async (RegisterRequest request, IAuthenticationService service, HttpContext context,
            CancellationToken cancellationToken) =>
            TypedResults.Created(context.Request.Path.StartsWithSegments("/api/v1")
                ? "/api/v1/auth/session" : "/api/auth/session", await service.RegisterAsync(request, cancellationToken)))
            .WithName("RegisterOwner").WithSummary("Register an account and its owned organization")
            .RequireRateLimiting("auth-sensitive")
            .ProducesValidationProblem();

        group.MapPost("/login", async Task<Results<NoContent, ProblemHttpResult>> (LoginRequest request, IAuthenticationService service,
            CancellationToken cancellationToken) =>
            await service.LoginAsync(request, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.Problem(statusCode: 401, title: "Sign-in failed",
                    detail: "Invalid credentials, unverified email, or unavailable account."))
            .WithName("Login").WithSummary("Create a cookie session after email verification")
            .RequireRateLimiting("auth-sensitive").Produces(204).ProducesProblem(401);

        group.MapPost("/logout", async (IAuthenticationService service, CancellationToken cancellationToken) =>
        {
            await service.LogoutAsync(false, cancellationToken);
            return TypedResults.NoContent();
        }).RequireAuthorization().WithName("Logout").WithSummary("Clear the current browser session");

        group.MapPost("/logout-all", async (IAuthenticationService service, CancellationToken cancellationToken) =>
        {
            await service.LogoutAsync(true, cancellationToken);
            return TypedResults.NoContent();
        }).RequireAuthorization().WithName("LogoutAll").WithSummary("Revoke all sessions and clear this session");

        group.MapGet("/session", async Task<Results<UnauthorizedHttpResult, Ok<SessionResponse>>> (HttpContext context, IAuthenticationService service,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var session = await service.GetSessionAsync(cancellationToken);
            return session is null ? TypedResults.Unauthorized() : TypedResults.Ok(session);
        }).RequireAuthorization().WithName("GetSession").WithSummary("Get current account and active memberships")
            .Produces<SessionResponse>().ProducesProblem(401);

        group.MapPost("/verification-email", async (EmailRequest request, IAuthenticationService service,
            CancellationToken cancellationToken) =>
        {
            await service.SendVerificationAsync(request.Email, cancellationToken);
            return TypedResults.Accepted((string?)null);
        }).RequireRateLimiting("auth-sensitive").WithName("SendVerificationEmail")
            .WithSummary("Request a verification email without revealing account existence");

        group.MapPost("/confirm-email", async Task<Results<NoContent, ProblemHttpResult>> (ConfirmEmailRequest request, IAuthenticationService service,
            CancellationToken cancellationToken) =>
            await service.ConfirmEmailAsync(request, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.Problem(statusCode: 400, title: "Invalid or expired verification token"))
            .RequireRateLimiting("auth-sensitive").WithName("ConfirmEmail").WithSummary("Verify the registered email")
            .Produces(204).ProducesProblem(400);

        group.MapPost("/forgot-password", async (EmailRequest request, IAuthenticationService service,
            CancellationToken cancellationToken) =>
        {
            await service.SendPasswordResetAsync(request.Email, cancellationToken);
            return TypedResults.Accepted((string?)null);
        }).RequireRateLimiting("auth-sensitive").WithName("ForgotPassword")
            .WithSummary("Request a password reset without revealing account existence");

        group.MapPost("/reset-password", async Task<Results<NoContent, ProblemHttpResult>> (ResetPasswordRequest request, IAuthenticationService service,
            CancellationToken cancellationToken) =>
            await service.ResetPasswordAsync(request, cancellationToken)
                ? TypedResults.NoContent()
                : TypedResults.Problem(statusCode: 400, title: "Invalid reset request",
                    detail: "The token is invalid or expired, or the new password does not satisfy the password policy."))
            .RequireRateLimiting("auth-sensitive").WithName("ResetPassword")
            .WithSummary("Reset a password and revoke all existing sessions").Produces(204).ProducesProblem(400);

        return endpoints;
    }
}
