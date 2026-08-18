using FCG.Domain.Common;

namespace FCG.Domain.Library;

public sealed class LibraryEntry
{
    public const int PriceScale = 2;

    private LibraryEntry(
        Guid userId,
        Guid gameId,
        DateTime acquiredAtUtc,
        decimal acquisitionPrice)
    {
        UserId = userId;
        GameId = gameId;
        AcquiredAtUtc = acquiredAtUtc;
        AcquisitionPrice = acquisitionPrice;
    }

    public Guid UserId { get; private set; }

    public Guid GameId { get; private set; }

    public DateTime AcquiredAtUtc { get; private set; }

    public decimal AcquisitionPrice { get; private set; }

    public static LibraryEntry Create(
        Guid userId,
        Guid gameId,
        DateTime acquiredAtUtc,
        decimal acquisitionPrice)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier cannot be empty.", nameof(userId));
        }

        if (gameId == Guid.Empty)
        {
            throw new ArgumentException("Game identifier cannot be empty.", nameof(gameId));
        }

        if (acquisitionPrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(acquisitionPrice),
                acquisitionPrice,
                "Acquisition price cannot be negative.");
        }

        if (decimal.Round(acquisitionPrice, PriceScale) != acquisitionPrice)
        {
            throw new ArgumentException(
                $"Acquisition price cannot have more than {PriceScale} decimal places.",
                nameof(acquisitionPrice));
        }

        DomainGuard.EnsureUtc(acquiredAtUtc, nameof(acquiredAtUtc));

        return new LibraryEntry(userId, gameId, acquiredAtUtc, acquisitionPrice);
    }
}
