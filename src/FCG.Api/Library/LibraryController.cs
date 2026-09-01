using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FCG.Api.Common;
using FCG.Api.Diagnostics;
using FCG.Api.Errors;
using FCG.Application.Identity;
using FCG.Application.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Api.Library;

[ApiController]
[Authorize(Policy = IdentityPolicies.UserOrAdmin)]
[Route("api/v1/me/library")]
public sealed class LibraryController(
    AcquireGameHandler acquireGameHandler,
    GetMyLibraryHandler getMyLibraryHandler,
    ILogger<LibraryController> logger) : ControllerBase
{
    /// <summary>
    /// Lists the games acquired by the authenticated user.
    /// </summary>
    /// <remarks>
    /// The acquisition price is the snapshot taken when the game was acquired, not today's price.
    /// Games that were deactivated afterwards remain listed as history.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResponse<LibraryItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] ListLibraryRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Unauthorized(
                ApiErrors.Unauthenticated.ToProblemDetails(HttpContext.Request.Path));
        }

        var result = await getMyLibraryHandler.HandleAsync(
            new GetMyLibraryQuery(userId, request.Page, request.PageSize),
            cancellationToken);

        return Ok(new PagedResponse<LibraryItemResponse>(
            result.Items.Select(ToResponse).ToArray(),
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    /// <summary>
    /// Adds an active game to the authenticated user's library.
    /// </summary>
    /// <remarks>
    /// The acquisition is simulated: it does not involve any charge, payment or billing. The
    /// acquisition price is a snapshot of the price in effect at that instant, so later promotion
    /// changes never rewrite it.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<LibraryItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Acquire(
        AcquireGameRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Unauthorized(
                ApiErrors.Unauthenticated.ToProblemDetails(HttpContext.Request.Path));
        }

        var result = await acquireGameHandler.HandleAsync(
            new AcquireGameCommand(userId, request.GameId!.Value),
            cancellationToken);

        switch (result.Status)
        {
            case AcquireGameStatus.Acquired:
                var item = result.Item!;

                logger.LogInformation(
                    "GameAddedToLibrary {ActorUserId} {TargetGameId} {AcquisitionPrice} {TraceId}",
                    userId,
                    item.GameId,
                    item.AcquisitionPrice,
                    TraceIdentity.Resolve(HttpContext));

                return Created($"/api/v1/games/{item.GameId}", ToResponse(item));
            case AcquireGameStatus.GameNotAvailable:
                return NotFound(
                    ApiErrors.ResourceNotFound.ToProblemDetails(HttpContext.Request.Path));
            case AcquireGameStatus.AlreadyAcquired:
                return Conflict(
                    ApiErrors.GameAlreadyAcquired.ToProblemDetails(HttpContext.Request.Path));
            default:
                throw new InvalidOperationException(
                    $"Unexpected acquisition result: {result.Status}.");
        }
    }

    private static LibraryItemResponse ToResponse(LibraryItem item) =>
        new(item.GameId, item.Title, item.AcquiredAtUtc, item.AcquisitionPrice);
}
