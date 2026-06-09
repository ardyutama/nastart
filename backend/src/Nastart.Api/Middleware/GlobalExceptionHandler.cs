using System.Net;
using Microsoft.AspNetCore.Diagnostics;

namespace Nastart.Api.Middleware;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            error = "An unexpected error occured.",
            detail = httpContext.RequestServices
                .GetRequiredService<IHostEnvironment>().IsDevelopment() ? exception.Message : null
        }, cancellationToken);

        return true;
    }
}