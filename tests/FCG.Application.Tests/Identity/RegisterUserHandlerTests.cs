using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Identity;

namespace FCG.Application.Tests.Identity;

public sealed class RegisterUserHandlerTests
{
    private static readonly DateTime UtcNow = DateTime.UnixEpoch;

    [Fact]
    public async Task HandleAsync_WhenInputIsValid_CreatesARegularUserWithOnlyTheHash()
    {
        const string password = "Str0ng!Pass";
        var repository = new FakeUserRepository();
        var passwordHasher = new FakePasswordHasher("HASHED_PASSWORD");
        var handler = CreateHandler(repository, passwordHasher);

        var result = await handler.HandleAsync(
            new RegisterUserCommand("  Gabriel Silva  ", "  Gabriel@Example.com  ", password),
            CancellationToken.None);

        Assert.Equal(RegisterUserStatus.Created, result.Status);
        Assert.NotNull(result.User);
        Assert.Equal("Gabriel Silva", result.User.Name);
        Assert.Equal("gabriel@example.com", result.User.Email);
        Assert.Equal(UserRole.User, result.User.Role);

        var persistedUser = Assert.Single(repository.AddedUsers);
        Assert.Equal("HASHED_PASSWORD", persistedUser.PasswordHash);
        Assert.NotEqual(password, persistedUser.PasswordHash);
        Assert.True(persistedUser.IsActive);
        Assert.Equal(UtcNow, persistedUser.CreatedAtUtc);
        Assert.Equal(password, passwordHasher.LastPassword);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenNormalizedEmailAlreadyExists_ReturnsConflictWithoutHashing()
    {
        var repository = new FakeUserRepository { EmailExists = true };
        var passwordHasher = new FakePasswordHasher("HASHED_PASSWORD");
        var handler = CreateHandler(repository, passwordHasher);

        var result = await handler.HandleAsync(
            new RegisterUserCommand("Gabriel", "  Gabriel@Example.com ", "Str0ng!Pass"),
            CancellationToken.None);

        Assert.Equal(RegisterUserStatus.EmailAlreadyRegistered, result.Status);
        Assert.Null(result.User);
        Assert.Equal("gabriel@example.com", repository.LastCheckedEmail?.Value);
        Assert.Null(passwordHasher.LastPassword);
        Assert.Empty(repository.AddedUsers);
        Assert.Equal(0, repository.SaveChangesCalls);
    }

    [Fact]
    public async Task HandleAsync_WhenInputIsInvalid_ThrowsTypedValidationWithoutPersistence()
    {
        var repository = new FakeUserRepository();
        var passwordHasher = new FakePasswordHasher("HASHED_PASSWORD");
        var handler = CreateHandler(repository, passwordHasher);

        var exception = await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            handler.HandleAsync(
                new RegisterUserCommand("   ", "invalid-email", "weak"),
                CancellationToken.None));

        Assert.Equal(
            new[] { "Email", "Name", "Password" },
            exception.Errors.Keys.Order());
        Assert.Null(repository.LastCheckedEmail);
        Assert.Null(passwordHasher.LastPassword);
        Assert.Empty(repository.AddedUsers);
    }

    [Fact]
    public async Task HandleAsync_WhenAnUnexpectedArgumentExceptionOccurs_DoesNotTranslateItToValidation()
    {
        var repository = new FakeUserRepository();
        var handler = CreateHandler(repository, new ThrowingPasswordHasher());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(
                new RegisterUserCommand("Gabriel", "gabriel@example.com", "Str0ng!Pass"),
                CancellationToken.None));

        Assert.Empty(repository.AddedUsers);
    }

    [Fact]
    public async Task HandleAsync_WhenUniqueEmailRaceIsDetected_ReturnsConflict()
    {
        var repository = new FakeUserRepository { ThrowDuplicateOnSave = true };
        var handler = CreateHandler(repository, new FakePasswordHasher("HASHED_PASSWORD"));

        var result = await handler.HandleAsync(
            new RegisterUserCommand("Gabriel", "gabriel@example.com", "Str0ng!Pass"),
            CancellationToken.None);

        Assert.Equal(RegisterUserStatus.EmailAlreadyRegistered, result.Status);
        Assert.Null(result.User);
        Assert.Single(repository.AddedUsers);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    private static RegisterUserHandler CreateHandler(
        FakeUserRepository repository,
        IPasswordHasher passwordHasher) =>
        new(repository, passwordHasher, new TestClock(UtcNow));

    private sealed class FakeUserRepository : IUserRepository
    {
        public bool EmailExists { get; init; }

        public bool ThrowDuplicateOnSave { get; init; }

        public Email? LastCheckedEmail { get; private set; }

        public List<User> AddedUsers { get; } = [];

        public int SaveChangesCalls { get; private set; }

        public Task<bool> ExistsByEmailAsync(
            Email email,
            CancellationToken cancellationToken)
        {
            LastCheckedEmail = email;
            return Task.FromResult(EmailExists);
        }

        public Task<User?> FindByEmailAsync(
            Email email,
            CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

        public Task<User?> FindByIdAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

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

        public Task<AdminUserSummary?> ChangeStatusAsync(
            Guid userId,
            bool isActive,
            uint expectedVersion,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(User user) => AddedUsers.Add(user);

        public void Remove(User user) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveChangesCalls++;

            if (ThrowDuplicateOnSave)
            {
                throw new EmailAlreadyRegisteredException(new InvalidOperationException());
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHasher(string hash) : IPasswordHasher
    {
        public string? LastPassword { get; private set; }

        public string Hash(string password)
        {
            LastPassword = password;
            return hash;
        }

        public bool Verify(string? passwordHash, string password) => passwordHash == hash;
    }

    private sealed class ThrowingPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) =>
            throw new ArgumentException("Unexpected hashing failure.", nameof(password));

        public bool Verify(string? passwordHash, string password) => false;
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
