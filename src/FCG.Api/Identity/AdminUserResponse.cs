namespace FCG.Api.Identity;

public sealed record AdminUserResponse(
    Guid Id,
    string Name,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    string Version);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
