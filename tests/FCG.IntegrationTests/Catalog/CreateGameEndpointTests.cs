using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Api.Contracts;
using FCG.Api.Controllers;
using FCG.Application.Catalog;
using FCG.Application.Identity;
using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.IntegrationTests.Catalog;

public sealed class CreateGameEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Endpoint = "/api/v1/games";
    private const string Password = "Str0ng!Pass";
    private const string AdminEmail = "admin@example.com";

    [Fact]
    public async Task Administrator_WithValidInput_CreatesGameOwnedByTheTokenSubject()
    {
        using var client = CreateClient();
        var administratorId = await CreateAdministratorAsync();
        await AuthenticateAsync(client, AdminEmail);
        Fixture.Logs.Clear();
        var startedAtUtc = DateTime.UtcNow;

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new
            {
                title = "  Celeste  ",
                description = "  Precision platformer  ",
                basePrice = 59.90m,
                createdByUserId = Guid.NewGuid(),
                isActive = false,
            });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var gameId = body.GetProperty("id").GetGuid();
        Assert.Equal($"/api/v1/games/{gameId}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("Celeste", body.GetProperty("title").GetString());
        Assert.Equal("Precision platformer", body.GetProperty("description").GetString());
        Assert.Equal(59.90m, body.GetProperty("basePrice").GetDecimal());
        Assert.Equal(59.90m, body.GetProperty("currentPrice").GetDecimal());
        Assert.Equal(0m, body.GetProperty("discountPercentage").GetDecimal());
        Assert.True(body.GetProperty("isActive").GetBoolean());
        Assert.False(body.TryGetProperty("createdByUserId", out _));

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var persisted = await dbContext.Games.AsNoTracking().SingleAsync();

        Assert.Equal(gameId, persisted.Id);
        Assert.Equal(administratorId, persisted.CreatedByUserId);
        Assert.True(persisted.CreatedAtUtc >= startedAtUtc);
        Assert.True(persisted.CreatedAtUtc <= DateTime.UtcNow);
        Assert.True(persisted.IsActive);

        var eventEntry = Assert.Single(Fixture.Logs.Entries.Where(entry =>
            entry.Category == typeof(GamesController).FullName &&
            entry.Message.StartsWith("GameCreated", StringComparison.Ordinal)));
        var requestEntry = Assert.Single(Fixture.Logs.Entries.Where(entry =>
            entry.Message.StartsWith("HttpRequest", StringComparison.Ordinal)));

        Assert.Equal(LogLevel.Information, eventEntry.Level);
        Assert.Equal(
            "GameCreated {ActorUserId} {TargetGameId} {TraceId}",
            eventEntry.Field("{OriginalFormat}"));
        Assert.Equal(administratorId, eventEntry.Field("ActorUserId"));
        Assert.Equal(gameId, eventEntry.Field("TargetGameId"));
        Assert.Equal(requestEntry.Field("TraceId"), eventEntry.Field("TraceId"));
    }

    [Fact]
    public async Task MaximumSupportedBasePrice_CanBePersisted()
    {
        using var client = CreateClient();
        await CreateAdministratorAsync();
        await AuthenticateAsync(client, AdminEmail);

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new
            {
                title = "Maximum price",
                basePrice = GamePriceLimits.MaximumSupportedBasePrice,
            });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            GamePriceLimits.MaximumSupportedBasePrice,
            body.GetProperty("basePrice").GetDecimal());

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        Assert.Equal(
            GamePriceLimits.MaximumSupportedBasePrice,
            await dbContext.Games.Select(game => game.BasePrice).SingleAsync());
    }

    [Fact]
    public async Task CommonUser_IsForbiddenWithoutCreatingAGame()
    {
        using var client = CreateClient();
        await RegisterAsync(client, "Common User", "common@example.com");
        await AuthenticateAsync(client, "common@example.com");

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new { title = "Celeste", basePrice = 59.90m });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("forbidden", problem.GetProperty("code").GetString());
        Assert.False(await AnyGameAsync());
    }

    [Fact]
    public async Task AnonymousRequest_IsUnauthenticatedWithoutCreatingAGame()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new { title = "Celeste", basePrice = 59.90m });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", problem.GetProperty("code").GetString());
        Assert.False(await AnyGameAsync());
    }

    [Fact]
    public async Task InvalidInputs_ReturnCanonicalValidationProblemsWithoutCreatingGames()
    {
        using var client = CreateClient();
        await CreateAdministratorAsync();
        await AuthenticateAsync(client, AdminEmail);
        var invalidRequests = new object[]
        {
            new { title = "   ", basePrice = 59.90m },
            new { title = "Celeste", basePrice = -0.01m },
            new { title = "Celeste", basePrice = 59.901m },
            new { title = "Celeste", basePrice = 10_000_000_000_000_000m },
            new { title = "Celeste" },
        };

        foreach (var invalidRequest in invalidRequests)
        {
            using var response = await client.PostAsJsonAsync(Endpoint, invalidRequest);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("validation_error", problem.GetProperty("code").GetString());
            Assert.Equal(400, problem.GetProperty("status").GetInt32());
            Assert.Equal(Endpoint, problem.GetProperty("instance").GetString());
            Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        }

        Assert.False(await AnyGameAsync());
    }

    [Fact]
    public async Task BasePriceRangeError_UsesAnInvariantPublicMessage()
    {
        using var client = CreateClient();
        await CreateAdministratorAsync();
        await AuthenticateAsync(client, AdminEmail);

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new { title = "Celeste", basePrice = -1m });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var message = problem
            .GetProperty("errors")
            .GetProperty("BasePrice")[0]
            .GetString();
        Assert.Equal(
            "Base price must be between 0 and 9999999999999999.99.",
            message);
    }

    private async Task<Guid> CreateAdministratorAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var administrator = User.RegisterAdministrator(
            "Administrator",
            Email.Create(AdminEmail),
            passwordHasher.Hash(Password),
            DateTime.UtcNow);

        dbContext.Users.Add(administrator);
        await dbContext.SaveChangesAsync();

        return administrator.Id;
    }

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

    private async Task<bool> AnyGameAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        return await dbContext.Games.AnyAsync();
    }

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
}
