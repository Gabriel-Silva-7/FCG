using System.ComponentModel.DataAnnotations;
using FCG.Api.Errors;
using Microsoft.AspNetCore.Mvc;

namespace FCG.IntegrationTests.Errors;

[ApiController]
[Route("_test/errors")]
public sealed class ErrorTestController : ControllerBase
{
    public const string ExceptionMessage = "NUNCA_DEVE_VAZAR";

    [HttpGet("status/{statusCode:int}")]
    public IActionResult ReturnStatus(int statusCode) => new EmptyStatusResult(statusCode);

    [HttpGet("throw")]
    public IActionResult Throw() => throw new InvalidOperationException(ExceptionMessage);

    [HttpGet("specific")]
    public IActionResult ReturnSpecificProblem()
    {
        var problemDetails = ApiErrors.GameAlreadyAcquired.ToProblemDetails("/wrong-instance");

        return StatusCode(problemDetails.Status!.Value, problemDetails);
    }

    [HttpGet("unsafe-internal")]
    public IActionResult ReturnUnsafeInternalProblem()
    {
        var problemDetails = ApiErrors.InternalError.ToProblemDetails(
            "/wrong-instance",
            ExceptionMessage);

        return StatusCode(problemDetails.Status!.Value, problemDetails);
    }

    [HttpGet("successful-problem")]
    public IActionResult ReturnSuccessfulProblem() => Ok(new ProblemDetails());

    [HttpPost("validation")]
    public IActionResult Validate(ValidationRequest request) => NoContent();

    public sealed class ValidationRequest
    {
        [Required]
        public string? Name { get; init; }
    }

    private sealed class EmptyStatusResult(int statusCode) : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        }
    }
}
