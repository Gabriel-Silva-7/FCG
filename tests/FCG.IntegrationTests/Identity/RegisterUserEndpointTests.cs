using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Api.Contracts;
using FCG.Api.Controllers;
using FCG.Application.Identity;
using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.IntegrationTests.Identity;

public sealed class RegisterUserEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Endpoint = "/api/v1/auth/register";

    [Fact]
    public async Task Register_WithValidInput_CreatesRegularUserWithoutLeakingSecrets()
    {
        const string password = "Str0ng!Pass";
        const string email = "New.User@Example.com";
        Fixture.Logs.Clear();
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new
            {
                name = "  New User  ",
                email,
                password,
                role = "Administrator",
                isActive = false,
            });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/me", response.Headers.Location?.OriginalString);
        Assert.Equal("New User", body.GetProperty("name").GetString());
        Assert.Equal("new.user@example.com", body.GetProperty("email").GetString());
        Assert.Equal(nameof(UserRole.User), body.GetProperty("role").GetString());
        Assert.False(body.TryGetProperty("password", out _));
        Assert.False(body.TryGetProperty("passwordHash", out _));

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = await dbContext.Users.AsNoTracking().SingleAsync();

        Assert.Equal("New User", user.Name);
        Assert.Equal("new.user@example.com", user.Email.Value);
        Assert.Equal(UserRole.User, user.Role);
        Assert.True(user.IsActive);
        Assert.NotEqual(password, user.PasswordHash);
        Assert.True(passwordHasher.Verify(user.PasswordHash, password));

        var loggedText = string.Join('\n', Fixture.Logs.AllText());
        Assert.DoesNotContain(password, loggedText, StringComparison.Ordinal);
        Assert.DoesNotContain(user.PasswordHash, loggedText, StringComparison.Ordinal);
        Assert.DoesNotContain(email, loggedText, StringComparison.OrdinalIgnoreCase);

        var eventEntry = Assert.Single(Fixture.Logs.Entries.Where(entry =>
            entry.Category == typeof(AuthController).FullName &&
            entry.Message.StartsWith("UserRegistered", StringComparison.Ordinal)));
        var requestEntry = Assert.Single(Fixture.Logs.Entries.Where(entry =>
            entry.Message.StartsWith("HttpRequest", StringComparison.Ordinal)));

        Assert.Equal(LogLevel.Information, eventEntry.Level);
        Assert.Equal(
            "UserRegistered {TargetUserId} {MaskedEmail} {TraceId}",
            eventEntry.Field("{OriginalFormat}"));
        Assert.Equal(user.Id, eventEntry.Field("TargetUserId"));
        Assert.Equal("n***@example.com", eventEntry.Field("MaskedEmail"));
        Assert.Equal(requestEntry.Field("TraceId"), eventEntry.Field("TraceId"));
        Assert.False(eventEntry.HasField("ActorUserId"));
    }

    [Fact]
    public async Task Register_WithInvalidDomainInput_ReturnsCanonicalValidationProblem()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            Endpoint,
            new
            {
                name = "Valid Name",
                email = "missing-domain@",
                password = "abcdefgh",
            });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("validation_error", body.GetProperty("code").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.Equal(Endpoint, body.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("traceId").GetString()));

        var errors = body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        Assert.False(await dbContext.Users.AnyAsync());
    }

    [Fact]
    public async Task Register_WithNormalizedDuplicateEmail_ReturnsConflictWithoutCreatingAnotherUser()
    {
        using var client = CreateClient();
        var firstRequest = new
        {
            name = "First User",
            email = "user@example.com",
            password = "Str0ng!Pass",
        };
        var duplicateRequest = new
        {
            name = "Second User",
            email = "  USER@EXAMPLE.COM  ",
            password = "An0ther!Pass",
        };

        using var firstResponse = await client.PostAsJsonAsync(Endpoint, firstRequest);
        using var duplicateResponse = await client.PostAsJsonAsync(Endpoint, duplicateRequest);
        var body = await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal("email_already_registered", body.GetProperty("code").GetString());

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        Assert.Equal(1, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task Register_WhenRequestsCompeteForTheSameEmail_ReturnsCreatedAndConflict()
    {
        using var client = CreateClient();
        var requests = new[]
        {
            new { name = "First User", email = "race@example.com", password = "Str0ng!Pass" },
            new { name = "Second User", email = "race@example.com", password = "An0ther!Pass" },
        };

        var responses = await Task.WhenAll(
            requests.Select(request => client.PostAsJsonAsync(Endpoint, request)));

        try
        {
            Assert.Equal(
                [HttpStatusCode.Created, HttpStatusCode.Conflict],
                responses.Select(response => response.StatusCode).Order());

            var conflict = Assert.Single(
                responses,
                response => response.StatusCode is HttpStatusCode.Conflict);
            var body = await conflict.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("email_already_registered", body.GetProperty("code").GetString());

            await using var scope = Fixture.Factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
            Assert.Equal(1, await dbContext.Users.CountAsync());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
}
