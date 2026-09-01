using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.Identity;
using FCG.Application.Library;
using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Domain.Library;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Library;

public sealed class LibraryConcurrencyTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Endpoint = "/api/v1/me/library";
    private const string Password = "Str0ng!Pass";
    private const string UserEmail = "player@example.com";

    [Fact]
    public async Task SecondAcquisitionOfTheSameGame_ReturnsCanonicalConflict()
    {
        using var client = CreateClient();
        var gameId = await SeedGameAsync();
        await RegisterAndAuthenticateAsync(client);

        using var first = await client.PostAsJsonAsync(Endpoint, new { gameId });
        using var second = await client.PostAsJsonAsync(Endpoint, new { gameId });
        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("game_already_acquired", problem.GetProperty("code").GetString());
        Assert.Equal(
            "urn:fcg:error:game-already-acquired",
            problem.GetProperty("type").GetString());
        Assert.Equal(1, await CountEntriesAsync());
    }

    // Duas requisições simultâneas atravessam o pre-check juntas; quem decide é a PK composta.
    [Fact]
    public async Task ConcurrentAcquisitions_PersistASingleRowAndConflictOnce()
    {
        using var client = CreateClient();
        var gameId = await SeedGameAsync();
        await RegisterAndAuthenticateAsync(client);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(_ => client.PostAsJsonAsync(Endpoint, new { gameId })));

        try
        {
            Assert.Equal(
                [HttpStatusCode.Created, HttpStatusCode.Conflict],
                responses.Select(response => response.StatusCode).Order());

            var conflict = Assert.Single(
                responses,
                response => response.StatusCode is HttpStatusCode.Conflict);
            var problem = await conflict.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("game_already_acquired", problem.GetProperty("code").GetString());
            Assert.Equal(1, await CountEntriesAsync());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    // Força a corrida que o Task.WhenAll não garante: dois escopos confirmam o pre-check negativo
    // ANTES de qualquer escrita, e só então gravam. Sem a tradução do 23505 isso vira 500.
    [Fact]
    public async Task TwoWritersPastThePreCheck_AreResolvedByThePrimaryKey()
    {
        var gameId = await SeedGameAsync();
        var userId = await SeedUserAsync();
        await using var firstScope = Fixture.Factory.Services.CreateAsyncScope();
        await using var secondScope = Fixture.Factory.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ILibraryRepository>();
        var second = secondScope.ServiceProvider.GetRequiredService<ILibraryRepository>();

        Assert.False(await first.ExistsAsync(userId, gameId, CancellationToken.None));
        Assert.False(await second.ExistsAsync(userId, gameId, CancellationToken.None));

        await first.AddAsync(
            LibraryEntry.Create(userId, gameId, DateTime.UnixEpoch, 10m),
            CancellationToken.None);

        await Assert.ThrowsAsync<GameAlreadyAcquiredException>(() =>
            second.AddAsync(
                LibraryEntry.Create(userId, gameId, DateTime.UnixEpoch, 10m),
                CancellationToken.None));

        Assert.Equal(1, await CountEntriesAsync());
    }

    // Uma violação de constraint diferente não pode ser mascarada como aquisição duplicada.
    [Fact]
    public async Task ForeignKeyViolation_IsNotMaskedAsADuplicateAcquisition()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILibraryRepository>();
        var orphan = LibraryEntry.Create(Guid.NewGuid(), Guid.NewGuid(), DateTime.UnixEpoch, 10m);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repository.AddAsync(orphan, CancellationToken.None));
    }

    private async Task<int> CountEntriesAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        return await dbContext.LibraryEntries.CountAsync();
    }

    private async Task<Guid> SeedUserAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = User.Register(
            "Player",
            Email.Create(UserEmail),
            passwordHasher.Hash(Password),
            DateTime.UtcNow);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user.Id;
    }

    private async Task<Guid> SeedGameAsync()
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

        dbContext.Users.Add(administrator);
        dbContext.Games.Add(game);
        await dbContext.SaveChangesAsync();

        return game.Id;
    }

    private static async Task RegisterAndAuthenticateAsync(HttpClient client)
    {
        using var register = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name = "Player", email = UserEmail, password = Password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        using var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = UserEmail, password = Password });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body.GetProperty("accessToken").GetString());
    }

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}
