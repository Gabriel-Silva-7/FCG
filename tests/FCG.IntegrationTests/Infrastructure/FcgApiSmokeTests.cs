using FCG.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FCG.IntegrationTests.Infrastructure;

[Collection(FcgApiCollection.Name)]
public sealed class FcgApiSmokeTests(FcgApiFixture fixture)
{
    [Fact]
    public async Task ApplicationStartsAndAppliesAllMigrationsToEphemeralPostgres()
    {
        using var client = fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });

        using var response = await client.GetAsync("/");

        Assert.InRange((int)response.StatusCode, 200, 499);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();

        Assert.True(await dbContext.Database.CanConnectAsync());

        var expectedConnection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString);
        var actualConnection = new NpgsqlConnectionStringBuilder(
            dbContext.Database.GetConnectionString());

        Assert.NotEqual(5432, expectedConnection.Port);
        Assert.Equal(expectedConnection.Host, actualConnection.Host);
        Assert.Equal(expectedConnection.Port, actualConnection.Port);
        Assert.Equal(expectedConnection.Database, actualConnection.Database);

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

        Assert.NotEmpty(appliedMigrations);
        Assert.Empty(pendingMigrations);
    }
}
