using FCG.Application.Common;
using FCG.Domain.Library;

namespace FCG.Application.Library;

public interface ILibraryRepository
{
    Task<bool> ExistsAsync(Guid userId, Guid gameId, CancellationToken cancellationToken);

    Task AddAsync(LibraryEntry entry, CancellationToken cancellationToken);

    Task<PagedResult<LibraryItem>> SearchByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
