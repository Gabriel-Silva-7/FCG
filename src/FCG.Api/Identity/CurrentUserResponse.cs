namespace FCG.Api.Identity;

public sealed record CurrentUserResponse(Guid Id, string Name, string Email, string Role);
