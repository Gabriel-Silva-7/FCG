using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using FCG.Infrastructure.Persistence.Configurations.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FCG.Infrastructure.Identity;

public sealed class UserRepository(FcgDbContext dbContext) : IUserRepository
{
    public Task<bool> ExistsByEmailAsync(
        Email email,
        CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == email, cancellationToken);

    public Task<User?> FindByEmailAsync(
        Email email,
        CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<User?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<PagedResult<AdminUserSummary>> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var projected = string.IsNullOrWhiteSpace(search)
            ? dbContext.Database.SqlQueryRaw<AdminUserRow>(AdminUserSelectSql)
            : SearchRows(search.Trim());

        var totalCount = await projected.CountAsync(cancellationToken);
        var rows = await projected
            .OrderBy(user => user.CreatedAtUtc)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(user => new AdminUserSummary(
                user.Id,
                user.Name,
                user.Email,
                Enum.Parse<UserRole>(user.Role),
                user.IsActive,
                user.CreatedAtUtc,
                user.Version.ToString()))
            .ToArray();

        return new PagedResult<AdminUserSummary>(items, page, pageSize, totalCount);
    }

    private IQueryable<AdminUserRow> SearchRows(string term) =>
        // SqlQuery mantém este read model fora do modelo persistido e permite buscar o texto da
        // coluna Email sem fazer o value converter tentar transformar o trecho em um Email válido.
        dbContext.Database.SqlQuery<AdminUserRow>(
            $"""
            SELECT "Id", "Name", "Email", "Role", "IsActive", "CreatedAtUtc",
                   xmin::text::bigint AS "Version"
            FROM "Users"
            WHERE strpos(lower("Name"), lower({term})) > 0
               OR strpos(lower("Email"), lower({term})) > 0
            """);

    private const string AdminUserSelectSql =
        """
        SELECT "Id", "Name", "Email", "Role", "IsActive", "CreatedAtUtc",
               xmin::text::bigint AS "Version"
        FROM "Users"
        """;

    private sealed class AdminUserRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string Role { get; init; } = string.Empty;

        public bool IsActive { get; init; }

        public DateTime CreatedAtUtc { get; init; }

        public uint Version { get; init; }
    }

    public void Add(User user) => dbContext.Users.Add(user);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueEmailViolation(exception))
        {
            throw new EmailAlreadyRegisteredException(exception);
        }
    }

    private static bool IsUniqueEmailViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UserConfiguration.UniqueEmailIndexName,
        };
}
