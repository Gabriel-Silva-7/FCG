namespace FCG.Application.Identity;

public sealed class UserDeletionRestrictedException(Exception innerException)
    : Exception("The user cannot be deleted because related records exist.", innerException);
