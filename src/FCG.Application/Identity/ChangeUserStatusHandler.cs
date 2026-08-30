using System.Globalization;
using FCG.Application.Common;

namespace FCG.Application.Identity;

public sealed class ChangeUserStatusHandler(IUserRepository userRepository)
{
    public async Task<ChangeUserStatusResult> HandleAsync(
        ChangeUserStatusCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!uint.TryParse(
                command.Version,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var expectedVersion))
        {
            throw new ApplicationValidationException(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [nameof(command.Version)] = ["Version must be an unsigned integer string."],
                });
        }

        if (!command.IsActive && command.ActorUserId == command.UserId)
        {
            return ChangeUserStatusResult.CannotDeactivateSelf();
        }

        try
        {
            var user = await userRepository.ChangeStatusAsync(
                command.UserId,
                command.IsActive,
                expectedVersion,
                cancellationToken);

            return user is null
                ? ChangeUserStatusResult.NotFound()
                : ChangeUserStatusResult.Updated(user);
        }
        catch (UserStatusConcurrencyException)
        {
            return ChangeUserStatusResult.ConcurrencyConflict();
        }
    }
}
