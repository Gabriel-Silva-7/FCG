using FCG.Application.Catalog;
using FCG.Application.Common;
using FCG.Application.Library;
using FCG.Domain.Catalog;
using FCG.Domain.Library;

namespace FCG.Application.Tests.Library;

public sealed class AcquireGameHandlerTests
{
    private static readonly DateTime UtcNow = DateTime.UnixEpoch.AddDays(10);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GameId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // O preço gravado é o vigente no instante da aquisição, não o de tabela: se a promoção mudar
    // amanhã, a biblioteca continua mostrando o que a pessoa adquiriu.
    [Fact]
    public async Task HandleAsync_WithAnActiveDiscount_SnapshotsTheDiscountedPrice()
    {
        var games = new FakeGameRepository(
            new GameReadModel(GameId, "Celeste", null, 59.90m, true, 25m));
        var library = new FakeLibraryRepository();
        var handler = new AcquireGameHandler(games, library, new TestClock(UtcNow));

        var result = await handler.HandleAsync(
            new AcquireGameCommand(UserId, GameId),
            CancellationToken.None);

        Assert.Equal(AcquireGameStatus.Acquired, result.Status);
        Assert.Equal(44.93m, result.Item!.AcquisitionPrice);
        Assert.Equal("Celeste", result.Item.Title);
        Assert.Equal(UtcNow, result.Item.AcquiredAtUtc);

        var persisted = Assert.Single(library.AddedEntries);
        Assert.Equal(UserId, persisted.UserId);
        Assert.Equal(GameId, persisted.GameId);
        Assert.Equal(44.93m, persisted.AcquisitionPrice);
        Assert.Equal(UtcNow, persisted.AcquiredAtUtc);
    }

    // Jogo inativo ou inexistente não é encontrado pelo gate compartilhado da GAME-04.
    [Fact]
    public async Task HandleAsync_WhenTheGameIsNotAvailable_ReturnsNotFoundWithoutPersisting()
    {
        var library = new FakeLibraryRepository();
        var handler = new AcquireGameHandler(
            new FakeGameRepository(game: null),
            library,
            new TestClock(UtcNow));

        var result = await handler.HandleAsync(
            new AcquireGameCommand(UserId, GameId),
            CancellationToken.None);

        Assert.Equal(AcquireGameStatus.GameNotAvailable, result.Status);
        Assert.Null(result.Item);
        Assert.Empty(library.AddedEntries);
    }

    [Fact]
    public async Task HandleAsync_WhenThePrimaryKeyWinsTheRace_ReturnsAlreadyAcquired()
    {
        var games = new FakeGameRepository(
            new GameReadModel(GameId, "Celeste", null, 59.90m, true, 0m));
        var library = new FakeLibraryRepository { ThrowDuplicateOnAdd = true };
        var handler = new AcquireGameHandler(games, library, new TestClock(UtcNow));

        var result = await handler.HandleAsync(
            new AcquireGameCommand(UserId, GameId),
            CancellationToken.None);

        Assert.Equal(AcquireGameStatus.AlreadyAcquired, result.Status);
        Assert.Null(result.Item);
        Assert.Equal(1, library.AddAttempts);
        Assert.Empty(library.AddedEntries);
    }

    private sealed class FakeGameRepository(GameReadModel? game) : IGameRepository
    {
        public Guid? LastGameId { get; private set; }

        public Task<GameReadModel?> FindActiveByIdAsync(
            Guid gameId,
            CancellationToken cancellationToken)
        {
            LastGameId = gameId;
            return Task.FromResult(game);
        }

        public void Add(Game newGame) => throw new NotSupportedException();

        public Task<PagedResult<GameReadModel>> SearchActiveAsync(
            string? search,
            GameSortField sortBy,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeLibraryRepository : ILibraryRepository
    {
        public List<LibraryEntry> AddedEntries { get; } = [];

        public bool ThrowDuplicateOnAdd { get; init; }

        public int AddAttempts { get; private set; }

        public Task<bool> ExistsAsync(Guid userId, Guid gameId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task AddAsync(LibraryEntry entry, CancellationToken cancellationToken)
        {
            AddAttempts++;

            if (ThrowDuplicateOnAdd)
            {
                throw new GameAlreadyAcquiredException(new InvalidOperationException());
            }

            AddedEntries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<PagedResult<LibraryItem>> SearchByUserAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
