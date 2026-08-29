using FCG.Application.Identity;
using FCG.Api.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCG.Api.Identity;

[ApiController]
[AllowAnonymous]
[Route("api/v1/auth")]
public sealed class AuthController(
    RegisterUserHandler registerUserHandler,
    LoginUserHandler loginUserHandler) : ControllerBase
{
    /// <summary>
    /// Registers a new user account.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registerUserHandler.HandleAsync(
            new RegisterUserCommand(request.Name, request.Email, request.Password),
            cancellationToken);

        if (result.Status is RegisterUserStatus.EmailAlreadyRegistered)
        {
            return Conflict(
                ApiErrors.EmailAlreadyRegistered.ToProblemDetails(HttpContext.Request.Path));
        }

        var user = result.User!;
        var response = new UserResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Role.ToString());

        return Created("/api/v1/me", response);
    }

    /// <summary>
    /// Authenticates a user and issues an access token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await loginUserHandler.HandleAsync(
            new LoginUserCommand(request.Email, request.Password),
            cancellationToken);

        if (result.Status is LoginUserStatus.InvalidCredentials)
        {
            return Unauthorized(
                ApiErrors.InvalidCredentials.ToProblemDetails(HttpContext.Request.Path));
        }

        var token = result.Token!;
        return Ok(new TokenResponse(token.Value, "Bearer", token.ExpiresInSeconds));
    }
}
