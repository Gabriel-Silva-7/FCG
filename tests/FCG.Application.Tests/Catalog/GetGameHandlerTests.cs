using FCG.Application.Catalog;
using FCG.Application.Common;
using FCG.Domain.Catalog;

namespace FCG.Application.Tests.Catalog;

public sealed class GetGameHandlerTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task HandleAsync_WhenActiveGameExists_ReturnsItsPublicSummary()
    {
        var repository = new FakeGameRepository
        {
            FoundGame = new GameReadModel(
                GameId,
                "Celeste",
                "Precision platformer",
                59.90m,
                true),
        };
        var handler = new GetGameHandler(repository);
        using var cancellationSource = new CancellationTokenSource();

        var result = await handler.HandleAsync(GameId, cancellationSource.Token);

        Assert.NotNull(result);
        Assert.Equal(GameId, repository.GameId);
        Assert.Equal(cancellationSource.Token, repository.CancellationToken);
        Assert.Equal(59.90m, result.BasePrice);
        Assert.Equal(59.90m, result.CurrentPrice);
        Assert.Equal(0m, result.DiscountPercentage);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task HandleAsync_WhenGameIsNotInTheActiveCatalog_ReturnsNull()
    {
        var repository = new FakeGameRepository();
        var handler = new GetGameHandler(repository);

        var result = await handler.HandleAsync(GameId, CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class FakeGameRepository : IGameRepository
    {
        public GameReadModel? FoundGame { get; init; }

        public Guid GameId { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<GameReadModel?> FindActiveByIdAsync(
            Guid gameId,
            CancellationToken cancellationToken)
        {
            GameId = gameId;
            CancellationToken = cancellationToken;
            return Task.FromResult(FoundGame);
        }

        public Task<PagedResult<GameReadModel>> SearchActiveAsync(
            string? search,
            GameSortField sortBy,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(Game game) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
