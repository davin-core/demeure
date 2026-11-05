namespace Demeure.Application.Common.Exceptions;

public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The request failed validation.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
