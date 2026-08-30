using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FCG.Api.Identity;
using FCG.Application.Identity;
using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.IntegrationTests.Identity;

public sealed class ChangeUserStatusEndpointTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string Endpoint = "/api/v1/admin/users";
    private const string Password = "Str0ng!Pass";
    private const string AdminEmail = "admin@example.com";
    private const string TargetEmail = "target@example.com";

    [Fact]
    public async Task CorrectVersions_BlockAndUnblockAThirdPartyAndRotateXmin()
    {
        using var adminClient = CreateClient();
        using var targetClient = CreateClient();
        var administratorId = await CreateAdministratorAsync();
        var targetId = await RegisterAsync(targetClient, "Target User", TargetEmail);
        await AuthenticateAsync(targetClient, TargetEmail);
        await AuthenticateAsync(adminClient, AdminEmail);
        var original = await FindUserAsync(adminClient, TargetEmail);
        var originalVersion = original.GetProperty("version").GetString()!;
        Fixture.Logs.Clear();

        using var blockedResponse = await ChangeStatusAsync(
            adminClient,
            targetId,
            isActive: false,
            originalVersion);
        var blocked = await blockedResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, blockedResponse.StatusCode);
        Assert.False(blocked.GetProperty("isActive").GetBoolean());
        var blockedVersion = blocked.GetProperty("version").GetString()!;
        Assert.NotEqual(originalVersion, blockedVersion);

        using var rejectedTargetRequest = await targetClient.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedTargetRequest.StatusCode);

        using var unblockedResponse = await ChangeStatusAsync(
            adminClient,
            targetId,
            isActive: true,
            blockedVersion);
        var unblocked = await unblockedResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, unblockedResponse.StatusCode);
        Assert.True(unblocked.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(blockedVersion, unblocked.GetProperty("version").GetString());

        using var restoredTargetRequest = await targetClient.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, restoredTargetRequest.StatusCode);

        var eventEntries = Fixture.Logs.Entries.Where(entry =>
            entry.Category == typeof(AdminUsersController).FullName &&
            (entry.Message.StartsWith("UserBlocked", StringComparison.Ordinal) ||
             entry.Message.StartsWith("UserUnblocked", StringComparison.Ordinal))).ToArray();
        var patchTraceIds = Fixture.Logs.Entries.Where(entry =>
                entry.Message.StartsWith("HttpRequest", StringComparison.Ordinal) &&
                Equals(entry.Field("Method"), "PATCH"))
            .Select(entry => entry.Field("TraceId"))
            .ToHashSet();

        Assert.Equal(2, eventEntries.Length);
        Assert.StartsWith("UserBlocked", eventEntries[0].Message, StringComparison.Ordinal);
        Assert.StartsWith("UserUnblocked", eventEntries[1].Message, StringComparison.Ordinal);
        Assert.Equal(
            "UserBlocked {ActorUserId} {TargetUserId} {TraceId}",
            eventEntries[0].Field("{OriginalFormat}"));
        Assert.Equal(
            "UserUnblocked {ActorUserId} {TargetUserId} {TraceId}",
            eventEntries[1].Field("{OriginalFormat}"));
        Assert.All(eventEntries, entry =>
        {
            Assert.Equal(LogLevel.Information, entry.Level);
            Assert.Equal(administratorId, entry.Field("ActorUserId"));
            Assert.Equal(targetId, entry.Field("TargetUserId"));
            Assert.Contains(entry.Field("TraceId"), patchTraceIds);
            Assert.False(entry.HasField("MaskedEmail"));
        });
    }

    [Fact]
    public async Task AdministratorDeactivatingSelf_ReturnsCanonicalConflictWithoutChangingTheAccount()
    {
        using var client = CreateClient();
        var administratorId = await CreateAdministratorAsync();
        await AuthenticateAsync(client, AdminEmail);
        var original = await FindUserAsync(client, AdminEmail);
        var originalVersion = original.GetProperty("version").GetString()!;
        Fixture.Logs.Clear();

        using var response = await ChangeStatusAsync(
            client,
            administratorId,
            isActive: false,
            originalVersion);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("cannot_deactivate_self", problem.GetProperty("code").GetString());
        Assert.Equal(
            "urn:fcg:error:cannot-deactivate-self",
            problem.GetProperty("type").GetString());

        var persisted = await FindUserAsync(client, AdminEmail);
        Assert.True(persisted.GetProperty("isActive").GetBoolean());
        Assert.Equal(originalVersion, persisted.GetProperty("version").GetString());

        var logEntry = Assert.Single(Fixture.Logs.Entries.Where(entry =>
            entry.Category == typeof(AdminUsersController).FullName &&
            entry.Message.StartsWith("UserSelfDeactivationRejected", StringComparison.Ordinal)));

        Assert.Equal(LogLevel.Warning, logEntry.Level);
        Assert.Equal(administratorId, logEntry.Field("ActorUserId"));
        Assert.Equal(administratorId, logEntry.Field("TargetUserId"));
        Assert.False(logEntry.HasField("Result"));
        Assert.Equal(problem.GetProperty("traceId").GetString(), logEntry.Field("TraceId"));
        Assert.DoesNotContain(
            logEntry.TextValues(),
            text => text.Contains(AdminEmail, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AdministratorKeepingSelfActive_WithCorrectVersion_ReturnsOk()
    {
        using var client = CreateClient();
        var administratorId = await CreateAdministratorAsync();
        await AuthenticateAsync(client, AdminEmail);
        var original = await FindUserAsync(client, AdminEmail);
        var originalVersion = original.GetProperty("version").GetString()!;

        using var response = await ChangeStatusAsync(
            client,
            administratorId,
            isActive: true,
            originalVersion);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(updated.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(originalVersion, updated.GetProperty("version").GetString());
    }

    [Fact]
    public async Task AdministratorDeactivatingSelf_WithMalformedVersion_ReturnsValidationErrorFirst()
    {
        using var client = CreateClient();
        var administratorId = await CreateAdministratorAsync();
        await AuthenticateAsync(client, AdminEmail);
        var original = await FindUserAsync(client, AdminEmail);
        var originalVersion = original.GetProperty("version").GetString()!;
        Fixture.Logs.Clear();

        using var response = await ChangeStatusAsync(
            client,
            administratorId,
            isActive: false,
            version: "invalid");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", problem.GetProperty("code").GetString());

        var persisted = await FindUserAsync(client, AdminEmail);
        Assert.True(persisted.GetProperty("isActive").GetBoolean());
        Assert.Equal(originalVersion, persisted.GetProperty("version").GetString());
        Assert.DoesNotContain(Fixture.Logs.Entries, entry =>
            entry.Message.StartsWith("UserSelfDeactivationRejected", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StaleWriterRequestingTheCurrentState_StillReceivesCanonicalConflict()
    {
        using var firstWriter = CreateClient();
        using var secondWriter = CreateClient();
        await CreateAdministratorAsync();
        var targetId = await RegisterAsync(firstWriter, "Target User", TargetEmail);
        await AuthenticateAsync(firstWriter, AdminEmail);
        await AuthenticateAsync(secondWriter, AdminEmail);
        var original = await FindUserAsync(firstWriter, TargetEmail);
        var sharedVersion = original.GetProperty("version").GetString()!;

        using var firstResponse = await ChangeStatusAsync(
            firstWriter,
            targetId,
            isActive: false,
            sharedVersion);
        using var secondResponse = await ChangeStatusAsync(
            secondWriter,
            targetId,
            isActive: false,
            sharedVersion);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var conflict = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("concurrency_conflict", conflict.GetProperty("code").GetString());
        Assert.Equal(
            "urn:fcg:error:concurrency-conflict",
            conflict.GetProperty("type").GetString());
        Assert.False(conflict.TryGetProperty("detail", out _));

        var persisted = await FindUserAsync(firstWriter, TargetEmail);
        Assert.False(persisted.GetProperty("isActive").GetBoolean());
        Assert.Equal(
            firstBody.GetProperty("version").GetString(),
            persisted.GetProperty("version").GetString());
    }

    [Fact]
    public async Task MissingUser_ReturnsCanonicalNotFound()
    {
        using var client = CreateClient();
        await CreateAdministratorAsync();
        await AuthenticateAsync(client, AdminEmail);

        using var response = await ChangeStatusAsync(
            client,
            Guid.NewGuid(),
            isActive: false,
            version: "1");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("resource_not_found", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InvalidVersion_ReturnsCanonicalValidationErrorWithoutWriting()
    {
        using var client = CreateClient();
        await CreateAdministratorAsync();
        var targetId = await RegisterAsync(client, "Target User", TargetEmail);
        await AuthenticateAsync(client, AdminEmail);

        using var response = await ChangeStatusAsync(
            client,
            targetId,
            isActive: false,
            version: "invalid");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation_error", problem.GetProperty("code").GetString());

        var persisted = await FindUserAsync(client, TargetEmail);
        Assert.True(persisted.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task CommonUser_IsForbiddenFromChangingStatus()
    {
        using var client = CreateClient();
        var targetId = await RegisterAsync(client, "Common User", TargetEmail);
        await AuthenticateAsync(client, TargetEmail);

        using var response = await ChangeStatusAsync(
            client,
            targetId,
            isActive: false,
            version: "1");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("forbidden", problem.GetProperty("code").GetString());
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        Guid userId,
        bool isActive,
        string version) =>
        client.PatchAsJsonAsync(
            $"{Endpoint}/{userId}/status",
            new { isActive, version });

    private static async Task<JsonElement> FindUserAsync(HttpClient client, string email)
    {
        using var response = await client.GetAsync(
            $"{Endpoint}?search={Uri.EscapeDataString(email)}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.Single(body.GetProperty("items").EnumerateArray().ToArray()).Clone();
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

    private static async Task<Guid> RegisterAsync(HttpClient client, string name, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { name, email, password = Password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return body.GetProperty("id").GetGuid();
    }

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

    private HttpClient CreateClient() =>
        Fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
}
