using Demeure.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Demeure.Api.Middleware;

internal sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var (statusCode, title) = exception switch
        {
            ResourceNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Request conflicts with the current resource state"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Access denied"),
            RequestValidationException => (StatusCodes.Status400BadRequest, "Request validation failed"),
            BadHttpRequestException requestException => (requestException.StatusCode, "Invalid HTTP request"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        logger.Log(
            statusCode == StatusCodes.Status500InternalServerError ? LogLevel.Error : LogLevel.Warning,
            exception,
            "API request failed with {StatusCode}; trace {TraceId}",
            statusCode,
            RequestCorrelation.GetTraceId(httpContext));

        httpContext.Response.StatusCode = statusCode;
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = exception is RequestValidationException validation
                ? new HttpValidationProblemDetails(validation.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
                {
                    Status = statusCode,
                    Title = title
                }
                : new ProblemDetails
            {
                Status = statusCode,
                Title = title
            }
        });

        return true;
    }
}
