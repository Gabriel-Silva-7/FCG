using FCG.Application.Common;
using FCG.Domain.Catalog;

namespace FCG.Application.Catalog;

public interface IGameRepository
{
    void Add(Game game);

    Task<PagedResult<GameReadModel>> SearchActiveAsync(
        string? search,
        GameSortField sortBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<GameReadModel?> FindActiveByIdAsync(
        Guid gameId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
