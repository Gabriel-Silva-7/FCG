using FCG.Application.Catalog;
using FCG.Application.Common;
using FCG.Domain.Catalog;

namespace FCG.Application.Tests.Catalog;

public sealed class CreateGameHandlerTests
{
    private static readonly DateTime UtcNow = DateTime.UnixEpoch;
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task HandleAsync_WithValidInput_CreatesAndPersistsAnActiveGame()
    {
        var repository = new FakeGameRepository();
        var handler = new CreateGameHandler(repository, new TestClock(UtcNow));
        using var cancellationSource = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new CreateGameCommand(
                CreatorId,
                "  Celeste  ",
                "  Precision platformer  ",
                59.90m),
            cancellationSource.Token);

        Assert.Equal("Celeste", result.Title);
        Assert.Equal("Precision platformer", result.Description);
        Assert.Equal(59.90m, result.BasePrice);
        Assert.Equal(59.90m, result.CurrentPrice);
        Assert.Equal(0m, result.DiscountPercentage);
        Assert.True(result.IsActive);

        var persisted = Assert.Single(repository.AddedGames);
        Assert.Equal(result.Id, persisted.Id);
        Assert.Equal(CreatorId, persisted.CreatedByUserId);
        Assert.Equal(UtcNow, persisted.CreatedAtUtc);
        Assert.Equal(cancellationSource.Token, repository.SaveCancellationToken);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WithWhitespaceDescription_NormalizesItToNull()
    {
        var repository = new FakeGameRepository();
        var handler = new CreateGameHandler(repository, new TestClock(UtcNow));

        var result = await handler.HandleAsync(
            new CreateGameCommand(CreatorId, "Celeste", "   ", 59.90m),
            CancellationToken.None);

        Assert.Null(result.Description);
        Assert.Null(Assert.Single(repository.AddedGames).Description);
    }

    public static TheoryData<CreateGameCommand, string> InvalidInputs =>
        new()
        {
            { new CreateGameCommand(CreatorId, null, null, 10m), "Title" },
            { new CreateGameCommand(CreatorId, "   ", null, 10m), "Title" },
            {
                new CreateGameCommand(
                    CreatorId,
                    new string('a', Game.MaxTitleLength + 1),
                    null,
                    10m),
                "Title"
            },
            {
                new CreateGameCommand(
                    CreatorId,
                    "Valid title",
                    new string('a', Game.MaxDescriptionLength + 1),
                    10m),
                "Description"
            },
            { new CreateGameCommand(CreatorId, "Valid title", null, -0.01m), "BasePrice" },
            { new CreateGameCommand(CreatorId, "Valid title", null, 10.001m), "BasePrice" },
            {
                new CreateGameCommand(
                    CreatorId,
                    "Valid title",
                    null,
                    10_000_000_000_000_000m),
                "BasePrice"
            },
        };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task HandleAsync_WithInvalidPublicInput_ThrowsTypedValidationWithoutPersistence(
        CreateGameCommand command,
        string expectedField)
    {
        var repository = new FakeGameRepository();
        var handler = new CreateGameHandler(repository, new TestClock(UtcNow));

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal([expectedField], exception.Errors.Keys);
        Assert.Empty(repository.AddedGames);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WithAnInvalidInternalActor_DoesNotMislabelItAsPublicInput()
    {
        var repository = new FakeGameRepository();
        var handler = new CreateGameHandler(repository, new TestClock(UtcNow));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(
                new CreateGameCommand(Guid.Empty, "Valid title", null, 10m),
                CancellationToken.None));

        Assert.Empty(repository.AddedGames);
    }

    private sealed class FakeGameRepository : IGameRepository
    {
        public List<Game> AddedGames { get; } = [];

        public int SaveChangesCalls { get; private set; }

        public CancellationToken SaveCancellationToken { get; private set; }

        public void Add(Game game) => AddedGames.Add(game);

        public Task<PagedResult<GameReadModel>> SearchActiveAsync(
            string? search,
            GameSortField sortBy,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GameReadModel?> FindActiveByIdAsync(
            Guid gameId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;
            SaveCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
