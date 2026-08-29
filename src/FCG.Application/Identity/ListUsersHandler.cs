using FCG.Application.Common;
using FCG.Domain.Identity;

namespace FCG.Application.Identity;

public sealed record ListUsersQuery(string? Search, int Page, int PageSize);

public sealed record AdminUserSummary(
    Guid Id,
    string Name,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    string Version);

public sealed class ListUsersHandler(IUserRepository userRepository)
{
    public Task<PagedResult<AdminUserSummary>> HandleAsync(
        ListUsersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return userRepository.SearchAsync(
            query.Search,
            query.Page,
            query.PageSize,
            cancellationToken);
    }
}
