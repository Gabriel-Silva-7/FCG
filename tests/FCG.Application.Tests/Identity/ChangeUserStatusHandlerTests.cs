using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Identity;

namespace FCG.Application.Tests.Identity;

public sealed class ChangeUserStatusHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidVersion_DelegatesTheExpectedConcurrencyToken()
    {
        var userId = Guid.NewGuid();
        var expected = new AdminUserSummary(
            userId,
            "User",
            "user@example.com",
            UserRole.User,
            false,
            DateTime.UnixEpoch,
            "43");
        var repository = new FakeUserRepository(expected);
        var handler = new ChangeUserStatusHandler(repository);

        var result = await handler.HandleAsync(
            new ChangeUserStatusCommand(Guid.NewGuid(), userId, false, "42"),
            CancellationToken.None);

        Assert.Equal(ChangeUserStatusStatus.Updated, result.Status);
        Assert.Same(expected, result.User);
        Assert.Equal(userId, repository.LastUserId);
        Assert.False(repository.LastIsActive);
        Assert.Equal(42u, repository.LastExpectedVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData(" 1")]
    [InlineData("4294967296")]
    [InlineData("not-a-version")]
    public async Task HandleAsync_WithInvalidVersion_ReturnsAValidationError(string? version)
    {
        var repository = new FakeUserRepository(user: null);
        var handler = new ChangeUserStatusHandler(repository);

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(
                new ChangeUserStatusCommand(Guid.NewGuid(), Guid.NewGuid(), false, version),
                CancellationToken.None));

        Assert.Contains(nameof(ChangeUserStatusCommand.Version), exception.Errors.Keys);
        Assert.Null(repository.LastUserId);
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryDoesNotFindUser_ReturnsNotFound()
    {
        var handler = new ChangeUserStatusHandler(new FakeUserRepository(user: null));

        var result = await handler.HandleAsync(
            new ChangeUserStatusCommand(Guid.NewGuid(), Guid.NewGuid(), false, "42"),
            CancellationToken.None);

        Assert.Equal(ChangeUserStatusStatus.NotFound, result.Status);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task HandleAsync_WhenPersistenceDetectsConcurrency_ReturnsConflict()
    {
        var repository = new FakeUserRepository(user: null, throwConcurrency: true);
        var handler = new ChangeUserStatusHandler(repository);

        var result = await handler.HandleAsync(
            new ChangeUserStatusCommand(Guid.NewGuid(), Guid.NewGuid(), false, "42"),
            CancellationToken.None);

        Assert.Equal(ChangeUserStatusStatus.ConcurrencyConflict, result.Status);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task HandleAsync_WhenAdministratorDeactivatesSelf_ReturnsConflictWithoutPersistence()
    {
        var administratorId = Guid.NewGuid();
        var repository = new FakeUserRepository(user: null);
        var handler = new ChangeUserStatusHandler(repository);

        var result = await handler.HandleAsync(
            new ChangeUserStatusCommand(
                administratorId,
                administratorId,
                IsActive: false,
                Version: "42"),
            CancellationToken.None);

        Assert.Equal(ChangeUserStatusStatus.CannotDeactivateSelf, result.Status);
        Assert.Null(result.User);
        Assert.Null(repository.LastUserId);
    }

    private sealed class FakeUserRepository(
        AdminUserSummary? user,
        bool throwConcurrency = false) : IUserRepository
    {
        public Guid? LastUserId { get; private set; }

        public bool LastIsActive { get; private set; }

        public uint? LastExpectedVersion { get; private set; }

        public Task<AdminUserSummary?> ChangeStatusAsync(
            Guid userId,
            bool isActive,
            uint expectedVersion,
            CancellationToken cancellationToken)
        {
            LastUserId = userId;
            LastIsActive = isActive;
            LastExpectedVersion = expectedVersion;

            if (throwConcurrency)
            {
                throw new UserStatusConcurrencyException(new InvalidOperationException());
            }

            return Task.FromResult(user);
        }

        public Task<bool> ExistsByEmailAsync(
            Email email,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<User?> FindByEmailAsync(
            Email email,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<User?> FindByIdAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<User?> FindByIdForUpdateAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<AdminUserSummary>> SearchAsync(
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(User user) => throw new NotSupportedException();

        public void Remove(User user) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
