using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Api.Errors;

public static class ProblemDetailsConfiguration
{
    public static IServiceCollection AddApiProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(
            options => options.CustomizeProblemDetails = context =>
                ApplyContract(context.HttpContext, context.ProblemDetails));
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.Configure<MvcOptions>(
            options => options.Filters.Add<ProblemDetailsResultFilter>());

        return services;
    }

    internal static void ApplyContract(HttpContext httpContext, ProblemDetails problemDetails)
    {
        var error = ResolveError(httpContext, problemDetails);

        httpContext.Response.StatusCode = error.Status;
        problemDetails.Type = error.Type;
        problemDetails.Title = error.Title;
        problemDetails.Status = error.Status;
        problemDetails.Instance = httpContext.Request.Path.Value;
        problemDetails.Detail = error.Status >= StatusCodes.Status500InternalServerError
            ? null
            : problemDetails.Detail;
        problemDetails.Extensions["code"] = error.Code;
        problemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? httpContext.TraceIdentifier;
    }

    private static ApiError ResolveError(HttpContext httpContext, ProblemDetails problemDetails)
    {
        if (problemDetails.Extensions.TryGetValue("code", out var value) &&
            value is string code &&
            ApiErrors.TryGetByCode(code, out var error))
        {
            return error;
        }

        return ApiErrors.ForStatus(problemDetails.Status ?? httpContext.Response.StatusCode);
    }
}
