using System.Text.Json.Serialization;
using Demeure.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics;

namespace Demeure.Api.Extensions;

internal static class DependencyInjection
{
    internal static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddApiDocumentation();
        services.AddHealthChecks();
        services.AddValidation();
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.Configure<ExceptionHandlerOptions>(options =>
            options.SuppressDiagnosticsCallback = _ => true);
        services.AddSingleton<IProblemDetailsWriter, ApiProblemDetailsWriter>();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance = context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["traceId"] = RequestCorrelation.GetTraceId(context.HttpContext);
            };
        });
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.PropertyNameCaseInsensitive = false;
            options.SerializerOptions.AllowDuplicateProperties = false;
        });

        return services;
    }
}
