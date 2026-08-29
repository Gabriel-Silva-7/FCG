using FCG.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.IntegrationTests.Identity;

[ApiController]
[Route("_test/authorization")]
public sealed class AuthorizationTestController : ControllerBase
{
    [Authorize(Policy = IdentityPolicies.UserOrAdmin)]
    [HttpGet("user-capability")]
    public IActionResult UserCapability() => NoContent();

    [Authorize(Policy = IdentityPolicies.AdminOnly)]
    [HttpGet("admin-capability")]
    public IActionResult AdminCapability() => NoContent();
}
