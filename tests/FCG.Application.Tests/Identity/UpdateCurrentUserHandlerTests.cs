using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Identity;

namespace FCG.Application.Tests.Identity;

public sealed class UpdateCurrentUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenDataIsValid_UpdatesAndReturnsTheProfile()
    {
        var user = RegisterUser();
        var repository = new FakeUserRepository(user);
        var handler = new UpdateCurrentUserHandler(repository);

        var result = await handler.HandleAsync(
            new UpdateCurrentUserCommand(user.Id, "  Updated Name  ", "UPDATED@EXAMPLE.COM"),
            CancellationToken.None);

        Assert.Equal(UpdateCurrentUserStatus.Updated, result.Status);
        Assert.Equal("Updated Name", result.User!.Name);
        Assert.Equal("updated@example.com", result.User.Email);
        Assert.True(repository.SaveCalled);
    }

    [Fact]
    public async Task HandleAsync_WhenEmailAlreadyExists_ReturnsConflict()
    {
        var repository = new FakeUserRepository(RegisterUser(), throwDuplicate: true);
        var handler = new UpdateCurrentUserHandler(repository);

        var result = await handler.HandleAsync(
            new UpdateCurrentUserCommand(Guid.NewGuid(), "Updated", "used@example.com"),
            CancellationToken.None);

        Assert.Equal(UpdateCurrentUserStatus.EmailAlreadyRegistered, result.Status);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task HandleAsync_WhenNameAndEmailAreInvalid_ReturnsBothValidationErrors()
    {
        var handler = new UpdateCurrentUserHandler(new FakeUserRepository(RegisterUser()));

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(
            () => handler.HandleAsync(
                new UpdateCurrentUserCommand(Guid.NewGuid(), "   ", "invalid"),
                CancellationToken.None));

        Assert.Contains(nameof(UpdateCurrentUserCommand.Name), exception.Errors.Keys);
        Assert.Contains(nameof(UpdateCurrentUserCommand.Email), exception.Errors.Keys);
    }

    private static User RegisterUser() =>
        User.Register(
            "Original",
            Email.Create("original@example.com"),
            "password-hash",
            DateTime.UnixEpoch);

    private sealed class FakeUserRepository(User? user, bool throwDuplicate = false)
        : IUserRepository
    {
        public bool SaveCalled { get; private set; }

        public Task<User?> FindByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(user);

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalled = true;

            return throwDuplicate
                ? Task.FromException(new EmailAlreadyRegisteredException(new InvalidOperationException()))
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

        public void Remove(User removedUser) => throw new NotSupportedException();
    }
}
