using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using DomainUser = FCG.Domain.Identity.User;

namespace FCG.IntegrationTests.Logging;

[ApiController]
[Route("_test/logging")]
public sealed class LoggingTestController : ControllerBase
{
    [HttpGet("plain")]
    public IActionResult GetPlain() => NoContent();

    [HttpPost("echo")]
    public IActionResult PostEcho([FromBody] SensitivePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return NoContent();
    }

    [HttpGet("route/{value}")]
    public IActionResult GetRoute(string value) => NoContent();

    [HttpPost("persist-user")]
    public async Task<IActionResult> PersistUser(
        [FromServices] FcgDbContext dbContext,
        [FromBody] PersistedUserPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var user = DomainUser.Register(
            "Usuario Sentinela",
            Email.Create(payload.Email),
            payload.PasswordHash,
            DateTime.UtcNow);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    public sealed record SensitivePayload(string Email, string Password);

    public sealed record PersistedUserPayload(string Email, string PasswordHash);
}
