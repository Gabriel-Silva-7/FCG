using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.Identity;
using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Flows;

public sealed class PrimaryUserJourneyTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Password = "Str0ng!Pass";

    [Fact]
    public async Task MainPresentationFlow_WorksFromRegistrationThroughImmediateBlock()
    {
        await SeedAdministratorAsync();
        using var administrator = CreateClient();
        using var playerA = CreateClient();
        using var playerB = CreateClient();

        await AuthenticateAsync(administrator, "admin@example.com");
        var playerAId = await RegisterAndAuthenticateAsync(
            playerA,
            "Player A",
            "player-a@example.com");
        await RegisterAndAuthenticateAsync(playerB, "Player B", "player-b@example.com");

        using var meResponse = await playerA.GetAsync("/api/v1/me");
        var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal(playerAId, me.GetProperty("id").GetGuid());
        Assert.Equal("User", me.GetProperty("role").GetString());

        using var createGameResponse = await administrator.PostAsJsonAsync(
            "/api/v1/games",
            new { title = "Celeste", description = "Platform game", basePrice = 59.90m });
        var createdGame = await createGameResponse.Content.ReadFromJsonAsync<JsonElement>();
        var gameId = createdGame.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Created, createGameResponse.StatusCode);
        Assert.Equal($"/api/v1/games/{gameId}", LocationPath(createGameResponse));

        var startsAt = DateTime.UtcNow.AddMinutes(-1);
        var endsAt = DateTime.UtcNow.AddDays(1);
        using var createPromotionResponse = await administrator.PostAsJsonAsync(
            $"/api/v1/games/{gameId}/promotions",
            new { discountPercentage = 25m, startsAt, endsAt });
        Assert.Equal(HttpStatusCode.Created, createPromotionResponse.StatusCode);
        Assert.Equal($"/api/v1/games/{gameId}", LocationPath(createPromotionResponse));

        using var catalogResponse = await administrator.GetAsync("/api/v1/games");
        var catalog = await catalogResponse.Content.ReadFromJsonAsync<JsonElement>();
        var catalogItem = Assert.Single(catalog.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);
        Assert.Equal(gameId, catalogItem.GetProperty("id").GetGuid());
        Assert.Equal(44.93m, catalogItem.GetProperty("currentPrice").GetDecimal());

        using var detailResponse = await administrator.GetAsync($"/api/v1/games/{gameId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(25m, detail.GetProperty("discountPercentage").GetDecimal());

        using var acquisitionResponse = await playerA.PostAsJsonAsync(
            "/api/v1/me/library",
            new { gameId });
        var acquisition = await acquisitionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Created, acquisitionResponse.StatusCode);
        Assert.Equal(44.93m, acquisition.GetProperty("acquisitionPrice").GetDecimal());

        using var playerALibraryResponse = await playerA.GetAsync("/api/v1/me/library");
        var playerALibrary = await playerALibraryResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, playerALibraryResponse.StatusCode);
        Assert.Equal(1, playerALibrary.GetProperty("totalCount").GetInt32());

        using var playerBLibraryResponse = await playerB.GetAsync("/api/v1/me/library");
        var playerBLibrary = await playerBLibraryResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, playerBLibraryResponse.StatusCode);
        Assert.Equal(0, playerBLibrary.GetProperty("totalCount").GetInt32());

        using var usersResponse = await administrator.GetAsync(
            "/api/v1/admin/users?search=player-a%40example.com");
        var users = await usersResponse.Content.ReadFromJsonAsync<JsonElement>();
        var target = Assert.Single(users.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        Assert.Equal(playerAId, target.GetProperty("id").GetGuid());

        using var blockResponse = await administrator.PatchAsJsonAsync(
            $"/api/v1/admin/users/{playerAId}/status",
            new { isActive = false, version = target.GetProperty("version").GetString() });
        var blockedUser = await blockResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, blockResponse.StatusCode);
        Assert.False(blockedUser.GetProperty("isActive").GetBoolean());

        using var rejectedResponse = await playerA.GetAsync("/api/v1/me");
        var rejected = await rejectedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedResponse.StatusCode);
        Assert.Equal("unauthenticated", rejected.GetProperty("code").GetString());
    }

    private async Task SeedAdministratorAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        dbContext.Users.Add(User.RegisterAdministrator(
            "Administrator",
            Email.Create("admin@example.com"),
            passwordHasher.Hash(Password),
            DateTime.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> RegisterAndAuthenticateAsync(
        HttpClient client,
        string name,
        string email)
    {
        using var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name, email, password = Password });
        var registered = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal("/api/v1/me", LocationPath(registerResponse));

        await AuthenticateAsync(client, email);
        return registered.GetProperty("id").GetGuid();
    }

    private static async Task AuthenticateAsync(HttpClient client, string email)
    {
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });
        var login = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            login.GetProperty("accessToken").GetString());
    }

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static string? LocationPath(HttpResponseMessage response) =>
        response.Headers.Location is { IsAbsoluteUri: true } absolute
            ? absolute.AbsolutePath
            : response.Headers.Location?.OriginalString;
}
