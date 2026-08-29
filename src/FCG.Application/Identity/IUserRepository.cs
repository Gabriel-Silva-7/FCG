using FCG.Application.Common;
using FCG.Domain.Identity;

namespace FCG.Application.Identity;

public interface IUserRepository
{
    Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<AdminUserSummary>> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    void Add(User user);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
