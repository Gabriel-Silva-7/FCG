using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.Catalog;
using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Infrastructure.Catalog;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Catalog;

public sealed class PromotionPricingTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Password = "Str0ng!Pass";

    [Fact]
    public async Task Catalog_UsesHighestActiveDiscountInTwoQueries()
    {
        var instant = DateTime.UtcNow;
        var (game, administratorId) = await SeedGameAsync();
        await SeedPromotionsAsync(
            game.Id,
            administratorId,
            new PromotionSeed(10m, instant.AddHours(-2), instant.AddHours(2)),
            new PromotionSeed(25m, instant.AddHours(-1), instant.AddHours(1)),
            new PromotionSeed(50m, instant.AddHours(1), instant.AddHours(2)),
            new PromotionSeed(40m, instant.AddHours(-2), instant.AddHours(-1)));
        using var client = Fixture.Factory.CreateClient();
        Fixture.Logs.Clear();

        using var response = await client.GetAsync("/api/v1/games");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = Assert.Single(body.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(25m, item.GetProperty("discountPercentage").GetDecimal());
        Assert.Equal(44.93m, item.GetProperty("currentPrice").GetDecimal());

        var commands = Fixture.Logs.Entries.Where(entry =>
            entry.Category == "Microsoft.EntityFrameworkCore.Database.Command" &&
            entry.Message.StartsWith("Executed DbCommand", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, commands.Length);
        var itemQuery = Assert.Single(commands.Where(command =>
            command.Message.Contains("Promotions", StringComparison.Ordinal)));
        Assert.Contains("max(", itemQuery.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SqlProjectionAndDomainPricing_AgreeAtPromotionBoundaries()
    {
        var startsAt = DateTime.UnixEpoch.AddDays(10);
        var endsAt = startsAt.AddHours(1);
        var (game, administratorId) = await SeedGameAsync();
        var promotion = Assert.Single(await SeedPromotionsAsync(
            game.Id,
            administratorId,
            new PromotionSeed(20m, startsAt, endsAt)));
        var instants = new[]
        {
            startsAt,
            endsAt.AddTicks(-10),
            endsAt,
        };

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();

        foreach (var instant in instants)
        {
            var repository = new GameRepository(dbContext, new FixedClock(instant));
            var projected = await repository.FindActiveByIdAsync(game.Id, CancellationToken.None);
            var expected = PricingService.Calculate(game.BasePrice, [promotion], instant);
            var actual = PricingService.Calculate(
                projected!.BasePrice,
                projected.DiscountPercentage);

            Assert.Equal(expected, actual);
        }
    }

    private async Task<(Game Game, Guid AdministratorId)> SeedGameAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var administrator = User.RegisterAdministrator(
            "Administrator",
            Email.Create("admin@example.com"),
            passwordHasher.Hash(Password),
            DateTime.UtcNow);
        var game = Game.Create(
            "Celeste",
            null,
            59.90m,
            administrator.Id,
            DateTime.UtcNow);

        dbContext.Users.Add(administrator);
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        return (game, administrator.Id);
    }

    private async Task<IReadOnlyList<Promotion>> SeedPromotionsAsync(
        Guid gameId,
        Guid creatorId,
        params PromotionSeed[] seeds)
    {
        var promotions = seeds
            .Select(seed => Promotion.Create(
                gameId,
                seed.DiscountPercentage,
                seed.StartsAtUtc,
                seed.EndsAtUtc,
                creatorId))
            .ToArray();
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        dbContext.Promotions.AddRange(promotions);
        await dbContext.SaveChangesAsync();

        return promotions;
    }

    private sealed record PromotionSeed(
        decimal DiscountPercentage,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
