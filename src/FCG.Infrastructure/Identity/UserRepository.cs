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
