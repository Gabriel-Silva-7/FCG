using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.IntegrationTests.Documentation;

[ApiController]
[Authorize]
[Route("_test/documentation")]
public sealed class OpenApiTestController : ControllerBase
{
    [HttpGet("protected")]
    public IActionResult GetProtected() => NoContent();

    [AllowAnonymous]
    [HttpGet("public")]
    public IActionResult GetPublic() => NoContent();
}
