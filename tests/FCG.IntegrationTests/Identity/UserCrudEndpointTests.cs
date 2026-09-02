using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Application.Identity;
using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Domain.Library;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Identity;

public sealed class UserCrudEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Password = "Current!1";
    private const string AdminEmail = "admin@example.com";

    [Fact]
    public async Task AuthenticatedUser_CanUpdateOwnNameAndEmail()
    {
        using var client = CreateClient();
        await RegisterAsync(client, "Original Name", "original@example.com", Password);
        await AuthenticateAsync(client, "original@example.com", Password);

        using var response = await client.PatchAsJsonAsync(
            "/api/v1/me",
            new { name = "Updated Name", email = "updated@example.bio" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Updated Name", body.GetProperty("name").GetString());
        Assert.Equal("updated@example.bio", body.GetProperty("email").GetString());

        using var oldLogin = await LoginAsync(client, "original@example.com", Password);
        using var newLogin = await LoginAsync(client, "updated@example.bio", Password);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task UpdatingToAnExistingEmail_ReturnsCanonicalConflict()
    {
        using var client = CreateClient();
        await RegisterAsync(client, "First", "first@example.com", Password);
        await RegisterAsync(client, "Second", "second@example.com", Password);
        await AuthenticateAsync(client, "first@example.com", Password);

        using var response = await client.PatchAsJsonAsync(
            "/api/v1/me",
            new { name = "First", email = "second@example.com" });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("email_already_registered", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthenticatedUser_CanChangeOwnPassword()
    {
        using var client = CreateClient();
        await RegisterAsync(client, "User", "user@example.com", Password);
        await AuthenticateAsync(client, "user@example.com", Password);

        using var response = await client.PatchAsJsonAsync(
            "/api/v1/me/password",
            new { currentPassword = Password, newPassword = "Updated!2" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var oldLogin = await LoginAsync(client, "user@example.com", Password);
        using var newLogin = await LoginAsync(client, "user@example.com", "Updated!2");
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task WrongCurrentPassword_ReturnsCanonicalValidationError()
    {
        using var client = CreateClient();
        await RegisterAsync(client, "User", "user@example.com", Password);
        await AuthenticateAsync(client, "user@example.com", Password);

        using var response = await client.PatchAsJsonAsync(
            "/api/v1/me/password",
            new { currentPassword = "Wrong!1", newPassword = "Updated!2" });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_current_password", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Administrator_CanDeleteAnotherUser()
    {
        using var client = CreateClient();
        await CreateAdministratorAsync();
        var targetId = await RegisterAsync(client, "Target", "target@example.com", Password);
        await AuthenticateAsync(client, AdminEmail, Password);

        using var response = await client.DeleteAsync($"/api/v1/admin/users/{targetId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var login = await LoginAsync(client, "target@example.com", Password);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Administrator_DeletingSelf_ReturnsCanonicalConflict()
    {
        using var client = CreateClient();
        var administratorId = await CreateAdministratorAsync();
        await AuthenticateAsync(client, AdminEmail, Password);

        using var response = await client.DeleteAsync(
            $"/api/v1/admin/users/{administratorId}");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cannot_delete_self", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Administrator_DeletingUserWithLibraryHistory_ReturnsConflict()
    {
        using var client = CreateClient();
        var administratorId = await CreateAdministratorAsync();
        var targetId = await RegisterAsync(client, "Target", "target@example.com", Password);

        await using (var scope = Fixture.Factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
            var game = Game.Create("History", null, 10m, administratorId, DateTime.UtcNow);
            dbContext.Games.Add(game);
            await dbContext.SaveChangesAsync();
            dbContext.LibraryEntries.Add(
                LibraryEntry.Create(targetId, game.Id, DateTime.UtcNow, 10m));
            await dbContext.SaveChangesAsync();
        }

        await AuthenticateAsync(client, AdminEmail, Password);

        using var response = await client.DeleteAsync($"/api/v1/admin/users/{targetId}");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user_has_dependencies", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CommonUser_DeletingAnotherUser_IsForbidden()
    {
        using var client = CreateClient();
        var targetId = await RegisterAsync(client, "Target", "target@example.com", Password);
        await RegisterAsync(client, "Actor", "actor@example.com", Password);
        await AuthenticateAsync(client, "actor@example.com", Password);

        using var response = await client.DeleteAsync($"/api/v1/admin/users/{targetId}");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("forbidden", problem.GetProperty("code").GetString());
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

    private static async Task<Guid> RegisterAsync(
        HttpClient client,
        string name,
        string email,
        string password)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name, email, password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task AuthenticateAsync(
        HttpClient client,
        string email,
        string password)
    {
        using var response = await LoginAsync(client, email, password);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body.GetProperty("accessToken").GetString());
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}
