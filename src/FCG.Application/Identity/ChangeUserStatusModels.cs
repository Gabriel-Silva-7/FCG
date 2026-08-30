namespace FCG.Application.Identity;

public sealed record ChangeUserStatusCommand(
    Guid ActorUserId,
    Guid UserId,
    bool IsActive,
    string? Version);

public enum ChangeUserStatusStatus
{
    Updated,
    NotFound,
    ConcurrencyConflict,
    CannotDeactivateSelf,
}

public sealed record ChangeUserStatusResult(
    ChangeUserStatusStatus Status,
    AdminUserSummary? User)
{
    public static ChangeUserStatusResult Updated(AdminUserSummary user) =>
        new(ChangeUserStatusStatus.Updated, user);

    public static ChangeUserStatusResult NotFound() =>
        new(ChangeUserStatusStatus.NotFound, null);

    public static ChangeUserStatusResult ConcurrencyConflict() =>
        new(ChangeUserStatusStatus.ConcurrencyConflict, null);

    public static ChangeUserStatusResult CannotDeactivateSelf() =>
        new(ChangeUserStatusStatus.CannotDeactivateSelf, null);
}
