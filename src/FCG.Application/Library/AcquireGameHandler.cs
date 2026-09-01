using FCG.Application.Catalog;
using FCG.Application.Common;
using FCG.Domain.Catalog;
using FCG.Domain.Library;

namespace FCG.Application.Library;

public sealed record AcquireGameCommand(Guid UserId, Guid GameId);

public enum AcquireGameStatus
{
    Acquired,
    GameNotAvailable,
    AlreadyAcquired,
}

public sealed record AcquireGameResult(AcquireGameStatus Status, LibraryItem? Item)
{
    public static AcquireGameResult Acquired(LibraryItem item) =>
        new(AcquireGameStatus.Acquired, item);

    public static AcquireGameResult GameNotAvailable() =>
        new(AcquireGameStatus.GameNotAvailable, null);

    public static AcquireGameResult AlreadyAcquired() =>
        new(AcquireGameStatus.AlreadyAcquired, null);
}

public sealed class AcquireGameHandler(
    IGameRepository gameRepository,
    ILibraryRepository libraryRepository,
    IClock clock)
{
    public async Task<AcquireGameResult> HandleAsync(
        AcquireGameCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await gameRepository.FindActiveByIdAsync(command.GameId, cancellationToken);

        if (game is null)
        {
            return AcquireGameResult.GameNotAvailable();
        }

        if (await libraryRepository.ExistsAsync(command.UserId, command.GameId, cancellationToken))
        {
            return AcquireGameResult.AlreadyAcquired();
        }

        var acquiredAtUtc = clock.UtcNow;
        var price = PricingService.Calculate(game.BasePrice, game.DiscountPercentage);
        var entry = LibraryEntry.Create(
            command.UserId,
            command.GameId,
            acquiredAtUtc,
            price.CurrentPrice);

        try
        {
            await libraryRepository.AddAsync(entry, cancellationToken);
        }
        catch (GameAlreadyAcquiredException)
        {
            // Dois pedidos passaram pelo pre-check ao mesmo tempo; a PK composta decidiu.
            return AcquireGameResult.AlreadyAcquired();
        }

        return AcquireGameResult.Acquired(
            new LibraryItem(game.Id, game.Title, acquiredAtUtc, price.CurrentPrice));
    }
}
