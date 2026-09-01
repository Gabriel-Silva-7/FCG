using FCG.Application.Common;
using FCG.Application.Library;
using FCG.Domain.Library;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FCG.Infrastructure.Library;

public sealed class LibraryRepository(FcgDbContext dbContext) : ILibraryRepository
{
    public Task<bool> ExistsAsync(
        Guid userId,
        Guid gameId,
        CancellationToken cancellationToken) =>
        dbContext.LibraryEntries
            .AsNoTracking()
            .AnyAsync(entry => entry.UserId == userId && entry.GameId == gameId, cancellationToken);

    public const string PrimaryKeyName = "PK_LibraryEntries";

    public async Task AddAsync(LibraryEntry entry, CancellationToken cancellationToken)
    {
        dbContext.LibraryEntries.Add(entry);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateAcquisition(exception))
        {
            throw new GameAlreadyAcquiredException(exception);
        }
    }

    // Casa SqlState e ConstraintName juntos: qualquer outra violação continua sendo erro
    // inesperado, e não é mascarada como aquisição duplicada.
    private static bool IsDuplicateAcquisition(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: PrimaryKeyName,
        };

    // Consulta histórica: junta Games sem filtrar IsActive de propósito. Um jogo desativado tem de
    // continuar visível para quem já o adquiriu, então o gate de ativos da GAME-04 não entra aqui.
    public async Task<PagedResult<LibraryItem>> SearchByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from entry in dbContext.LibraryEntries.AsNoTracking()
            join game in dbContext.Games.AsNoTracking() on entry.GameId equals game.Id
            where entry.UserId == userId
            select new { entry.GameId, game.Title, entry.AcquiredAtUtc, entry.AcquisitionPrice };

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.AcquiredAtUtc)
            .ThenBy(row => row.GameId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = rows
            .Select(row => new LibraryItem(
                row.GameId,
                row.Title,
                row.AcquiredAtUtc,
                row.AcquisitionPrice))
            .ToArray();

        return new PagedResult<LibraryItem>(items, page, pageSize, totalCount);
    }
}
