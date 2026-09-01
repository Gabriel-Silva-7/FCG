namespace FCG.Application.Library;

public sealed class GameAlreadyAcquiredException(Exception innerException)
    : Exception("The game is already in the user library.", innerException);
