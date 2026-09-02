using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Identity;

namespace FCG.Application.Tests.Identity;

public sealed class ChangeOwnPasswordHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenCurrentPasswordMatches_StoresTheNewHash()
    {
        var user = RegisterUser();
        var repository = new FakeUserRepository(user);
        var hasher = new FakePasswordHasher(matches: true);
        var handler = new ChangeOwnPasswordHandler(repository, hasher);

        var result = await handler.HandleAsync(
            new ChangeOwnPasswordCommand(user.Id, "Current!1", "Updated!2"),
            CancellationToken.None);

        Assert.Equal(ChangeOwnPasswordStatus.Updated, result.Status);
        Assert.Equal("hash:Updated!2", user.PasswordHash);
        Assert.True(repository.SaveCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentPasswordDoesNotMatch_DoesNotSave()
    {
        var repository = new FakeUserRepository(RegisterUser());
        var handler = new ChangeOwnPasswordHandler(
            repository,
            new FakePasswordHasher(matches: false));

        var result = await handler.HandleAsync(
            new ChangeOwnPasswordCommand(Guid.NewGuid(), "Wrong!1", "Updated!2"),
            CancellationToken.None);

        Assert.Equal(ChangeOwnPasswordStatus.InvalidCurrentPassword, result.Status);
        Assert.False(repository.SaveCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenNewPasswordIsWeak_ReturnsValidationErrorBeforePersistence()
    {
        var repository = new FakeUserRepository(RegisterUser());
        var handler = new ChangeOwnPasswordHandler(repository, new FakePasswordHasher(matches: true));

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(
                new ChangeOwnPasswordCommand(Guid.NewGuid(), "Current!1", "12345678"),
                CancellationToken.None));

        Assert.Contains(nameof(ChangeOwnPasswordCommand.NewPassword), exception.Errors.Keys);
        Assert.False(repository.SaveCalled);
    }

    private static User RegisterUser() =>
        User.Register(
            "User",
            Email.Create("user@example.com"),
            "old-hash",
            DateTime.UnixEpoch);

    private sealed class FakePasswordHasher(bool matches) : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string? passwordHash, string password) => matches;
    }

    private sealed class FakeUserRepository(User? user) : IUserRepository
    {
        public bool SaveCalled { get; private set; }

        public Task<User?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(user);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalled = true;
            return Task.CompletedTask;
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

        public void Remove(User removedUser) => throw new NotSupportedException();
    }
}
