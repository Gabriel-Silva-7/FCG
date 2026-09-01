using FCG.Application.Catalog;
using FCG.Application.Common;
using FCG.Domain.Catalog;

namespace FCG.Application.Tests.Catalog;

public sealed class ListGamesHandlerTests
{
    private static readonly GameReadModel SampleGame = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Celeste",
        "Precision platformer",
        59.90m,
        true,
        25m);

    [Theory]
    [InlineData(GameSortFields.Title, GameSortField.Title)]
    [InlineData(GameSortFields.BasePrice, GameSortField.BasePrice)]
    [InlineData(GameSortFields.CreatedAt, GameSortField.CreatedAt)]
    public async Task HandleAsync_WithValidQuery_NormalizesAndMapsTheRepositoryResult(
        string sortBy,
        GameSortField expectedSortField)
    {
        var repository = new FakeGameRepository
        {
            SearchResult = new PagedResult<GameReadModel>([SampleGame], 2, 10, 21),
        };
        var handler = new ListGamesHandler(repository);
        using var cancellationSource = new CancellationTokenSource();

        var result = await handler.HandleAsync(
            new ListGamesQuery("  CELESTE  ", 2, 10, sortBy),
            cancellationSource.Token);

        Assert.Equal("CELESTE", repository.Search);
        Assert.Equal(expectedSortField, repository.SortBy);
        Assert.Equal(2, repository.Page);
        Assert.Equal(10, repository.PageSize);
        Assert.Equal(cancellationSource.Token, repository.CancellationToken);
        Assert.Equal(1, repository.SearchCalls);
        Assert.Equal(21, result.TotalCount);

        var game = Assert.Single(result.Items);
        Assert.Equal(SampleGame.Id, game.Id);
        Assert.Equal(44.93m, game.CurrentPrice);
        Assert.Equal(25m, game.DiscountPercentage);
    }

    public static TheoryData<ListGamesQuery, string> InvalidQueries =>
        new()
        {
            { new ListGamesQuery(null, 0, 20, GameSortFields.Title), "Page" },
            {
                new ListGamesQuery(
                    null,
                    ListGamesQuery.MaxPage + 1,
                    20,
                    GameSortFields.Title),
                "Page"
            },
            { new ListGamesQuery(null, 1, 0, GameSortFields.Title), "PageSize" },
            { new ListGamesQuery(null, 1, 101, GameSortFields.Title), "PageSize" },
            {
                new ListGamesQuery(
                    new string('a', Game.MaxTitleLength + 1),
                    1,
                    20,
                    GameSortFields.Title),
                "Search"
            },
            { new ListGamesQuery(null, 1, 20, "unknown"), "SortBy" },
        };

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task HandleAsync_WithInvalidQuery_ThrowsTypedValidationWithoutQuerying(
        ListGamesQuery query,
        string expectedField)
    {
        var repository = new FakeGameRepository();
        var handler = new ListGamesHandler(repository);

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            handler.HandleAsync(query, CancellationToken.None));

        Assert.Contains(expectedField, exception.Errors.Keys);
        Assert.Equal(0, repository.SearchCalls);
    }

    [Fact]
    public async Task HandleAsync_WithNoSortField_DefaultsToTitle()
    {
        var repository = new FakeGameRepository();
        var handler = new ListGamesHandler(repository);

        await handler.HandleAsync(
            new ListGamesQuery(null, 1, 20, null),
            CancellationToken.None);

        Assert.Equal(GameSortField.Title, repository.SortBy);
    }

    private sealed class FakeGameRepository : IGameRepository
    {
        public PagedResult<GameReadModel> SearchResult { get; init; } =
            new([], 1, 20, 0);

        public string? Search { get; private set; }

        public GameSortField SortBy { get; private set; }

        public int Page { get; private set; }

        public int PageSize { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public int SearchCalls { get; private set; }

        public Task<PagedResult<GameReadModel>> SearchActiveAsync(
            string? search,
            GameSortField sortBy,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Search = search;
            SortBy = sortBy;
            Page = page;
            PageSize = pageSize;
            CancellationToken = cancellationToken;
            SearchCalls++;
            return Task.FromResult(SearchResult);
        }

        public Task<GameReadModel?> FindActiveByIdAsync(
            Guid gameId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(Game game) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
