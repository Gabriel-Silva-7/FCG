using FCG.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Api.Identity;

[ApiController]
[Authorize(Policy = IdentityPolicies.AdminOnly)]
[Route("api/v1/admin/users")]
public sealed class AdminUsersController(ListUsersHandler listUsersHandler) : ControllerBase
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

        var items = result.Items
            .Select(user => new AdminUserResponse(
                user.Id,
                user.Name,
                user.Email,
                user.Role.ToString(),
                user.IsActive,
                user.CreatedAtUtc,
                user.Version))
            .ToArray();

        return Ok(new PagedResponse<AdminUserResponse>(
            items,
            result.Page,
            result.PageSize,
            result.TotalCount));
    }
}
