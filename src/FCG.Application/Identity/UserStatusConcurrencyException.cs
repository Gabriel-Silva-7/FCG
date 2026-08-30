namespace FCG.Application.Identity;

public sealed class UserStatusConcurrencyException(Exception innerException)
    : Exception("The user status was changed by another writer.", innerException);
