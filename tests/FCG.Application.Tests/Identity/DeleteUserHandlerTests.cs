using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Identity;

namespace FCG.Application.Tests.Identity;

public sealed class DeleteUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenAdministratorDeletesAnotherUser_RemovesAndSaves()
    {
        var user = RegisterUser();
        var repository = new FakeUserRepository(user);
        var handler = new DeleteUserHandler(repository);

        var result = await handler.HandleAsync(
            new DeleteUserCommand(Guid.NewGuid(), user.Id),
            CancellationToken.None);

        Assert.Equal(DeleteUserStatus.Deleted, result);
        Assert.Same(user, repository.RemovedUser);
        Assert.True(repository.SaveCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenAdministratorDeletesSelf_ReturnsConflictWithoutPersistence()
    {
        var administratorId = Guid.NewGuid();
        var repository = new FakeUserRepository(RegisterUser());
        var handler = new DeleteUserHandler(repository);

        var result = await handler.HandleAsync(
            new DeleteUserCommand(administratorId, administratorId),
            CancellationToken.None);

        Assert.Equal(DeleteUserStatus.CannotDeleteSelf, result);
        Assert.Null(repository.RemovedUser);
        Assert.False(repository.SaveCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenDatabaseProtectsHistory_ReturnsConflict()
    {
        var handler = new DeleteUserHandler(
            new FakeUserRepository(RegisterUser(), throwRestricted: true));

        var result = await handler.HandleAsync(
            new DeleteUserCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(DeleteUserStatus.HasDependencies, result);
    }

    private static User RegisterUser() =>
        User.Register(
            "User",
            Email.Create("user@example.com"),
            "password-hash",
            DateTime.UnixEpoch);

    private sealed class FakeUserRepository(User? user, bool throwRestricted = false)
        : IUserRepository
    {
        public User? RemovedUser { get; private set; }

        public bool SaveCalled { get; private set; }

        public Task<User?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(user);

        public void Remove(User removedUser) => RemovedUser = removedUser;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalled = true;

            return throwRestricted
                ? Task.FromException(new UserDeletionRestrictedException(new InvalidOperationException()))
                : Task.CompletedTask;
        }

        public Task<bool> ExistsByEmailAsync(Email email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResult<AdminUserSummary>> SearchAsync(
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AdminUserSummary?> ChangeStatusAsync(
            Guid userId,
            bool isActive,
            uint expectedVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(User addedUser) => throw new NotSupportedException();
    }
}
