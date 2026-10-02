namespace Epik.Crud.Api.Application;

public sealed class ValidationException(string message) : Exception(message);
public sealed class ConflictException(string message) : Exception(message);
