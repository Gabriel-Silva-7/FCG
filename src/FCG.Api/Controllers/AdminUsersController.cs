using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FCG.Api.Contracts;
using FCG.Api.Diagnostics;
using FCG.Api.Errors;
using FCG.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Api.Controllers;

[ApiController]
[Authorize(Policy = IdentityPolicies.AdminOnly)]
[Route("api/v1/admin/users")]
public sealed class AdminUsersController(
    ListUsersHandler listUsersHandler,
    ChangeUserStatusHandler changeUserStatusHandler,
    ILogger<AdminUsersController> logger) : ControllerBase
{
    /// <summary>
    /// Lists users for administration. The search term matches part of the name or email address.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<AdminUserResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] ListUsersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await listUsersHandler.HandleAsync(
            new ListUsersQuery(request.Search, request.Page, request.PageSize),
            cancellationToken);

        var items = result.Items.Select(ToResponse).ToArray();

        return Ok(new PagedResponse<AdminUserResponse>(
            items,
            result.Page,
            result.PageSize,
            result.TotalCount));
    }

    /// <summary>
    /// Changes whether a user account is active using optimistic concurrency.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<AdminUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        ChangeUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var actorUserId))
        {
            return Unauthorized(
                ApiErrors.Unauthenticated.ToProblemDetails(HttpContext.Request.Path));
        }

        var result = await changeUserStatusHandler.HandleAsync(
            new ChangeUserStatusCommand(
                actorUserId,
                id,
                request.IsActive!.Value,
                request.Version),
            cancellationToken);

        return result.Status switch
        {
            ChangeUserStatusStatus.Updated => StatusChanged(actorUserId, result.User!),
            ChangeUserStatusStatus.NotFound => NotFound(
                ApiErrors.ResourceNotFound.ToProblemDetails(HttpContext.Request.Path)),
            ChangeUserStatusStatus.ConcurrencyConflict => Conflict(
                ApiErrors.ConcurrencyConflict.ToProblemDetails(HttpContext.Request.Path)),
            ChangeUserStatusStatus.CannotDeactivateSelf => RejectSelfDeactivation(actorUserId, id),
            _ => throw new InvalidOperationException($"Unexpected change status result: {result.Status}."),
        };
    }

    private IActionResult StatusChanged(Guid actorUserId, AdminUserSummary user)
    {
        var traceId = TraceIdentity.Resolve(HttpContext);

        if (user.IsActive)
        {
            logger.LogInformation(
                "UserUnblocked {ActorUserId} {TargetUserId} {TraceId}",
                actorUserId,
                user.Id,
                traceId);
        }
        else
        {
            logger.LogInformation(
                "UserBlocked {ActorUserId} {TargetUserId} {TraceId}",
                actorUserId,
                user.Id,
                traceId);
        }

        return Ok(ToResponse(user));
    }

    private IActionResult RejectSelfDeactivation(Guid actorUserId, Guid targetUserId)
    {
        logger.LogWarning(
            "UserSelfDeactivationRejected {ActorUserId} {TargetUserId} {TraceId}",
            actorUserId,
            targetUserId,
            TraceIdentity.Resolve(HttpContext));

        return Conflict(
            ApiErrors.CannotDeactivateSelf.ToProblemDetails(HttpContext.Request.Path));
    }

    private static AdminUserResponse ToResponse(AdminUserSummary user) =>
        new(
            user.Id,
            user.Name,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAtUtc,
            user.Version);
}
