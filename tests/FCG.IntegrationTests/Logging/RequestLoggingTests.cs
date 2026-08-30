using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Api.Errors;
using FCG.IntegrationTests.Errors;
using FCG.IntegrationTests.Infrastructure;

namespace FCG.IntegrationTests.Logging;

[Collection(FcgApiCollection.Name)]
public sealed class RequestLoggingTests(FcgApiFixture fixture)
{
    private const string RequestEventName = "HttpRequest";

    [Fact]
    public async Task Request_ProducesOneEntryWithTheStructuredFieldsRequiredByTheProject()
    {
        fixture.Logs.Clear();
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync("/_test/logging/plain");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var entry = Assert.Single(RequestEntries());

        Assert.Equal("GET", entry.Field("Method"));
        Assert.Equal("/_test/logging/plain", entry.Field("Route"));
        Assert.Equal(204, entry.Field("StatusCode"));
        var timestampUtc = Assert.IsType<DateTime>(entry.Field("TimestampUtc"));
        Assert.Equal(DateTimeKind.Utc, timestampUtc.Kind);
        Assert.True(entry.HasField("TraceId"), "O campo TraceId é o que liga o log ao corpo de erro.");
        Assert.True(entry.HasField("DurationMs"), "A duração é campo exigido pelo §10.3 do refinamento.");
        Assert.True(entry.HasField("UserId"), "O userId é exigido pelo §10.3; é nulo enquanto anônimo.");
    }

    [Fact]
    public async Task AuthenticatedRequest_LogsTheTokenSubjectAsUserId()
    {
        const string password = "Str0ng!Pass";
        var email = $"request-log-{Guid.NewGuid():N}@example.com";
        using var client = fixture.Factory.CreateClient();

        using var registrationResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name = "Request Log User", email, password });
        var registered = await registrationResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });
        var token = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.GetProperty("accessToken").GetString());
        fixture.Logs.Clear();

        using var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = Assert.Single(RequestEntries());
        Assert.Equal(
            registered.GetProperty("id").GetGuid().ToString(),
            entry.Field("UserId"));
    }

    [Fact]
    public async Task NothingSensitiveInTheRequestReachesAnyLogEntry()
    {
        const string passwordInBody = "SENTINELA_SENHA_NO_CORPO_1!";
        const string emailInBody = "sentinela.email@example.com";
        const string jwt = "eyJhbGciOiJIUzI1NiJ9.SENTINELA_JWT.assinatura";
        const string passwordInQuery = "SENTINELA_SENHA_NA_QUERY";

        fixture.Logs.Clear();
        using var client = fixture.Factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/_test/logging/echo?password={passwordInQuery}")
        {
            Content = JsonContent.Create(new { Email = emailInBody, Password = passwordInBody }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotEmpty(RequestEntries());

        foreach (var sensitiveValue in new[] { passwordInBody, emailInBody, jwt, passwordInQuery })
        {
            var leak = fixture.Logs.AllText().FirstOrDefault(
                text => text.Contains(sensitiveValue, StringComparison.OrdinalIgnoreCase));

            Assert.True(leak is null, $"'{sensitiveValue}' vazou para o log em: {leak}");
        }
    }

    [Theory]
    [InlineData("/_test/logging/route/SENTINELA_SEGREDO_NA_ROTA", "/_test/logging/route/{value}")]
    [InlineData("/SENTINELA_SEGREDO_NA_ROTA", "<unmatched>")]
    public async Task SensitivePathSegments_AreReplacedByTheRouteTemplate(
        string requestUri,
        string expectedRoute)
    {
        const string sensitiveValue = "SENTINELA_SEGREDO_NA_ROTA";

        fixture.Logs.Clear();
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync(requestUri);

        var entry = Assert.Single(RequestEntries());

        Assert.Equal(expectedRoute, entry.Field("Route"));
        Assert.DoesNotContain(
            fixture.Logs.AllText(),
            text => text.Contains(sensitiveValue, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RequestLog_CarriesTheSameTraceIdTheErrorBodyReturnsToTheClient()
    {
        fixture.Logs.Clear();
        using var client = fixture.Factory.CreateClient();

        using var response = await client.GetAsync("/_test/errors/status/404");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var traceIdFromBody = problem.GetProperty("traceId").GetString();

        Assert.False(string.IsNullOrWhiteSpace(traceIdFromBody));

        var entry = Assert.Single(RequestEntries());

        Assert.Equal(traceIdFromBody, entry.Field("TraceId"));
    }

    [Fact]
    public async Task UnhandledException_IsLoggedWithoutItsMessageOrStackTrace()
    {
        fixture.Logs.Clear();
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync("/_test/errors/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(
            fixture.Logs.AllText(),
            text => text.Contains(ErrorTestController.ExceptionMessage, StringComparison.Ordinal));

        var entry = Assert.Single(fixture.Logs.Entries.Where(log =>
            log.Category == typeof(GlobalExceptionHandler).FullName));

        Assert.Equal(typeof(InvalidOperationException).FullName, entry.Field("ExceptionType"));
        Assert.True(entry.HasField("TraceId"));
        Assert.Null(entry.ExceptionText);
    }

    private IEnumerable<CapturedLogEntry> RequestEntries() =>
        fixture.Logs.Entries.Where(entry =>
            entry.Message.StartsWith(RequestEventName, StringComparison.Ordinal));
}
