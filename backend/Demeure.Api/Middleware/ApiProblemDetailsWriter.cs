using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Demeure.Api.Middleware;

internal sealed class ApiProblemDetailsWriter(IOptions<ProblemDetailsOptions> options) : IProblemDetailsWriter
{
    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        problem.Status ??= context.HttpContext.Response.StatusCode;
        problem.Title ??= ReasonPhrases.GetReasonPhrase(problem.Status.Value);
        problem.Type ??= "about:blank";
        options.Value.CustomizeProblemDetails?.Invoke(context);
        context.HttpContext.Response.StatusCode = problem.Status.Value;

        // Preserve derived validation problems and their field-error dictionaries.
        return new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
            (object)problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.HttpContext.RequestAborted));
    }
}
