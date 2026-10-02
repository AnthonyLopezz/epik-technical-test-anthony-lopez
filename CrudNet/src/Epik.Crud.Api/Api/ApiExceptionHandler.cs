using Epik.Crud.Api.Application;
using Microsoft.AspNetCore.Diagnostics;

namespace Epik.Crud.Api.Api;
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, message) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message),
            BadHttpRequestException e => (e.StatusCode,
                "La solicitud no tiene un formato válido. El género debe ser Masculino o Femenino y los números deben ser enteros."),
            _ => (StatusCodes.Status500InternalServerError, "Ocurrió un error interno. Intenta de nuevo más tarde.")
        };
        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled error on {Path}", context.Request.Path);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(ApiResponse.Error(message), ct);
        return true;
    }
}
