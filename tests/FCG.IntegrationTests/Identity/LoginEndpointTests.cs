using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Identity;

public sealed class LoginEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string LoginEndpoint = "/api/v1/auth/login";
    private const string Password = "Str0ng!Pass";
    private const string Email = "user@example.com";

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAValidTokenWithoutPiiOrRefreshToken()
    {
        using var client = CreateClient();
        await RegisterAsync(client);
        Fixture.Logs.Clear();
        var startedAtUtc = DateTime.UtcNow;

        using var response = await client.PostAsJsonAsync(
            LoginEndpoint,
            new { email = Email, password = Password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        Assert.Equal(3600, body.GetProperty("expiresIn").GetInt32());
        Assert.False(body.TryGetProperty("refreshToken", out _));

        var accessToken = body.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        Assert.Equal("HS256", jwt.Header.Alg);
        Assert.Equal("fcg-api", jwt.Issuer);
        Assert.Contains("fcg-clients", jwt.Audiences);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.Single(claim => claim.Type == "sub").Value));
        Assert.Equal("User", jwt.Claims.Single(claim => claim.Type == "role").Value);
        Assert.True(Guid.TryParse(jwt.Claims.Single(claim => claim.Type == "jti").Value, out _));
        Assert.InRange(jwt.ValidTo, startedAtUtc.AddMinutes(59), startedAtUtc.AddMinutes(61));
        Assert.DoesNotContain("email", jwt.Payload.Keys);
        Assert.DoesNotContain("name", jwt.Payload.Keys);
        Assert.DoesNotContain("password", jwt.Payload.Keys);
        Assert.DoesNotContain("passwordHash", jwt.Payload.Keys);

        using var protectedRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/_test/documentation/protected");
        protectedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var protectedResponse = await client.SendAsync(protectedRequest);
        Assert.Equal(HttpStatusCode.NoContent, protectedResponse.StatusCode);

        var loggedText = string.Join('\n', Fixture.Logs.AllText());
        Assert.DoesNotContain(accessToken!, loggedText, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, loggedText, StringComparison.Ordinal);
        Assert.DoesNotContain(Email, loggedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithUnknownWrongOrInactiveAccount_ReturnsTheSameGenericFailure()
    {
        using var client = CreateClient();
        await RegisterAsync(client);

        using var wrongPasswordResponse = await client.PostAsJsonAsync(
            LoginEndpoint,
            new { email = Email, password = "Wr0ng!Pass" });
        using var unknownAccountResponse = await client.PostAsJsonAsync(
            LoginEndpoint,
            new { email = "missing@example.com", password = Password });

        await DeactivateUserAsync();

        using var inactiveAccountResponse = await client.PostAsJsonAsync(
            LoginEndpoint,
            new { email = Email, password = Password });

        var wrongProblem = await ReadProblemAsync(wrongPasswordResponse);
        var unknownProblem = await ReadProblemAsync(unknownAccountResponse);
        var inactiveProblem = await ReadProblemAsync(inactiveAccountResponse);

        AssertGenericInvalidCredentials(wrongPasswordResponse, wrongProblem);
        AssertGenericInvalidCredentials(unknownAccountResponse, unknownProblem);
        AssertGenericInvalidCredentials(inactiveAccountResponse, inactiveProblem);
        Assert.Equal(
            wrongProblem.GetProperty("title").GetString(),
            unknownProblem.GetProperty("title").GetString());
        Assert.Equal(
            wrongProblem.GetProperty("title").GetString(),
            inactiveProblem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedToken_ReturnsCanonicalUnauthenticatedProblem()
    {
        using var client = CreateClient();
        await RegisterAsync(client);
        using var loginResponse = await client.PostAsJsonAsync(
            LoginEndpoint,
            new { email = Email, password = Password });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("accessToken").GetString()!;
        var tokenParts = token.Split('.');
        tokenParts[2] = (tokenParts[2][0] == 'a' ? 'b' : 'a') + tokenParts[2][1..];
        var tamperedToken = string.Join('.', tokenParts);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_test/documentation/protected");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        using var response = await client.SendAsync(request);
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", problem.GetProperty("code").GetString());
    }

    private async Task DeactivateUserAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var user = await dbContext.Users.SingleAsync();
        dbContext.Entry(user).Property(current => current.IsActive).CurrentValue = false;
        await dbContext.SaveChangesAsync();
    }

    private static async Task RegisterAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name = "User", email = Email, password = Password });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body;
    }

    private static void AssertGenericInvalidCredentials(
        HttpResponseMessage response,
        JsonElement problem)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("invalid_credentials", problem.GetProperty("code").GetString());
        Assert.Equal("Invalid credentials", problem.GetProperty("title").GetString());
        Assert.False(problem.TryGetProperty("detail", out _));
        Assert.False(problem.TryGetProperty("accessToken", out _));
    }

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
}
