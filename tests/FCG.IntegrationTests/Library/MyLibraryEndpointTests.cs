using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.Identity;
using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Library;

public sealed class MyLibraryEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Endpoint = "/api/v1/me/library";
    private const string Password = "Str0ng!Pass";

    // O teste que o card pede nominalmente: A nunca vê B, e um userId malicioso na query é
    // ignorado porque a identidade vem exclusivamente do claim sub.
    [Fact]
    public async Task Library_IsScopedToTheTokenSubjectAndIgnoresAnExternalUserId()
    {
        var gameIds = await SeedGamesAsync("Celeste", "Hollow Knight");
        using var clientA = CreateClient();
        using var clientB = CreateClient();
        var userA = await RegisterAndAuthenticateAsync(clientA, "a@example.com");
        var userB = await RegisterAndAuthenticateAsync(clientB, "b@example.com");

        await AcquireAsync(clientA, gameIds[0]);
        await AcquireAsync(clientB, gameIds[1]);

        using var responseA = await clientA.GetAsync($"{Endpoint}?userId={userB}");
        var bodyA = await responseA.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        Assert.Equal(1, bodyA.GetProperty("totalCount").GetInt32());

        var item = Assert.Single(bodyA.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(gameIds[0], item.GetProperty("gameId").GetGuid());
        Assert.Equal("Celeste", item.GetProperty("title").GetString());
        Assert.False(item.TryGetProperty("userId", out _));
    }

    // Critério 3: jogo desativado depois da aquisição continua no histórico de quem o adquiriu.
    [Fact]
    public async Task DeactivatedGame_RemainsVisibleInTheLibraryThatAcquiredIt()
    {
        var gameIds = await SeedGamesAsync("Celeste");
        using var client = CreateClient();
        await RegisterAndAuthenticateAsync(client, "player@example.com");
        await AcquireAsync(client, gameIds[0]);

        await DeactivateGameAsync(gameIds[0]);

        using var library = await client.GetAsync(Endpoint);
        using var catalog = await client.GetAsync($"/api/v1/games/{gameIds[0]}");
        var body = await library.Content.ReadFromJsonAsync<JsonElement>();

        // Sai do catálogo público, mas permanece na biblioteca.
        Assert.Equal(HttpStatusCode.NotFound, catalog.StatusCode);
        Assert.Equal(HttpStatusCode.OK, library.StatusCode);
        Assert.Equal("Celeste", Assert
            .Single(body.GetProperty("items").EnumerateArray().ToArray())
            .GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=0")]
    [InlineData("?page=0")]
    public async Task InvalidPagination_ReturnsCanonicalValidationProblem(string queryString)
    {
        using var client = CreateClient();
        await RegisterAndAuthenticateAsync(client, "player@example.com");

        using var response = await client.GetAsync($"{Endpoint}{queryString}");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnonymousRequest_IsUnauthenticated()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(Endpoint);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", problem.GetProperty("code").GetString());
    }

    private async Task<Guid[]> SeedGamesAsync(params string[] titles)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var administrator = User.RegisterAdministrator(
            "Administrator",
            Email.Create("admin@example.com"),
            passwordHasher.Hash(Password),
            DateTime.UtcNow);
        dbContext.Users.Add(administrator);

        var games = titles
            .Select(title => Game.Create(title, null, 59.90m, administrator.Id, DateTime.UnixEpoch))
            .ToArray();
        dbContext.Games.AddRange(games);
        await dbContext.SaveChangesAsync();

        return games.Select(game => game.Id).ToArray();
    }

    private async Task DeactivateGameAsync(Guid gameId)
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var game = await dbContext.Games.SingleAsync(current => current.Id == gameId);
        game.Deactivate();
        await dbContext.SaveChangesAsync();
    }

    private static async Task AcquireAsync(HttpClient client, Guid gameId)
    {
        using var response = await client.PostAsJsonAsync(Endpoint, new { gameId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<Guid> RegisterAndAuthenticateAsync(HttpClient client, string email)
    {
        using var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name = "Player", email, password = Password });
        var registered = await register.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });
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
