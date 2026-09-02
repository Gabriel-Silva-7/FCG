namespace FCG.Application.Identity;

public sealed record DeleteUserCommand(Guid ActorUserId, Guid UserId);

public enum DeleteUserStatus
{
    Deleted,
    NotFound,
    CannotDeleteSelf,
    HasDependencies,
}

public sealed class DeleteUserHandler(IUserRepository userRepository)
{
    public async Task<DeleteUserStatus> HandleAsync(
        DeleteUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.ActorUserId == command.UserId)
        {
            return DeleteUserStatus.CannotDeleteSelf;
        }

        var user = await userRepository.FindByIdForUpdateAsync(
            command.UserId,
            cancellationToken);

        if (user is null)
        {
            return DeleteUserStatus.NotFound;
        }

        userRepository.Remove(user);

        try
        {
            await userRepository.SaveChangesAsync(cancellationToken);
            return DeleteUserStatus.Deleted;
        }
        catch (UserDeletionRestrictedException)
        {
            return DeleteUserStatus.HasDependencies;
        }
    }
}
