using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Persistence;

// Os testes de Configurations/ inspecionam o modelo EF em memória: provam que o mapeamento está
// declarado, não que o PostgreSQL o materializou. Estes fecham essa lacuna consultando o schema
// real criado pela cadeia de migrations.
public sealed class SchemaFromMigrationsTests(FcgApiFixture fixture)
    : DatabaseBackedTest(fixture)
{
    [Fact]
    public async Task Schema_ComesFromTheMigrationChainWithNothingPending()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();

        var applied = (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray();
        var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();

        // EnsureCreated não grava histórico: se o schema tivesse nascido dele, esta lista estaria
        // vazia e as migrations apareceriam como pendentes.
        Assert.NotEmpty(applied);
        Assert.Contains("20260820110842_InitialCreate", applied);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Schema_ContainsExactlyTheFourDomainTables()
    {
        var tables = await QuerySingleColumnAsync(
            """
            SELECT tablename FROM pg_tables
            WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
            ORDER BY tablename
            """);

        Assert.Equal(["Games", "LibraryEntries", "Promotions", "Users"], tables);
    }

    // A lista é literal de propósito: uma constraint removida por engano some daqui, e uma nova
    // obriga quem a criou a declará-la neste teste.
    [Fact]
    public async Task Schema_DeclaresEveryExpectedConstraint()
    {
        var constraints = await QuerySingleColumnAsync(
            """
            SELECT conname FROM pg_constraint
            WHERE connamespace = 'public'::regnamespace
              AND contype IN ('c', 'f', 'p')
              AND conrelid::regclass::text <> '"__EFMigrationsHistory"'
            ORDER BY conname
            """);

        Assert.Equal(
            [
                "CK_Games_BasePrice_NonNegative",
                "CK_LibraryEntries_AcquisitionPrice_NonNegative",
                "CK_Promotions_DateRange",
                "CK_Promotions_DiscountPercentage_Range",
                "CK_Users_Role",
                "FK_Games_Users_CreatedByUserId",
                "FK_LibraryEntries_Games_GameId",
                "FK_LibraryEntries_Users_UserId",
                "FK_Promotions_Games_GameId",
                "FK_Promotions_Users_CreatedByUserId",
                "PK_Games",
                "PK_LibraryEntries",
                "PK_Promotions",
                "PK_Users",
            ],
            constraints);
    }

    [Fact]
    public async Task Schema_KeepsMoneyColumnsAsExactNumeric()
    {
        var columns = await QuerySingleColumnAsync(
            """
            SELECT table_name || '.' || column_name || ' ' || data_type
                   || '(' || numeric_precision || ',' || numeric_scale || ')'
            FROM information_schema.columns
            WHERE table_schema = 'public' AND data_type = 'numeric'
            ORDER BY table_name, column_name
            """);

        // numeric, nunca float/double: dinheiro binário perde centavo em ida e volta.
        Assert.Equal(
            [
                "Games.BasePrice numeric(18,2)",
                "LibraryEntries.AcquisitionPrice numeric(18,2)",
                "Promotions.DiscountPercentage numeric(5,2)",
            ],
            columns);
    }

    private async Task<string[]> QuerySingleColumnAsync(string sql)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        await using var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return [.. values];
    }
}
