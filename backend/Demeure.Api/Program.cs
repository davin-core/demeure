using Demeure.Api.Endpoints;
using Demeure.Api.Extensions;
using Demeure.Api.Middleware;
using Demeure.Application;
using Demeure.Infrastructure;
using Demeure.Api.Modules.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services
    .AddApplication()
    .AddInfrastructure(
        builder.Configuration["Database:Provider"] ?? "PostgreSQL",
        builder.Configuration.GetConnectionString("Demeure"))
    .AddPresentation();
builder.Services.AddAuthenticationConfiguration(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseRouting();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<ResourceResponseMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<CsrfMiddleware>();
app.UseMiddleware<OrganizationContextMiddleware>();
app.UseAuthorization();
app.UseMiddleware<CollectionQueryMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapApiDocumentation();
}

app.MapHealthEndpoints();
app.MapVersionedApi();
app.Run();

/// <summary>API entry point exposed for integration testing.</summary>
public partial class Program;
