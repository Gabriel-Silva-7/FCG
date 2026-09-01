using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Api.Library;
using FCG.Application.Identity;
using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.IntegrationTests.Library;

public sealed class AcquireGameEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Endpoint = "/api/v1/me/library";
    private const string Password = "Str0ng!Pass";
    private const string UserEmail = "player@example.com";

    // O jogo tem promoção ativa de 25%: se o preço gravado fosse o basePrice, este teste passaria
    // por acaso. 59.90 * 0.75 = 44.925 -> 44.93 com AwayFromZero.
    [Fact]
    public async Task Acquisition_SnapshotsThePriceInEffectAtThatInstant()
    {
        using var client = CreateClient();
        var gameId = await SeedGameAsync(discountPercentage: 25m);
        var userId = await RegisterAndAuthenticateAsync(client);
        Fixture.Logs.Clear();

        using var response = await client.PostAsJsonAsync(Endpoint, new { gameId });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(gameId, body.GetProperty("gameId").GetGuid());
        Assert.Equal("Celeste", body.GetProperty("title").GetString());
        Assert.Equal(44.93m, body.GetProperty("acquisitionPrice").GetDecimal());
        Assert.NotEqual(59.90m, body.GetProperty("acquisitionPrice").GetDecimal());

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var entry = await dbContext.LibraryEntries.AsNoTracking().SingleAsync();
        Assert.Equal(44.93m, entry.AcquisitionPrice);

        var eventEntry = Assert.Single(Fixture.Logs.Entries.Where(log =>
            log.Category == typeof(LibraryController).FullName &&
            log.Message.StartsWith("GameAddedToLibrary", StringComparison.Ordinal)));
        var requestEntry = Assert.Single(Fixture.Logs.Entries.Where(log =>
            log.Message.StartsWith("HttpRequest", StringComparison.Ordinal)));

        Assert.Equal(LogLevel.Information, eventEntry.Level);
        Assert.Equal(
            "GameAddedToLibrary {ActorUserId} {TargetGameId} {AcquisitionPrice} {TraceId}",
            eventEntry.Field("{OriginalFormat}"));
        Assert.Equal(userId, eventEntry.Field("ActorUserId"));
        Assert.Equal(gameId, eventEntry.Field("TargetGameId"));
        Assert.Equal(44.93m, eventEntry.Field("AcquisitionPrice"));
        Assert.Equal(requestEntry.Field("TraceId"), eventEntry.Field("TraceId"));
    }

    [Fact]
    public async Task UnknownOrInactiveGame_IsNotAcquired()
    {
        using var client = CreateClient();
        var inactiveGameId = await SeedGameAsync(discountPercentage: null, isActive: false);
        await RegisterAndAuthenticateAsync(client);

        using var unknown = await client.PostAsJsonAsync(Endpoint, new { gameId = Guid.NewGuid() });
        using var inactive = await client.PostAsJsonAsync(Endpoint, new { gameId = inactiveGameId });
        var problem = await inactive.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inactive.StatusCode);
        Assert.Equal("resource_not_found", problem.GetProperty("code").GetString());

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        Assert.Equal(0, await dbContext.LibraryEntries.CountAsync());
    }

    [Fact]
    public async Task AnonymousRequest_IsUnauthenticated()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(Endpoint, new { gameId = Guid.NewGuid() });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", problem.GetProperty("code").GetString());
    }

    private async Task<Guid> SeedGameAsync(decimal? discountPercentage, bool isActive = true)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var administrator = User.RegisterAdministrator(
            "Administrator",
            Email.Create("admin@example.com"),
            passwordHasher.Hash(Password),
            DateTime.UtcNow);
        var game = Game.Create("Celeste", null, 59.90m, administrator.Id, DateTime.UnixEpoch);

        if (!isActive)
        {
            game.Deactivate();
        }

        dbContext.Users.Add(administrator);
        dbContext.Games.Add(game);

        if (discountPercentage is { } discount)
        {
            dbContext.Promotions.Add(Promotion.Create(
                game.Id,
                discount,
                DateTime.UnixEpoch,
                DateTime.UnixEpoch.AddYears(100),
                administrator.Id));
        }

        await dbContext.SaveChangesAsync();
        return game.Id;
    }

    private static async Task<Guid> RegisterAndAuthenticateAsync(HttpClient client)
    {
        using var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name = "Player", email = UserEmail, password = Password });
        var registered = await register.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = UserEmail, password = Password });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body.GetProperty("accessToken").GetString());

        return registered.GetProperty("id").GetGuid();
    }

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}
