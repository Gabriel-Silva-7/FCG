using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FCG.Infrastructure.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FCG.IntegrationTests.Identity;

public sealed class ActiveUserRevalidationTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string ProtectedEndpoint = "/_test/authorization/user-capability";
    private const string Email = "blocked@example.com";
    private const string Password = "Str0ng!Pass";

    [Fact]
    public async Task DeactivatedAccount_LosesAccessImmediatelyWithTheTokenItAlreadyHolds()
    {
        using var client = CreateClient();
        await RegisterAsync(client);
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var beforeBlock = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, beforeBlock.StatusCode);

        await DeactivateOnlyUserAsync();

        using var afterBlock = await client.GetAsync("/api/v1/me");
        var problem = await afterBlock.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, afterBlock.StatusCode);
        Assert.Equal("unauthenticated", problem.GetProperty("code").GetString());
    }

    // Contra /_test/authorization/user-capability de propósito: o MeController já devolve 401
    // sozinho quando o usuário não existe, então usá-lo aqui daria um teste que passa sem o
    // mecanismo. Esse endpoint só exige autenticação e nunca consulta o banco.
    [Fact]
    public async Task DeletedAccount_CannotUseATokenThatWasValidWhenItWasIssued()
    {
        using var client = CreateClient();
        await RegisterAsync(client);
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var beforeDeletion = await client.GetAsync(ProtectedEndpoint);
        Assert.Equal(HttpStatusCode.NoContent, beforeDeletion.StatusCode);

        await DeleteOnlyUserAsync();

        using var afterDeletion = await client.GetAsync(ProtectedEndpoint);
        var problem = await afterDeletion.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, afterDeletion.StatusCode);
        Assert.Equal("unauthenticated", problem.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task TokenWithoutAUsableSubject_IsRejected(string? subject)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ForgeSignedToken(subject));

        using var response = await client.GetAsync(ProtectedEndpoint);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", problem.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData(InvalidTokenScenario.Expired)]
    [InlineData(InvalidTokenScenario.MissingExpiration)]
    [InlineData(InvalidTokenScenario.WrongIssuer)]
    [InlineData(InvalidTokenScenario.WrongAudience)]
    public async Task TokenOutsideTheJwtContract_IsRejected(InvalidTokenScenario scenario)
    {
        using var client = CreateClient();
        var userId = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            ForgeSignedToken(userId.ToString(), scenario));

        using var response = await client.GetAsync(ProtectedEndpoint);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthenticated", problem.GetProperty("code").GetString());
    }

    // O corpo já é coberto pelo contrato da HTTP-02; o risco real é o WWW-Authenticate, onde o
    // JwtBearer pode publicar um error_description e ninguém olha.
    [Fact]
    public async Task RejectedTokenResponse_RevealsNoInternalReason()
    {
        using var client = CreateClient();
        await RegisterAsync(client);
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await DeactivateOnlyUserAsync();

        using var response = await client.GetAsync(ProtectedEndpoint);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var challenge = string.Join(
            " ",
            response.Headers.WwwAuthenticate.Select(header => header.ToString()));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(problem.TryGetProperty("detail", out _));
        Assert.DoesNotContain("error_description", challenge, StringComparison.OrdinalIgnoreCase);

        foreach (var internalWord in new[] { "inactive", "inativ", "IsActive", "Invalid token." })
        {
            Assert.DoesNotContain(internalWord, challenge, StringComparison.OrdinalIgnoreCase);
        }
    }

    private string ForgeSignedToken(
        string? subject,
        InvalidTokenScenario? scenario = null)
    {
        var jwt = Fixture.Factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var claims = subject is null
            ? []
            : new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim("role", "User"),
            };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            scenario is InvalidTokenScenario.WrongIssuer ? "wrong-issuer" : jwt.Issuer,
            scenario is InvalidTokenScenario.WrongAudience ? "wrong-audience" : jwt.Audience,
            claims,
            expires: scenario switch
            {
                InvalidTokenScenario.Expired => DateTime.UtcNow.AddMinutes(-1),
                InvalidTokenScenario.MissingExpiration => null,
                _ => DateTime.UtcNow.AddMinutes(10),
            },
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task DeleteOnlyUserAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        dbContext.Users.Remove(await dbContext.Users.SingleAsync());
        await dbContext.SaveChangesAsync();
    }

    private async Task DeactivateOnlyUserAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var user = await dbContext.Users.SingleAsync();
        dbContext.Entry(user).Property(current => current.IsActive).CurrentValue = false;
        await dbContext.SaveChangesAsync();
    }

    private static async Task<Guid> RegisterAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name = "Blocked User", email = Email, password = Password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return body.GetProperty("id").GetGuid();
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = Email, password = Password });
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

    public enum InvalidTokenScenario
    {
        Expired,
        MissingExpiration,
        WrongIssuer,
        WrongAudience,
    }
}
