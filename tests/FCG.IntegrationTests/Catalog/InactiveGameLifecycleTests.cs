using FCG.Application.Catalog;
using FCG.Application.Identity;
using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Domain.Library;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Catalog;

public sealed class InactiveGameLifecycleTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    [Fact]
    public async Task Deactivation_HidesNewOperationsWithoutDeletingHistoricalData()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var gameRepository = scope.ServiceProvider.GetRequiredService<IGameRepository>();
        var administrator = User.RegisterAdministrator(
            "Administrator",
            Email.Create("admin@example.com"),
            passwordHasher.Hash("Str0ng!Pass"),
            DateTime.UtcNow);
        var game = Game.Create(
            "Historical game",
            null,
            59.90m,
            administrator.Id,
            DateTime.UnixEpoch);
        var promotion = Promotion.Create(
            game.Id,
            20m,
            DateTime.UnixEpoch,
            DateTime.UnixEpoch.AddDays(1),
            administrator.Id);
        var libraryEntry = LibraryEntry.Create(
            administrator.Id,
            game.Id,
            DateTime.UnixEpoch,
            47.92m);

        dbContext.Users.Add(administrator);
        dbContext.Games.Add(game);
        dbContext.Promotions.Add(promotion);
        dbContext.LibraryEntries.Add(libraryEntry);
        await dbContext.SaveChangesAsync();
        Fixture.Logs.Clear();

        game.Deactivate();
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var catalog = await gameRepository.SearchActiveAsync(
            search: null,
            GameSortField.Title,
            page: 1,
            pageSize: 20,
            CancellationToken.None);
        var activeGame = await gameRepository.FindActiveByIdAsync(
            game.Id,
            CancellationToken.None);
        var history = await (
            from entry in dbContext.LibraryEntries.AsNoTracking()
            join persistedGame in dbContext.Games.AsNoTracking()
                on entry.GameId equals persistedGame.Id
            where entry.UserId == administrator.Id
            select new
            {
                persistedGame.Title,
                persistedGame.IsActive,
                entry.AcquisitionPrice,
            }).SingleAsync();

        Assert.Empty(catalog.Items);
        Assert.Equal(0, catalog.TotalCount);
        Assert.Null(activeGame);
        Assert.Equal("Historical game", history.Title);
        Assert.False(history.IsActive);
        Assert.Equal(47.92m, history.AcquisitionPrice);
        Assert.Equal(1, await dbContext.LibraryEntries.CountAsync());
        Assert.Equal(1, await dbContext.Promotions.CountAsync());

        var deactivationSql = string.Join(
            '\n',
            Fixture.Logs.Entries
                .Where(entry =>
                    entry.Category == "Microsoft.EntityFrameworkCore.Database.Command")
                .Select(entry => entry.Message));
        Assert.Contains("UPDATE \"Games\"", deactivationSql, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM", deactivationSql, StringComparison.Ordinal);
    }
}
