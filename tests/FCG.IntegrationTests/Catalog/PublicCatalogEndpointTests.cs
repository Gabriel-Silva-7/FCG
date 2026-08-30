using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.Catalog;
using FCG.Application.Identity;
using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Catalog;

public sealed class PublicCatalogEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Endpoint = "/api/v1/games";
    private const string Password = "Str0ng!Pass";
    private const string AdminEmail = "admin@example.com";

    [Fact]
    public async Task List_IsPublicPagedAndExcludesInactiveGames()
    {
        var games = await SeedGamesAsync(
            new GameSeed("Charlie", 30m, DateTime.UnixEpoch.AddDays(3)),
            new GameSeed("Alpha", 10m, DateTime.UnixEpoch.AddDays(1)),
            new GameSeed("Bravo", 20m, DateTime.UnixEpoch.AddDays(2)),
            new GameSeed("Hidden", 1m, DateTime.UnixEpoch.AddDays(4), IsActive: false));
        using var client = CreateClient();

        using var firstResponse = await client.GetAsync($"{Endpoint}?page=1&pageSize=2");
        using var secondResponse = await client.GetAsync($"{Endpoint}?page=2&pageSize=2");
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var second = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(3, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, second.GetProperty("totalCount").GetInt32());
        Assert.Equal(["Alpha", "Bravo"], TitlesOf(first));
        Assert.Equal(["Charlie"], TitlesOf(second));
        Assert.DoesNotContain(games.Single(game => !game.IsActive).Id, IdsOf(first).Concat(IdsOf(second)));

        foreach (var item in first.GetProperty("items").EnumerateArray())
        {
            Assert.Equal(
                item.GetProperty("basePrice").GetDecimal(),
                item.GetProperty("currentPrice").GetDecimal());
            Assert.Equal(0m, item.GetProperty("discountPercentage").GetDecimal());
            Assert.True(item.GetProperty("isActive").GetBoolean());
            Assert.False(item.TryGetProperty("createdByUserId", out _));
        }
    }

    [Fact]
    public async Task Search_IsCaseInsensitiveAndTreatsSqlWildcardsAsLiteralText()
    {
        await SeedGamesAsync(
            new GameSeed("100% Fun", 10m, DateTime.UnixEpoch),
            new GameSeed("1000 Fun", 20m, DateTime.UnixEpoch.AddDays(1)),
            new GameSeed("Under_score", 30m, DateTime.UnixEpoch.AddDays(2)),
            new GameSeed("UnderXscore", 40m, DateTime.UnixEpoch.AddDays(3)),
            new GameSeed(@"Back\slash", 50m, DateTime.UnixEpoch.AddDays(4)),
            new GameSeed("BackXslash", 60m, DateTime.UnixEpoch.AddDays(5)),
            new GameSeed("Celeste", 70m, DateTime.UnixEpoch.AddDays(6)));
        using var client = CreateClient();

        using var percentResponse = await client.GetAsync(
            $"{Endpoint}?search={Uri.EscapeDataString("100%")}");
        using var underscoreResponse = await client.GetAsync(
            $"{Endpoint}?search={Uri.EscapeDataString("Under_")}");
        using var backslashResponse = await client.GetAsync(
            $"{Endpoint}?search={Uri.EscapeDataString(@"Back\")}");
        using var caseResponse = await client.GetAsync($"{Endpoint}?search=CELESTE");
        var percent = await percentResponse.Content.ReadFromJsonAsync<JsonElement>();
        var underscore = await underscoreResponse.Content.ReadFromJsonAsync<JsonElement>();
        var backslash = await backslashResponse.Content.ReadFromJsonAsync<JsonElement>();
        var insensitive = await caseResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, percentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, underscoreResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, backslashResponse.StatusCode);
        Assert.Equal(["100% Fun"], TitlesOf(percent));
        Assert.Equal(["Under_score"], TitlesOf(underscore));
        Assert.Equal([@"Back\slash"], TitlesOf(backslash));
        Assert.Equal(["Celeste"], TitlesOf(insensitive));
    }

    [Fact]
    public async Task SortBy_OrdersOnlyByTheAllowListedFields()
    {
        await SeedGamesAsync(
            new GameSeed("Third", 20m, DateTime.UnixEpoch.AddDays(3)),
            new GameSeed("First", 30m, DateTime.UnixEpoch.AddDays(1)),
            new GameSeed("Second", 10m, DateTime.UnixEpoch.AddDays(2)));
        using var client = CreateClient();

        using var byPriceResponse = await client.GetAsync(
            $"{Endpoint}?sortBy={GameSortFields.BasePrice}");
        using var byCreationResponse = await client.GetAsync(
            $"{Endpoint}?sortBy={GameSortFields.CreatedAt}");
        var byPrice = await byPriceResponse.Content.ReadFromJsonAsync<JsonElement>();
        var byCreation = await byCreationResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(["Second", "Third", "First"], TitlesOf(byPrice));
        Assert.Equal(["First", "Second", "Third"], TitlesOf(byCreation));
    }

    [Fact]
    public async Task InvalidListInputs_ReturnCanonicalValidationProblems()
    {
        using var client = CreateClient();
        var invalidQueries = new[]
        {
            "page=0",
            "pageSize=101",
            "sortBy=description",
            $"search={new string('a', Game.MaxTitleLength + 1)}",
        };

        foreach (var query in invalidQueries)
        {
            using var response = await client.GetAsync($"{Endpoint}?{query}");
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("validation_error", problem.GetProperty("code").GetString());
            Assert.Equal(400, problem.GetProperty("status").GetInt32());
            Assert.Equal(Endpoint, problem.GetProperty("instance").GetString());
        }
    }

    [Fact]
    public async Task Detail_ReturnsAnActiveGameAndHidesInactiveOrUnknownGames()
    {
        var games = await SeedGamesAsync(
            new GameSeed("Visible", 20m, DateTime.UnixEpoch),
            new GameSeed("Hidden", 10m, DateTime.UnixEpoch.AddDays(1), IsActive: false));
        var visible = games.Single(game => game.IsActive);
        var hidden = games.Single(game => !game.IsActive);
        using var client = CreateClient();

        using var visibleResponse = await client.GetAsync($"{Endpoint}/{visible.Id}");
        using var hiddenResponse = await client.GetAsync($"{Endpoint}/{hidden.Id}");
        using var missingResponse = await client.GetAsync($"{Endpoint}/{Guid.NewGuid()}");
        var body = await visibleResponse.Content.ReadFromJsonAsync<JsonElement>();
        var hiddenProblem = await hiddenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var missingProblem = await missingResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, visibleResponse.StatusCode);
        Assert.Equal(visible.Id, body.GetProperty("id").GetGuid());
        Assert.Equal("Visible", body.GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.NotFound, hiddenResponse.StatusCode);
        Assert.Equal("resource_not_found", hiddenProblem.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        Assert.Equal("resource_not_found", missingProblem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreationLocation_CanBeFollowedWithoutAuthentication()
    {
        await SeedGamesAsync();
        using var client = CreateClient();
        await AuthenticateAsAdministratorAsync(client);

        using var creationResponse = await client.PostAsJsonAsync(
            Endpoint,
            new { title = "Celeste", basePrice = 59.90m });
        var created = await creationResponse.Content.ReadFromJsonAsync<JsonElement>();
        var location = Assert.IsType<Uri>(creationResponse.Headers.Location);
        client.DefaultRequestHeaders.Authorization = null;

        using var detailResponse = await client.GetAsync(location);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, creationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(created.GetProperty("id").GetGuid(), detail.GetProperty("id").GetGuid());
        Assert.Equal(created.GetProperty("title").GetString(), detail.GetProperty("title").GetString());
    }

    [Fact]
    public async Task List_UsesTwoQueriesAndProjectsOnlyPublicGameColumns()
    {
        await SeedGamesAsync(new GameSeed("Celeste", 59.90m, DateTime.UnixEpoch));
        using var client = CreateClient();
        Fixture.Logs.Clear();

        using var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var commands = Fixture.Logs.Entries.Where(entry =>
            entry.Category == "Microsoft.EntityFrameworkCore.Database.Command" &&
            entry.Message.StartsWith("Executed DbCommand", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, commands.Length);

        var sql = string.Join('\n', commands.Select(command => command.Message));
        Assert.DoesNotContain("CreatedByUserId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(GameSortFields.Title, "ORDER BY g.\"Title\", g.\"Id\"")]
    [InlineData(GameSortFields.BasePrice, "ORDER BY g.\"BasePrice\", g.\"Title\", g.\"Id\"")]
    [InlineData(GameSortFields.CreatedAt, "ORDER BY g.\"CreatedAtUtc\", g.\"Id\"")]
    public async Task DuplicateSortValues_UseStablePaginationTieBreakers(
        string sortBy,
        string expectedOrderBy)
    {
        var games = await SeedGamesAsync(
            new GameSeed("Same title", 10m, DateTime.UnixEpoch),
            new GameSeed("Same title", 10m, DateTime.UnixEpoch));
        using var client = CreateClient();
        Fixture.Logs.Clear();

        using var firstResponse = await client.GetAsync(
            $"{Endpoint}?page=1&pageSize=1&sortBy={sortBy}");
        using var secondResponse = await client.GetAsync(
            $"{Endpoint}?page=2&pageSize=1&sortBy={sortBy}");
        var first = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var second = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        var pagedIds = IdsOf(first).Concat(IdsOf(second)).ToArray();
        Assert.Equal(2, pagedIds.Distinct().Count());
        Assert.Equal(games.Select(game => game.Id).Order(), pagedIds.Order());

        var itemQueries = Fixture.Logs.Entries.Where(entry =>
            entry.Category == "Microsoft.EntityFrameworkCore.Database.Command" &&
            entry.Message.Contains("ORDER BY", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, itemQueries.Length);
        Assert.All(itemQueries, query =>
            Assert.Contains(
                expectedOrderBy,
                query.Message,
                StringComparison.Ordinal));
    }

    private async Task<IReadOnlyList<Game>> SeedGamesAsync(params GameSeed[] seeds)
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

        var games = seeds
            .Select(seed =>
            {
                var game = Game.Create(
                    seed.Title,
                    null,
                    seed.BasePrice,
                    administrator.Id,
                    seed.CreatedAtUtc);

                if (!seed.IsActive)
                {
                    game.Deactivate();
                }

                return game;
            })
            .ToArray();
        dbContext.Games.AddRange(games);
        await dbContext.SaveChangesAsync();

        return games;
    }

    private static string[] TitlesOf(JsonElement body) =>
        body.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("title").GetString()!)
            .ToArray();

    private static Guid[] IdsOf(JsonElement body) =>
        body.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();

    private static async Task AuthenticateAsAdministratorAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = AdminEmail, password = Password });
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

    private sealed record GameSeed(
        string Title,
        decimal BasePrice,
        DateTime CreatedAtUtc,
        bool IsActive = true);
}
