using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FCG.IntegrationTests.Security;

public sealed class RateLimitingTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const int PermitLimit = 3;

    [Fact]
    public async Task LoginBeyondTheConfiguredLimit_IsRejectedWithCanonicalTooManyRequests()
    {
        using var factory = CreateRateLimitedFactory();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= PermitLimit; attempt++)
        {
            using var allowed = await PostLoginAsync(client);

            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        using var rejected = await PostLoginAsync(client);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("rate_limit_exceeded", problem.GetProperty("code").GetString());
        Assert.Equal("urn:fcg:error:rate-limit-exceeded", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task RegisterBeyondTheConfiguredLimit_IsRejectedWithTheSameContract()
    {
        using var factory = CreateRateLimitedFactory();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= PermitLimit; attempt++)
        {
            using var allowed = await PostRegisterAsync(client, attempt);

            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        using var rejected = await PostRegisterAsync(client, PermitLimit + 1);
        var problem = await rejected.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("rate_limit_exceeded", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task LoginAndRegister_HaveIndependentLimitsForTheSameIp()
    {
        using var factory = CreateRateLimitedFactory();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= PermitLimit; attempt++)
        {
            using var login = await PostLoginAsync(client);

            Assert.NotEqual(HttpStatusCode.TooManyRequests, login.StatusCode);
        }

        for (var attempt = 1; attempt <= PermitLimit; attempt++)
        {
            using var register = await PostRegisterAsync(client, attempt);

            Assert.NotEqual(HttpStatusCode.TooManyRequests, register.StatusCode);
        }

        using var rejectedLogin = await PostLoginAsync(client);
        using var rejectedRegister = await PostRegisterAsync(client, PermitLimit + 1);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedLogin.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedRegister.StatusCode);
    }

    // Critério 3 do card: a política é aplicada por atributo no AuthController, então nenhuma
    // outra rota pode herdá-la por engano. Sem isso, um limitador global cortaria a API inteira.
    [Fact]
    public async Task RoutesOutsideTheAuthPolicy_AreNeverRateLimited()
    {
        using var factory = CreateRateLimitedFactory();
        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= (PermitLimit * 3) + 1; attempt++)
        {
            using var response = await client.GetAsync("/api/v1/me");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static Task<HttpResponseMessage> PostRegisterAsync(HttpClient client, int attempt) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                name = "Rate Limited",
                email = $"rate.limited.{attempt}@example.com",
                password = "Str0ng!Pass",
            });

    private static Task<HttpResponseMessage> PostLoginAsync(HttpClient client) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "rate.limited@example.com", password = "Wr0ng!Pass" });

    private WebApplicationFactory<Program> CreateRateLimitedFactory() =>
        Fixture.Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:PermitLimit", PermitLimit.ToString()));
}
