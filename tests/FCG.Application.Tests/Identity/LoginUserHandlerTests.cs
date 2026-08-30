using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Identity;

namespace FCG.Application.Tests.Identity;

public sealed class LoginUserHandlerTests
{
    private const string Password = "Str0ng!Pass";
    private const string PasswordHash = "HASHED_PASSWORD";

    [Fact]
    public async Task HandleAsync_WhenCredentialsAreValid_ReturnsGeneratedToken()
    {
        var user = CreateUser();
        var repository = new FakeUserRepository(user);
        var passwordHasher = new FakePasswordHasher(verificationResult: true);
        var tokenGenerator = new FakeJwtTokenGenerator();
        var handler = new LoginUserHandler(repository, passwordHasher, tokenGenerator);

        var result = await handler.HandleAsync(
            new LoginUserCommand("  USER@EXAMPLE.COM  ", Password),
            CancellationToken.None);

        Assert.Equal(LoginUserStatus.Authenticated, result.Status);
        Assert.Equal(tokenGenerator.Token, result.Token);
        Assert.Equal("user@example.com", repository.LastEmail?.Value);
        Assert.Equal(PasswordHash, passwordHasher.LastPasswordHash);
        Assert.Equal(Password, passwordHasher.LastPassword);
        Assert.Same(user, tokenGenerator.LastUser);
    }

    [Fact]
    public async Task HandleAsync_WhenAccountDoesNotExist_PerformsDummyVerificationAndReturnsGenericFailure()
    {
        var repository = new FakeUserRepository(user: null);
        var passwordHasher = new FakePasswordHasher(verificationResult: false);
        var tokenGenerator = new FakeJwtTokenGenerator();
        var handler = new LoginUserHandler(repository, passwordHasher, tokenGenerator);

        var result = await handler.HandleAsync(
            new LoginUserCommand("missing@example.com", Password),
            CancellationToken.None);

        Assert.Equal(LoginUserStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Token);
        Assert.Null(passwordHasher.LastPasswordHash);
        Assert.Equal(Password, passwordHasher.LastPassword);
        Assert.Null(tokenGenerator.LastUser);
    }

    [Fact]
    public async Task HandleAsync_WhenPasswordIsWrong_ReturnsTheSameGenericFailure()
    {
        var repository = new FakeUserRepository(CreateUser());
        var passwordHasher = new FakePasswordHasher(verificationResult: false);
        var tokenGenerator = new FakeJwtTokenGenerator();
        var handler = new LoginUserHandler(repository, passwordHasher, tokenGenerator);

        var result = await handler.HandleAsync(
            new LoginUserCommand("user@example.com", "Wr0ng!Pass"),
            CancellationToken.None);

        Assert.Equal(LoginUserStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Token);
        Assert.Equal(PasswordHash, passwordHasher.LastPasswordHash);
        Assert.Null(tokenGenerator.LastUser);
    }

    [Fact]
    public async Task HandleAsync_WhenEmailIsMalformed_StillPerformsDummyVerification()
    {
        var repository = new FakeUserRepository(user: null);
        var passwordHasher = new FakePasswordHasher(verificationResult: false);
        var handler = new LoginUserHandler(repository, passwordHasher, new FakeJwtTokenGenerator());

        var result = await handler.HandleAsync(
            new LoginUserCommand("missing-domain@", Password),
            CancellationToken.None);

        Assert.Equal(LoginUserStatus.InvalidCredentials, result.Status);
        Assert.Equal(0, repository.FindCalls);
        Assert.Null(passwordHasher.LastPasswordHash);
        Assert.Equal(Password, passwordHasher.LastPassword);
    }

    private static User CreateUser() =>
        User.Register(
            "User",
            Email.Create("user@example.com"),
            PasswordHash,
            DateTime.UnixEpoch);

    private sealed class FakeUserRepository(User? user) : IUserRepository
    {
        public Email? LastEmail { get; private set; }

        public int FindCalls { get; private set; }

        public Task<bool> ExistsByEmailAsync(
            Email email,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<User?> FindByEmailAsync(
            Email email,
            CancellationToken cancellationToken)
        {
            LastEmail = email;
            FindCalls++;
            return Task.FromResult(user);
        }

        public Task<User?> FindByIdAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<User?>(null);

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

        public void Add(User newUser) => throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakePasswordHasher(bool verificationResult) : IPasswordHasher
    {
        public string? LastPasswordHash { get; private set; }

        public string? LastPassword { get; private set; }

        public string Hash(string password) => throw new NotSupportedException();

        public bool Verify(string? passwordHash, string password)
        {
            LastPasswordHash = passwordHash;
            LastPassword = password;
            return verificationResult;
        }
    }

    private sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
    {
        public AccessToken Token { get; } = new("ACCESS_TOKEN", 3600);

        public User? LastUser { get; private set; }

        public AccessToken Generate(User user)
        {
            LastUser = user;
            return Token;
        }
    }
}
