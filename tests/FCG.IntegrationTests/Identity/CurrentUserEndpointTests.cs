using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using FCG.Application.Identity;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Identity;

public sealed class CurrentUserEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Password = "Str0ng!Pass";

    [Fact]
    public async Task Me_UsesSubAndIgnoresAnExternalUserId()
    {
        using var client = CreateClient();
        var firstUser = await RegisterAsync(client, "First", "first@example.com");
        var secondUser = await RegisterAsync(client, "Second", "second@example.com");
        var token = await LoginAsync(client, "first@example.com");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/me?userId={secondUser}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(firstUser, body.GetProperty("id").GetGuid());
        Assert.Equal("First", body.GetProperty("name").GetString());
        Assert.Equal("first@example.com", body.GetProperty("email").GetString());
        Assert.Equal("User", body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("passwordHash", out _));
        Assert.False(body.TryGetProperty("isActive", out _));
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsCanonicalUnauthenticatedProblem()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/v1/me");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UserRole_CanUseCommonPolicyButReceivesForbiddenFromAdminPolicy()
    {
        using var client = CreateClient();
        await RegisterAsync(client, "User", "user@example.com");
        var token = await LoginAsync(client, "user@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var commonResponse = await client.GetAsync("/_test/authorization/user-capability");
        using var adminResponse = await client.GetAsync("/_test/authorization/admin-capability");
        var forbidden = await adminResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NoContent, commonResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminResponse.StatusCode);
        Assert.Equal("forbidden", forbidden.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AdministratorRole_SatisfiesBothPolicies()
    {
        var authorizationService = Fixture.Factory.Services
            .GetRequiredService<IAuthorizationService>();
        var identity = new ClaimsIdentity(
            [new Claim("role", "Administrator")],
            authenticationType: "Test",
            nameType: "sub",
            roleType: "role");
        var administrator = new ClaimsPrincipal(identity);

        var commonResult = await authorizationService.AuthorizeAsync(
            administrator,
            resource: null,
            IdentityPolicies.UserOrAdmin);
        var adminResult = await authorizationService.AuthorizeAsync(
            administrator,
            resource: null,
            IdentityPolicies.AdminOnly);

        Assert.True(commonResult.Succeeded);
        Assert.True(adminResult.Succeeded);
    }

    private static async Task<Guid> RegisterAsync(
        HttpClient client,
        string name,
        string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name, email, password = Password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password = Password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body.GetProperty("accessToken").GetString()!;
    }

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
}
