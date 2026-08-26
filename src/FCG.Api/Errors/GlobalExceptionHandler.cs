using FCG.Api.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;

namespace FCG.Api.Errors;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Mensagem e stack trace podem conter dados sensíveis, por isso não passamos a exceção.
        logger.LogError(
            "UnhandledException {TraceId} {ExceptionType}",
            TraceIdentity.Resolve(httpContext),
            exception.GetType().FullName);

        var problemDetails = ApiErrors.InternalError.ToProblemDetails(httpContext.Request.Path);

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
            });
    }
}
