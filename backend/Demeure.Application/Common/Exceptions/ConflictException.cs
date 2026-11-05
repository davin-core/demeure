namespace Demeure.Application.Common.Exceptions;

public sealed class ConflictException(string message) : Exception(message);
