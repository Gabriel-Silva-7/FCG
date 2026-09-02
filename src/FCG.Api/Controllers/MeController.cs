using System.IdentityModel.Tokens.Jwt;
using FCG.Api.Contracts;
using System.Security.Claims;
using FCG.Api.Diagnostics;
using FCG.Api.Errors;
using FCG.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Api.Controllers;

[ApiController]
[Authorize(Policy = IdentityPolicies.UserOrAdmin)]
[Route("api/v1/me")]
public sealed class MeController(
    GetCurrentUserHandler getCurrentUserHandler,
    UpdateCurrentUserHandler updateCurrentUserHandler,
    ChangeOwnPasswordHandler changeOwnPasswordHandler,
    ILogger<MeController> logger) : ControllerBase
{
    /// <summary>
    /// Returns the profile of the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            return Unauthorized(
                ApiErrors.Unauthenticated.ToProblemDetails(HttpContext.Request.Path));
        }

        var user = await getCurrentUserHandler.HandleAsync(userId, cancellationToken);

        if (user is null)
        {
            return Unauthorized(
                ApiErrors.Unauthenticated.ToProblemDetails(HttpContext.Request.Path));
        }

        return Ok(new CurrentUserResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Role.ToString()));
    }

    /// <summary>
    /// Updates the authenticated user's name and email address.
    /// </summary>
    [HttpPatch]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        UpdateCurrentUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(
                ApiErrors.Unauthenticated.ToProblemDetails(HttpContext.Request.Path));
        }

        var result = await updateCurrentUserHandler.HandleAsync(
            new UpdateCurrentUserCommand(userId, request.Name, request.Email),
            cancellationToken);

        return result.Status switch
        {
            UpdateCurrentUserStatus.Updated => ProfileUpdated(userId, result.User!),
            UpdateCurrentUserStatus.EmailAlreadyRegistered => Conflict(
                ApiErrors.EmailAlreadyRegistered.ToProblemDetails(HttpContext.Request.Path)),
            UpdateCurrentUserStatus.NotFound => Unauthorized(
                ApiErrors.Unauthenticated.ToProblemDetails(HttpContext.Request.Path)),
            _ => throw new InvalidOperationException($"Unexpected update profile result: {result.Status}."),
        };
    }

    private IActionResult ProfileUpdated(Guid userId, CurrentUser user)
    {
        logger.LogInformation(
            "UserProfileUpdated {ActorUserId} {TraceId}",
            userId,
            TraceIdentity.Resolve(HttpContext));

        return Ok(new CurrentUserResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Role.ToString()));
    }

    /// <summary>
    /// Changes the authenticated user's password after verifying the current password.
    /// </summary>
    [HttpPatch("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        ChangeOwnPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(
                ApiErrors.Unauthenticated.ToProblemDetails(HttpContext.Request.Path));
        }

        var result = await changeOwnPasswordHandler.HandleAsync(
            new ChangeOwnPasswordCommand(
                userId,
                request.CurrentPassword,
                request.NewPassword),
            cancellationToken);

        return result.Status switch
        {
            ChangeOwnPasswordStatus.Updated => PasswordChanged(userId),
            ChangeOwnPasswordStatus.InvalidCurrentPassword => BadRequest(
                ApiErrors.InvalidCurrentPassword.ToProblemDetails(HttpContext.Request.Path)),
            ChangeOwnPasswordStatus.NotFound => Unauthorized(
                ApiErrors.Unauthenticated.ToProblemDetails(HttpContext.Request.Path)),
            _ => throw new InvalidOperationException($"Unexpected change password result: {result.Status}."),
        };
    }

    private IActionResult PasswordChanged(Guid userId)
    {
        logger.LogInformation(
            "UserPasswordChanged {ActorUserId} {TraceId}",
            userId,
            TraceIdentity.Resolve(HttpContext));

        return NoContent();
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);
}
