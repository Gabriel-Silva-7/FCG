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
    private const string PlayerName = "Player";

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

    // O usuário comum existe só para a demonstração: permite mostrar 403 e isolamento de
    // biblioteca sem precisar cadastrar alguém ao vivo. Diferente do administrador, um conflito
    // aqui não derruba o startup — qualquer conta já existente com esse e-mail serve.
    public async Task<AdminBootstrapResult> EnsurePlayerAsync(
        string emailValue,
        string password,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(emailValue);
        PasswordPolicy.EnsureIsValid(password);

        if (await userRepository.FindByEmailAsync(email, cancellationToken) is not null)
        {
            return AdminBootstrapResult.AlreadyConfigured;
        }

        userRepository.Add(User.Register(
            PlayerName,
            email,
            passwordHasher.Hash(password),
            clock.UtcNow));
        await userRepository.SaveChangesAsync(cancellationToken);

        return AdminBootstrapResult.Created;
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
