namespace FCG.Api.Identity;

public sealed record AdminUserResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    string Version);
