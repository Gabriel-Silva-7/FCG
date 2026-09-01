using System.IdentityModel.Tokens.Jwt;
using FCG.Api.Contracts;
using System.Security.Claims;
using FCG.Api.Errors;
using FCG.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Api.Controllers;

[ApiController]
[Authorize(Policy = IdentityPolicies.UserOrAdmin)]
[Route("api/v1/me")]
public sealed class MeController(GetCurrentUserHandler getCurrentUserHandler) : ControllerBase
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
}
