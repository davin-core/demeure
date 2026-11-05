namespace Demeure.Application.Common.Exceptions;

public sealed class ResourceNotFoundException(string message) : Exception(message);
