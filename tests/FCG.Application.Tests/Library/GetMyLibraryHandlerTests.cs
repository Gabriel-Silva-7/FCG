using FCG.Application.Common;
using FCG.Application.Library;
using FCG.Domain.Library;

namespace FCG.Application.Tests.Library;

public sealed class GetMyLibraryHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1000001, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task HandleAsync_WithPaginationOutsideTheContract_ThrowsWithoutQuerying(
        int page,
        int pageSize)
    {
        var repository = new FakeLibraryRepository();
        var handler = new GetMyLibraryHandler(repository);

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            handler.HandleAsync(
                new GetMyLibraryQuery(UserId, page, pageSize),
                CancellationToken.None));

        Assert.NotEmpty(exception.Errors);
        Assert.False(repository.WasQueried);
    }

    [Fact]
    public async Task HandleAsync_WithinTheContract_DelegatesToTheRepository()
    {
        var repository = new FakeLibraryRepository();
        var handler = new GetMyLibraryHandler(repository);

        var result = await handler.HandleAsync(
            new GetMyLibraryQuery(UserId, 2, 50),
            CancellationToken.None);

        Assert.True(repository.WasQueried);
        Assert.Equal(UserId, repository.LastUserId);
        Assert.Equal(2, repository.LastPage);
        Assert.Equal(50, repository.LastPageSize);
        Assert.Empty(result.Items);
    }

    private sealed class FakeLibraryRepository : ILibraryRepository
    {
        public bool WasQueried { get; private set; }

        public Guid LastUserId { get; private set; }

        public int LastPage { get; private set; }

        public int LastPageSize { get; private set; }

        public Task<PagedResult<LibraryItem>> SearchByUserAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            WasQueried = true;
            LastUserId = userId;
            LastPage = page;
            LastPageSize = pageSize;

            return Task.FromResult(new PagedResult<LibraryItem>([], page, pageSize, 0));
        }

        public Task<bool> ExistsAsync(
            Guid userId,
            Guid gameId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(LibraryEntry entry, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
