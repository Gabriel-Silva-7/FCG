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

namespace FCG.IntegrationTests.Identity;

public sealed class AdminUsersEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Endpoint = "/api/v1/admin/users";
    private const string Password = "Str0ng!Pass";
    private const string AdminEmail = "admin@example.com";

    [Fact]
    public async Task Administrator_ReceivesAPagedListWithoutSecrets()
    {
        using var client = CreateClient();
        await CreateAdministratorAsync();
        await RegisterAsync(client, "Common User", "common@example.com");
        await AuthenticateAsAdministratorAsync(client);

        using var response = await client.GetAsync(Endpoint);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());

        var items = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);

        foreach (var item in items)
        {
            Assert.False(item.TryGetProperty("passwordHash", out _));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("version").GetString()));
            Assert.True(item.GetProperty("isActive").GetBoolean());
        }
    }

    [Fact]
    public async Task CommonUser_IsForbidden()
    {
        using var client = CreateClient();
        await RegisterAsync(client, "Common User", "common@example.com");
        await AuthenticateAsync(client, "common@example.com");

        using var response = await client.GetAsync(Endpoint);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("forbidden", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnonymousRequest_IsUnauthenticatedRatherThanForbidden()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(Endpoint);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PageSizeAboveTheMaximum_IsRejected()
    {
        using var client = CreateClient();
        await CreateAdministratorAsync();
        await AuthenticateAsAdministratorAsync(client);

        using var response = await client.GetAsync($"{Endpoint}?pageSize=101");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Search_MatchesPartialNameOrEmailAndKeepsTheTotalConsistent()
    {
        using var client = CreateClient();
        await CreateAdministratorAsync();
        await RegisterAsync(client, "Gabriel Silva", "gabriel@example.com");
        await RegisterAsync(client, "Gabriela Souza", "gabriela@example.com");
        await RegisterAsync(client, "Sem Relacao", "sem.relacao@example.com");
        await AuthenticateAsAdministratorAsync(client);

        using var byName = await client.GetAsync($"{Endpoint}?search=gabriel");
        using var byEmail = await client.GetAsync($"{Endpoint}?search=RELACAO@EXAMPLE");

        Assert.Equal(HttpStatusCode.OK, byName.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byEmail.StatusCode);
        var byNameBody = await byName.Content.ReadFromJsonAsync<JsonElement>();
        var byEmailBody = await byEmail.Content.ReadFromJsonAsync<JsonElement>();

        // O total reflete o filtro, não o tamanho da página — senão a paginação mentiria sobre
        // quantos resultados existem.
        Assert.Equal(2, byNameBody.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, byNameBody.GetProperty("items").GetArrayLength());

        // A busca por e-mail também aceita trechos e ignora diferenças entre maiúsculas e minúsculas.
        var found = Assert.Single(byEmailBody.GetProperty("items").EnumerateArray().ToArray());
        Assert.Equal("sem.relacao@example.com", found.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Pagination_SplitsResultsWithoutRepeatingOrLosingAnyone()
    {
        using var client = CreateClient();
        await CreateAdministratorAsync();

        for (var index = 1; index <= 4; index++)
        {
            await RegisterAsync(client, $"User {index}", $"user{index}@example.com");
        }

        await AuthenticateAsAdministratorAsync(client);

        using var firstPage = await client.GetAsync($"{Endpoint}?page=1&pageSize=2");
        using var secondPage = await client.GetAsync($"{Endpoint}?page=2&pageSize=2");
        var firstBody = await firstPage.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await secondPage.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(5, firstBody.GetProperty("totalCount").GetInt32());
        Assert.Equal(5, secondBody.GetProperty("totalCount").GetInt32());

        var firstIds = IdsOf(firstBody);
        var secondIds = IdsOf(secondBody);

        Assert.Equal(2, firstIds.Length);
        Assert.Equal(2, secondIds.Length);
        Assert.Empty(firstIds.Intersect(secondIds));
    }

    private static Guid[] IdsOf(JsonElement body) =>
        body.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();

    private async Task CreateAdministratorAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        dbContext.Users.Add(User.RegisterAdministrator(
            "Administrator",
            Email.Create(AdminEmail),
            passwordHasher.Hash(Password),
            DateTime.UtcNow));

        await dbContext.SaveChangesAsync();
    }

    private static Task AuthenticateAsAdministratorAsync(HttpClient client) =>
        AuthenticateAsync(client, AdminEmail);

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

    private static async Task RegisterAsync(HttpClient client, string name, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name, email, password = Password });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
}
