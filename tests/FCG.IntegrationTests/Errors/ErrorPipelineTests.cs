using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FCG.IntegrationTests.Errors;

[Collection(FcgApiCollection.Name)]
public sealed class ErrorPipelineTests(FcgApiFixture fixture)
{
    [Theory]
    [InlineData("/_test/errors/status/401", 401, "unauthenticated")]
    [InlineData("/_test/errors/status/403", 403, "forbidden")]
    [InlineData("/_test/errors/status/404", 404, "resource_not_found")]
    [InlineData("/_test/errors/status/429", 429, "rate_limit_exceeded")]
    [InlineData("/_test/route-does-not-exist?email=secret@example.com", 404, "resource_not_found")]
    public async Task StatusWithoutBody_ReturnsCanonicalProblemDetails(
        string requestUri,
        int expectedStatus,
        string expectedCode)
    {
        using var client = CreateClient();
        using var response = await client.GetAsync(requestUri);
        using var document = await ReadProblemDetailsAsync(response);

        AssertCanonicalProblem(
            response,
            document.RootElement,
            expectedStatus,
            expectedCode,
            new Uri(requestUri, UriKind.Relative).OriginalString.Split('?')[0]);
        Assert.DoesNotContain("secret@example.com", document.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MethodNotAllowed_PreservesStatusThroughTheFallbackDescriptor()
    {
        using var client = CreateClient();
        using var response = await client.PostAsync("/_test/errors/status/204", content: null);
        using var document = await ReadProblemDetailsAsync(response);

        AssertCanonicalProblem(
            response,
            document.RootElement,
            StatusCodes.Status405MethodNotAllowed,
            "http_405",
            "/_test/errors/status/204");
    }

    [Fact]
    public async Task UnhandledException_ReturnsInternalErrorWithoutInternalDetails()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/_test/errors/throw");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        AssertCanonicalProblem(
            response,
            document.RootElement,
            StatusCodes.Status500InternalServerError,
            "internal_error",
            "/_test/errors/throw");
        Assert.DoesNotContain(ErrorTestController.ExceptionMessage, json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), json, StringComparison.Ordinal);
        Assert.False(
            document.RootElement.TryGetProperty("detail", out var detail) &&
            detail.ValueKind is not JsonValueKind.Null);
    }

    [Fact]
    public async Task UnexpectedArgumentException_RemainsAnInternalError()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/_test/errors/throw-argument");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        AssertCanonicalProblem(
            response,
            document.RootElement,
            StatusCodes.Status500InternalServerError,
            "internal_error",
            "/_test/errors/throw-argument");
        Assert.DoesNotContain(ErrorTestController.ExceptionMessage, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SpecificProblem_PreservesItsCodeAndReceivesRequestMetadata()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/_test/errors/specific");
        using var document = await ReadProblemDetailsAsync(response);

        AssertCanonicalProblem(
            response,
            document.RootElement,
            StatusCodes.Status409Conflict,
            "game_already_acquired",
            "/_test/errors/specific");
    }

    [Fact]
    public async Task InternalProblemReturnedByController_DropsUnsafeDetail()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/_test/errors/unsafe-internal");
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        AssertCanonicalProblem(
            response,
            document.RootElement,
            StatusCodes.Status500InternalServerError,
            "internal_error",
            "/_test/errors/unsafe-internal");
        Assert.DoesNotContain(ErrorTestController.ExceptionMessage, json, StringComparison.Ordinal);
        Assert.False(
            document.RootElement.TryGetProperty("detail", out var detail) &&
            detail.ValueKind is not JsonValueKind.Null);
    }

    [Fact]
    public async Task ProblemDetailsInsideSuccessfulObjectResult_RemainsSuccessful()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/_test/errors/successful-problem");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("internal_error", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AutomaticValidation_PreservesErrorsInsideTheCanonicalContract()
    {
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync("/_test/errors/validation", new { });
        using var document = await ReadProblemDetailsAsync(response);

        AssertCanonicalProblem(
            response,
            document.RootElement,
            StatusCodes.Status400BadRequest,
            "validation_error",
            "/_test/errors/validation");

        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Name", out var nameErrors));
        Assert.NotEmpty(nameErrors.EnumerateArray());
    }

    private HttpClient CreateClient() =>
        fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

    private static async Task<JsonDocument> ReadProblemDetailsAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(json));

        return JsonDocument.Parse(json);
    }

    private static void AssertCanonicalProblem(
        HttpResponseMessage response,
        JsonElement problem,
        int expectedStatus,
        string expectedCode,
        string expectedInstance)
    {
        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        Assert.Equal(expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.Equal(
            "urn:fcg:error:" + expectedCode.Replace('_', '-'),
            problem.GetProperty("type").GetString());
        Assert.Equal(expectedInstance, problem.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }
}
