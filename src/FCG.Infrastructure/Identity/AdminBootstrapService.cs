using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Identity;

namespace FCG.Infrastructure.Identity;

public sealed class AdminBootstrapService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IClock clock)
{
    private const string AdministratorName = "Administrator";

    public async Task<AdminBootstrapOutcome> ExecuteAsync(
        string emailValue,
        string password,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(emailValue);
        PasswordPolicy.EnsureIsValid(password);
        var existingUser = await userRepository.FindByEmailAsync(email, cancellationToken);

        if (existingUser is not null)
        {
            if (existingUser.Role is UserRole.Administrator && existingUser.IsActive)
            {
                return new AdminBootstrapOutcome(
                    AdminBootstrapResult.AlreadyConfigured,
                    existingUser.Id,
                    existingUser.Email.Value);
            }

            throw new AdminBootstrapConflictException();
        }

        var passwordHash = passwordHasher.Hash(password);
        var administrator = User.RegisterAdministrator(
            AdministratorName,
            email,
            passwordHash,
            clock.UtcNow);

        userRepository.Add(administrator);
        await userRepository.SaveChangesAsync(cancellationToken);

        return new AdminBootstrapOutcome(
            AdminBootstrapResult.Created,
            administrator.Id,
            administrator.Email.Value);
    }
}

public sealed record AdminBootstrapOutcome(
    AdminBootstrapResult Result,
    Guid UserId,
    string Email);

public enum AdminBootstrapResult
{
    Created,
    AlreadyConfigured,
}

public sealed class AdminBootstrapConflictException()
    : Exception("The configured administrator conflicts with an existing account.");
