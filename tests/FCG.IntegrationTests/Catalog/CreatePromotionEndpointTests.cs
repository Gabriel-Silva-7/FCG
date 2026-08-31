using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Api.Catalog;
using FCG.Application.Identity;
using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.IntegrationTests.Catalog;

public sealed class CreatePromotionEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Password = "Str0ng!Pass";
    private const string AdminEmail = "admin@example.com";

    [Fact]
    public async Task Administrator_CreatesOverlappingPromotionsForAnActiveGame()
    {
        var (administratorId, game) = await SeedGameAsync(isActive: true);
        using var client = CreateClient();
        await AuthenticateAsync(client, AdminEmail);
        Fixture.Logs.Clear();
        var startsAt = DateTime.UtcNow.AddHours(-1);
        var endsAt = DateTime.UtcNow.AddHours(1);

        using var firstResponse = await client.PostAsJsonAsync(
            Endpoint(game.Id),
            new { discountPercentage = 20m, startsAt, endsAt });
        using var secondResponse = await client.PostAsJsonAsync(
            Endpoint(game.Id),
            new
            {
                discountPercentage = 30m,
                startsAt = startsAt.AddMinutes(10),
                endsAt = endsAt.AddMinutes(10),
            });
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.Equal($"/api/v1/games/{game.Id}", firstResponse.Headers.Location?.AbsolutePath);
        Assert.Equal(game.Id, first.GetProperty("gameId").GetGuid());
        Assert.Equal(20m, first.GetProperty("discountPercentage").GetDecimal());
        Assert.True(first.GetProperty("isCurrentlyActive").GetBoolean());

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var persisted = await dbContext.Promotions
            .AsNoTracking()
            .OrderBy(promotion => promotion.DiscountPercentage)
            .ToArrayAsync();

        Assert.Equal([20m, 30m], persisted.Select(promotion => promotion.DiscountPercentage));
        Assert.All(persisted, promotion => Assert.Equal(administratorId, promotion.CreatedByUserId));

        var events = Fixture.Logs.Entries.Where(entry =>
            entry.Category == typeof(PromotionsController).FullName &&
            entry.Message.StartsWith("PromotionCreated", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, events.Length);
        Assert.All(events, entry =>
        {
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(administratorId, entry.Field("ActorUserId"));
            Assert.Equal(game.Id, entry.Field("TargetGameId"));
            Assert.False(string.IsNullOrWhiteSpace(entry.Field("TraceId")?.ToString()));
        });
    }

    [Fact]
    public async Task InactiveOrUnknownGame_IsRejectedWithoutCreatingPromotion()
    {
        var (_, inactiveGame) = await SeedGameAsync(isActive: false);
        using var client = CreateClient();
        await AuthenticateAsync(client, AdminEmail);
        var request = ValidRequest();

        using var inactiveResponse = await client.PostAsJsonAsync(Endpoint(inactiveGame.Id), request);
        using var unknownResponse = await client.PostAsJsonAsync(Endpoint(Guid.NewGuid()), request);
        var inactiveProblem = await inactiveResponse.Content.ReadFromJsonAsync<JsonElement>();
        var unknownProblem = await unknownResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, inactiveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownResponse.StatusCode);
        Assert.Equal("resource_not_found", inactiveProblem.GetProperty("code").GetString());
        Assert.Equal("resource_not_found", unknownProblem.GetProperty("code").GetString());
        Assert.False(await AnyPromotionAsync());
    }

    [Fact]
    public async Task NonUtcDate_UsesThePublicRequestFieldInTheValidationError()
    {
        var (_, game) = await SeedGameAsync(isActive: true);
        using var client = CreateClient();
        await AuthenticateAsync(client, AdminEmail);

        using var response = await client.PostAsJsonAsync(
            Endpoint(game.Id),
            new
            {
                discountPercentage = 20m,
                startsAt = "2026-09-01T12:00:00",
                endsAt = "2026-09-01T13:00:00Z",
            });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty("StartsAt", out _));
        Assert.False(errors.TryGetProperty("StartsAtUtc", out _));
        Assert.False(await AnyPromotionAsync());
    }

    [Fact]
    public async Task CommonAndAnonymousUsers_CannotCreatePromotion()
    {
        var (_, game) = await SeedGameAsync(isActive: true);
        using var commonClient = CreateClient();
        await RegisterAsync(commonClient, "Common User", "common@example.com");
        await AuthenticateAsync(commonClient, "common@example.com");
        using var anonymousClient = CreateClient();

        using var forbiddenResponse = await commonClient.PostAsJsonAsync(
            Endpoint(game.Id),
            ValidRequest());
        using var unauthorizedResponse = await anonymousClient.PostAsJsonAsync(
            Endpoint(game.Id),
            ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedResponse.StatusCode);
        Assert.False(await AnyPromotionAsync());
    }

    [Fact]
    public async Task InvalidInputs_ReturnCanonicalValidationWithoutCreatingPromotion()
    {
        var (_, game) = await SeedGameAsync(isActive: true);
        using var client = CreateClient();
        await AuthenticateAsync(client, AdminEmail);
        var startsAt = DateTime.UtcNow;
        var invalidRequests = new object[]
        {
            new { discountPercentage = 0m, startsAt, endsAt = startsAt.AddHours(1) },
            new { discountPercentage = 100.01m, startsAt, endsAt = startsAt.AddHours(1) },
            new { discountPercentage = 10.001m, startsAt, endsAt = startsAt.AddHours(1) },
            new { discountPercentage = 20m, startsAt, endsAt = startsAt },
            new { startsAt, endsAt = startsAt.AddHours(1) },
        };

        foreach (var request in invalidRequests)
        {
            using var response = await client.PostAsJsonAsync(Endpoint(game.Id), request);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("validation_error", problem.GetProperty("code").GetString());
            Assert.Equal(400, problem.GetProperty("status").GetInt32());
        }

        Assert.False(await AnyPromotionAsync());
    }

    private async Task<(Guid AdministratorId, Game Game)> SeedGameAsync(bool isActive)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var administrator = User.RegisterAdministrator(
            "Administrator",
            Email.Create(AdminEmail),
            passwordHasher.Hash(Password),
            DateTime.UtcNow);
        var game = Game.Create(
            "Celeste",
            null,
            59.90m,
            administrator.Id,
            DateTime.UtcNow);

        if (!isActive)
        {
            game.Deactivate();
        }

        dbContext.Users.Add(administrator);
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        return (administrator.Id, game);
    }

    private async Task<bool> AnyPromotionAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<FcgDbContext>()
            .Promotions
            .AnyAsync();
    }

    private static object ValidRequest() =>
        new
        {
            discountPercentage = 20m,
            startsAt = DateTime.UtcNow.AddHours(-1),
            endsAt = DateTime.UtcNow.AddHours(1),
        };

    private static string Endpoint(Guid gameId) => $"/api/v1/games/{gameId}/promotions";

    private static async Task RegisterAsync(HttpClient client, string name, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name, email, password = Password });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task AuthenticateAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body.GetProperty("accessToken").GetString());
    }

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
}
