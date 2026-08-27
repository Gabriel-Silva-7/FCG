namespace FCG.Api.Identity;

public sealed record UserResponse(Guid Id, string Name, string Email, string Role);
