using FCG.Application.Identity;
using Microsoft.AspNetCore.Identity;

namespace FCG.Infrastructure.Identity;

public sealed class AspNetCorePasswordHasher : IPasswordHasher
{
    private static readonly object UserContext = new();

    private readonly PasswordHasher<object> _passwordHasher = new();

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        return _passwordHasher.HashPassword(UserContext, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        ArgumentNullException.ThrowIfNull(password);

        return _passwordHasher.VerifyHashedPassword(UserContext, passwordHash, password) is not
            PasswordVerificationResult.Failed;
    }
}
