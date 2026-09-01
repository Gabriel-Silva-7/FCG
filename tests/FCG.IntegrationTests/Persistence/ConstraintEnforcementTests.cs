using FCG.Application.Identity;
using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Domain.Library;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FCG.IntegrationTests.Persistence;

// O domínio já barra estes valores; estes testes provam que o BANCO também barra, de forma
// independente. Sem isso, um caminho que escape do agregado — SQL cru, seed, migration futura —
// gravaria dado inválido sem ninguém perceber.
public sealed class ConstraintEnforcementTests(FcgApiFixture fixture)
    : DatabaseBackedTest(fixture)
{
    [Fact]
    public async Task NegativeGameBasePrice_IsRejectedByTheCheckConstraint()
    {
        var userId = await SeedUserAsync();

        var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            """
            INSERT INTO "Games" ("Id","Title","Description","BasePrice","IsActive","CreatedAtUtc","CreatedByUserId")
            VALUES (gen_random_uuid(), 'Invalid', NULL, -0.01, true, now(), @userId)
            """,
            ("userId", userId)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, violation.SqlState);
        Assert.Equal("CK_Games_BasePrice_NonNegative", violation.ConstraintName);
    }

    [Fact]
    public async Task NegativeAcquisitionPrice_IsRejectedByTheCheckConstraint()
    {
        var (userId, gameId) = await SeedUserAndGameAsync();

        var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            """
            INSERT INTO "LibraryEntries" ("UserId","GameId","AcquiredAtUtc","AcquisitionPrice")
            VALUES (@userId, @gameId, now(), -0.01)
            """,
            ("userId", userId), ("gameId", gameId)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, violation.SqlState);
        Assert.Equal("CK_LibraryEntries_AcquisitionPrice_NonNegative", violation.ConstraintName);
    }

    [Fact]
    public async Task UnknownUserRole_IsRejectedByTheCheckConstraint()
    {
        var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            """
            INSERT INTO "Users" ("Id","Name","Email","PasswordHash","Role","IsActive","CreatedAtUtc")
            VALUES (gen_random_uuid(), 'Invalid', 'invalid.role@example.com', 'hash', 'Superuser', true, now())
            """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, violation.SqlState);
        Assert.Equal("CK_Users_Role", violation.ConstraintName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100.01)]
    [InlineData(-1)]
    public async Task DiscountOutsideTheOpenRange_IsRejectedByTheCheckConstraint(decimal discount)
    {
        var (userId, gameId) = await SeedUserAndGameAsync();

        var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            """
            INSERT INTO "Promotions" ("Id","GameId","DiscountPercentage","StartsAtUtc","EndsAtUtc","CreatedByUserId")
            VALUES (gen_random_uuid(), @gameId, @discount, now(), now() + interval '1 day', @userId)
            """,
            ("gameId", gameId), ("userId", userId), ("discount", discount)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, violation.SqlState);
        Assert.Equal("CK_Promotions_DiscountPercentage_Range", violation.ConstraintName);
    }

    [Fact]
    public async Task PromotionEndingBeforeItStarts_IsRejectedByTheCheckConstraint()
    {
        var (userId, gameId) = await SeedUserAndGameAsync();

        var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            """
            INSERT INTO "Promotions" ("Id","GameId","DiscountPercentage","StartsAtUtc","EndsAtUtc","CreatedByUserId")
            VALUES (gen_random_uuid(), @gameId, 10, now(), now(), @userId)
            """,
            ("gameId", gameId), ("userId", userId)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, violation.SqlState);
        Assert.Equal("CK_Promotions_DateRange", violation.ConstraintName);
    }

    [Fact]
    public async Task DuplicateEmail_IsRejectedByTheUniqueIndex()
    {
        await SeedUserAsync();

        var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            """
            INSERT INTO "Users" ("Id","Name","Email","PasswordHash","Role","IsActive","CreatedAtUtc")
            VALUES (gen_random_uuid(), 'Clone', 'seed@example.com', 'hash', 'User', true, now())
            """));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, violation.SqlState);
        Assert.Equal("UX_Users_Email", violation.ConstraintName);
    }

    [Fact]
    public async Task LibraryEntryPointingAtNoGame_IsRejectedByTheForeignKey()
    {
        var userId = await SeedUserAsync();

        var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            """
            INSERT INTO "LibraryEntries" ("UserId","GameId","AcquiredAtUtc","AcquisitionPrice")
            VALUES (@userId, gen_random_uuid(), now(), 10.00)
            """,
            ("userId", userId)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, violation.SqlState);
        Assert.Equal("FK_LibraryEntries_Games_GameId", violation.ConstraintName);
    }

    // Restrict, não Cascade: apagar um jogo com biblioteca associada tem de falhar, senão o
    // histórico de aquisição do usuário desaparece junto.
    [Fact]
    public async Task DeletingAGameThatSomeoneAcquired_IsRestricted()
    {
        var (userId, gameId) = await SeedUserAndGameAsync();
        await ExecuteAsync(
            """
            INSERT INTO "LibraryEntries" ("UserId","GameId","AcquiredAtUtc","AcquisitionPrice")
            VALUES (@userId, @gameId, now(), 10.00)
            """,
            ("userId", userId), ("gameId", gameId));

        var violation = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            """DELETE FROM "Games" WHERE "Id" = @gameId""",
            ("gameId", gameId)));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, violation.SqlState);
        Assert.Equal("FK_LibraryEntries_Games_GameId", violation.ConstraintName);
    }

    // Round-trip de decimal: os literais são a especificação. Um mapeamento para float/double
    // poderia devolver 19.989999999999998 em vez de 19.99.
    [Theory]
    [InlineData("0.01")]
    [InlineData("19.99")]
    [InlineData("59.90")]
    [InlineData("9999999999999999.99")]
    public async Task ExactDecimalValues_SurviveTheRoundTrip(string rawPrice)
    {
        var price = decimal.Parse(rawPrice, System.Globalization.CultureInfo.InvariantCulture);
        var userId = await SeedUserAsync();

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var game = Game.Create("Round trip", null, price, userId, DateTime.UnixEpoch);
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var persisted = await dbContext.Games.AsNoTracking().SingleAsync(g => g.Id == game.Id);

        Assert.Equal(price, persisted.BasePrice);
        Assert.Equal(rawPrice, persisted.BasePrice.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
    }

    private async Task<Guid> SeedUserAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = User.Register(
            "Seed",
            Email.Create("seed@example.com"),
            hasher.Hash("Str0ng!Pass"),
            DateTime.UtcNow);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user.Id;
    }

    private async Task<(Guid UserId, Guid GameId)> SeedUserAndGameAsync()
    {
        var userId = await SeedUserAsync();

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var game = Game.Create("Seed", null, 10m, userId, DateTime.UnixEpoch);
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        return (userId, game.Id);
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        await using var connection = new NpgsqlConnection(dbContext.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync();
    }
}
