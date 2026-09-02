using FCG.Application.Common;
using FCG.Domain.Identity;

namespace FCG.Application.Identity;

public sealed record ChangeOwnPasswordCommand(
    Guid UserId,
    string? CurrentPassword,
    string? NewPassword);

public enum ChangeOwnPasswordStatus
{
    Updated,
    NotFound,
    InvalidCurrentPassword,
}

public sealed record ChangeOwnPasswordResult(ChangeOwnPasswordStatus Status);

public sealed class ChangeOwnPasswordHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher)
{
    public async Task<ChangeOwnPasswordResult> HandleAsync(
        ChangeOwnPasswordCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Validate(command);

        var user = await userRepository.FindByIdForUpdateAsync(
            command.UserId,
            cancellationToken);

        if (user is null)
        {
            return new ChangeOwnPasswordResult(ChangeOwnPasswordStatus.NotFound);
        }

        if (!passwordHasher.Verify(user.PasswordHash, command.CurrentPassword!))
        {
            return new ChangeOwnPasswordResult(ChangeOwnPasswordStatus.InvalidCurrentPassword);
        }

        user.ChangePasswordHash(passwordHasher.Hash(command.NewPassword!));
        await userRepository.SaveChangesAsync(cancellationToken);

        return new ChangeOwnPasswordResult(ChangeOwnPasswordStatus.Updated);
    }

    private static void Validate(ChangeOwnPasswordCommand command)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(command.CurrentPassword))
        {
            errors[nameof(command.CurrentPassword)] = ["Current password is required."];
        }

        if (command.NewPassword is null)
        {
            errors[nameof(command.NewPassword)] = ["New password is required."];
        }
        else
        {
            try
            {
                PasswordPolicy.EnsureIsValid(command.NewPassword);
            }
            catch (ArgumentException exception)
            {
                errors[nameof(command.NewPassword)] = [exception.Message];
            }
        }

        if (errors.Count > 0)
        {
            throw new ApplicationValidationException(errors);
        }
    }
}
