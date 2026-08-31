using FCG.Application.Catalog;
using FCG.Application.Common;
using FCG.Domain.Catalog;

namespace FCG.Application.Tests.Catalog;

public sealed class CreatePromotionHandlerTests
{
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GameId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime UtcNow = DateTime.UnixEpoch.AddDays(10);

    [Fact]
    public async Task HandleAsync_WithActiveGame_CreatesAndPersistsPromotion()
    {
        var gameRepository = new FakeGameRepository(ActiveGame());
        var promotionRepository = new FakePromotionRepository();
        var handler = new CreatePromotionHandler(
            gameRepository,
            promotionRepository,
            new TestClock(UtcNow));
        using var cancellationSource = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new CreatePromotionCommand(
                CreatorId,
                GameId,
                25m,
                UtcNow.AddHours(-1),
                UtcNow.AddHours(1)),
            cancellationSource.Token);

        Assert.NotNull(result);
        Assert.Equal(GameId, result.GameId);
        Assert.Equal(25m, result.DiscountPercentage);
        Assert.True(result.IsCurrentlyActive);
        var persisted = Assert.Single(promotionRepository.AddedPromotions);
        Assert.Equal(result.Id, persisted.Id);
        Assert.Equal(CreatorId, persisted.CreatedByUserId);
        Assert.Equal(cancellationSource.Token, promotionRepository.SaveCancellationToken);
        Assert.Equal(cancellationSource.Token, gameRepository.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_WhenGameIsNotActive_ReturnsNullWithoutPersistence()
    {
        var promotionRepository = new FakePromotionRepository();
        var handler = new CreatePromotionHandler(
            new FakeGameRepository(null),
            promotionRepository,
            new TestClock(UtcNow));

        var result = await handler.HandleAsync(
            ValidCommand(),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(promotionRepository.AddedPromotions);
        Assert.Equal(0, promotionRepository.SaveChangesCalls);
    }

    public static TheoryData<CreatePromotionCommand, string> InvalidInputs =>
        new()
        {
            { ValidCommand(discountPercentage: 10.001m), "DiscountPercentage" },
            {
                ValidCommand(
                    startsAtUtc: DateTime.SpecifyKind(UtcNow, DateTimeKind.Unspecified)),
                "StartsAt"
            },
            { ValidCommand(endsAtUtc: UtcNow.AddDays(-2)), "EndsAt" },
        };

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task HandleAsync_WithInvalidPublicInput_ReturnsTypedValidationWithoutPersistence(
        CreatePromotionCommand command,
        string expectedField)
    {
        var promotionRepository = new FakePromotionRepository();
        var handler = new CreatePromotionHandler(
            new FakeGameRepository(ActiveGame()),
            promotionRepository,
            new TestClock(UtcNow));

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            handler.HandleAsync(command, CancellationToken.None));

        Assert.Equal([expectedField], exception.Errors.Keys);
        Assert.Empty(promotionRepository.AddedPromotions);
        Assert.Equal(0, promotionRepository.SaveChangesCalls);
    }

    private static CreatePromotionCommand ValidCommand(
        decimal discountPercentage = 25m,
        DateTime? startsAtUtc = null,
        DateTime? endsAtUtc = null) =>
        new(
            CreatorId,
            GameId,
            discountPercentage,
            startsAtUtc ?? UtcNow.AddDays(-1),
            endsAtUtc ?? UtcNow.AddDays(1));

    private static GameReadModel ActiveGame() =>
        new(GameId, "Celeste", null, 59.90m, true, 0m);

    private sealed class FakeGameRepository(GameReadModel? game) : IGameRepository
    {
        public CancellationToken CancellationToken { get; private set; }

        public Task<GameReadModel?> FindActiveByIdAsync(
            Guid gameId,
            CancellationToken cancellationToken)
        {
            Assert.Equal(GameId, gameId);
            CancellationToken = cancellationToken;
            return Task.FromResult(game);
        }

        public void Add(Game game) => throw new NotSupportedException();

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

    private sealed class FakePromotionRepository : IPromotionRepository
    {
        public List<Promotion> AddedPromotions { get; } = [];

        public int SaveChangesCalls { get; private set; }

        public CancellationToken SaveCancellationToken { get; private set; }

        public void Add(Promotion promotion) => AddedPromotions.Add(promotion);

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
