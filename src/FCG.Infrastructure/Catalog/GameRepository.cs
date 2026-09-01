using FCG.Application.Common;
using FCG.Application.Catalog;
using FCG.Domain.Catalog;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Catalog;

public sealed class GameRepository(
    FcgDbContext dbContext,
    IClock clock) : IGameRepository
{
    public void Add(Game game) => dbContext.Games.Add(game);

    public async Task<PagedResult<GameReadModel>> SearchActiveAsync(
        string? search,
        GameSortField sortBy,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Games
            .AsNoTracking()
            .Where(game => game.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{EscapeLikePattern(search)}%";
            query = query.Where(game => EF.Functions.ILike(game.Title, pattern, "\\"));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var ordered = Order(query, sortBy);
        var pagedGames = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
        var items = await ProjectWithCurrentDiscount(pagedGames, clock.UtcNow)
            .ToListAsync(cancellationToken);

        return new PagedResult<GameReadModel>(items, page, pageSize, totalCount);
    }

    public Task<GameReadModel?> FindActiveByIdAsync(
        Guid gameId,
        CancellationToken cancellationToken) =>
        ProjectWithCurrentDiscount(
                dbContext.Games
                    .AsNoTracking()
                    .Where(game => game.Id == gameId && game.IsActive),
                clock.UtcNow)
            .SingleOrDefaultAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private IQueryable<GameReadModel> ProjectWithCurrentDiscount(
        IQueryable<Game> games,
        DateTime instantUtc) =>
        games.Select(game => new GameReadModel(
                game.Id,
                game.Title,
                game.Description,
                game.BasePrice,
                game.IsActive,
                dbContext.Promotions
                    .Where(promotion =>
                        promotion.GameId == game.Id &&
                        promotion.StartsAtUtc <= instantUtc &&
                        instantUtc < promotion.EndsAtUtc)
                    .Select(promotion => (decimal?)promotion.DiscountPercentage)
                    .Max() ?? 0m));

    private static IOrderedQueryable<Game> Order(
        IQueryable<Game> query,
        GameSortField sortBy) =>
        sortBy switch
        {
            GameSortField.Title => query
                .OrderBy(game => game.Title)
                .ThenBy(game => game.Id),
            GameSortField.BasePrice => query
                .OrderBy(game => game.BasePrice)
                .ThenBy(game => game.Title)
                .ThenBy(game => game.Id),
            GameSortField.CreatedAt => query
                .OrderBy(game => game.CreatedAtUtc)
                .ThenBy(game => game.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(sortBy), sortBy, null),
        };

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
