using FCG.Application.Common;
using FCG.Application.Identity;
using FCG.Domain.Identity;
using FCG.Infrastructure.Common;
using FCG.Infrastructure.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FCG.IntegrationTests.Identity;

public sealed class AdminBootstrapTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string AdminEmail = "admin@example.com";
    private const string AdminPassword = "Adm1n!Pass";
    private const string ValidSigningKey = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Startup_WithSeedExplicitlyDisabled_OnlyLogsAWarning()
    {
        var logs = new CapturingLoggerProvider();
        using var host = BuildHost(Environments.Development, logs);

        await host.StartAsync();

        Assert.Contains(
            logs.Entries,
            entry => entry.Level is LogLevel.Warning &&
                     Equals(entry.Field("Reason")?.ToString(), "MissingConfiguration"));
        Assert.Null(await FindOnlyUserAsync());
    }

    [Fact]
    public async Task Startup_OutsideDevelopment_DoesNotBootstrapEvenWhenConfigured()
    {
        var logs = new CapturingLoggerProvider();
        using var host = BuildHost(Environments.Production, logs, AdminEmail, AdminPassword);

        await host.StartAsync();

        Assert.Null(await FindOnlyUserAsync());
        Assert.DoesNotContain(
            logs.Entries,
            entry => entry.Category == typeof(AdminBootstrapHostedService).FullName);
    }

    [Fact]
    public async Task Startup_OutsideDevelopment_DoesNotReadPartialBootstrapConfiguration()
    {
        var logs = new CapturingLoggerProvider();
        using var host = BuildHost(
            Environments.Production,
            logs,
            email: AdminEmail,
            password: null);

        await host.StartAsync();

        Assert.Null(await FindOnlyUserAsync());
    }

    [Fact]
    public async Task Startup_WithConfiguration_CreatesAnActiveAdministratorWithoutLeakingSecrets()
    {
        var logs = new CapturingLoggerProvider();
        using var host = BuildHost(Environments.Development, logs, AdminEmail, AdminPassword);

        await host.StartAsync();

        var administrator = Assert.IsType<User>(await FindOnlyUserAsync());
        Assert.Equal("Administrator", administrator.Name);
        Assert.Equal(AdminEmail, administrator.Email.Value);
        Assert.Equal(UserRole.Administrator, administrator.Role);
        Assert.True(administrator.IsActive);
        Assert.NotEqual(AdminPassword, administrator.PasswordHash);

        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        Assert.True(passwordHasher.Verify(administrator.PasswordHash, AdminPassword));

        Assert.Contains(
            logs.Entries,
            entry => Equals(entry.Field("Result")?.ToString(), nameof(AdminBootstrapResult.Created)));

        var registeredEvent = Assert.Single(logs.Entries.Where(entry =>
            entry.Category == typeof(AdminBootstrapHostedService).FullName &&
            entry.Message.StartsWith("UserRegistered", StringComparison.Ordinal)));

        Assert.Equal(
            "UserRegistered {TargetUserId} {MaskedEmail} {TraceId}",
            registeredEvent.Field("{OriginalFormat}"));
        Assert.Equal(administrator.Id, registeredEvent.Field("TargetUserId"));
        Assert.Equal("a***@example.com", registeredEvent.Field("MaskedEmail"));
        Assert.Null(registeredEvent.Field("TraceId"));
        AssertLogsDoNotContainSecrets(logs, administrator.PasswordHash);
    }

    [Fact]
    public async Task Startup_WhenRepeated_DoesNotDuplicateOrReplaceTheAdministrator()
    {
        using (var firstHost = BuildHost(
                   Environments.Development,
                   new CapturingLoggerProvider(),
                   AdminEmail,
                   AdminPassword))
        {
            await firstHost.StartAsync();
        }

        var original = Assert.IsType<User>(await FindOnlyUserAsync());
        var secondLogs = new CapturingLoggerProvider();
        using var secondHost = BuildHost(
            Environments.Development,
            secondLogs,
            AdminEmail,
            AdminPassword);

        await secondHost.StartAsync();

        var persisted = Assert.IsType<User>(await FindOnlyUserAsync());
        Assert.Equal(original.Id, persisted.Id);
        Assert.Equal(original.PasswordHash, persisted.PasswordHash);
        Assert.Equal(1, await CountUsersAsync());
        Assert.Contains(
            secondLogs.Entries,
            entry => Equals(
                entry.Field("Result")?.ToString(),
                nameof(AdminBootstrapResult.AlreadyConfigured)));
        Assert.DoesNotContain(
            secondLogs.Entries,
            entry => entry.Message.StartsWith("UserRegistered", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Startup_WhenEmailBelongsToACommonUser_FailsWithoutPromotingTheAccount()
    {
        await CreateCommonUserAsync();
        var logs = new CapturingLoggerProvider();
        using var host = BuildHost(Environments.Development, logs, AdminEmail, AdminPassword);

        await Assert.ThrowsAsync<AdminBootstrapConflictException>(() => host.StartAsync());

        var persisted = Assert.IsType<User>(await FindOnlyUserAsync());
        Assert.Equal(UserRole.User, persisted.Role);
        Assert.True(persisted.IsActive);
        Assert.Equal(1, await CountUsersAsync());
        AssertLogsDoNotContainSecrets(logs, persisted.PasswordHash);
    }

    [Fact]
    public async Task Startup_WhenAdministratorIsInactive_FailsWithoutReactivatingTheAccount()
    {
        using (var firstHost = BuildHost(
                   Environments.Development,
                   new CapturingLoggerProvider(),
                   AdminEmail,
                   AdminPassword))
        {
            await firstHost.StartAsync();
        }

        await DeactivateOnlyUserAsync();
        var logs = new CapturingLoggerProvider();
        using var secondHost = BuildHost(
            Environments.Development,
            logs,
            AdminEmail,
            AdminPassword);

        await Assert.ThrowsAsync<AdminBootstrapConflictException>(() => secondHost.StartAsync());

        var persisted = Assert.IsType<User>(await FindOnlyUserAsync());
        Assert.Equal(UserRole.Administrator, persisted.Role);
        Assert.False(persisted.IsActive);
        AssertLogsDoNotContainSecrets(logs, persisted.PasswordHash);
    }

    private IHost BuildHost(
        string environment,
        CapturingLoggerProvider logs,
        string? email = null,
        string? password = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:FcgDatabase"] = Fixture.ConnectionString,
            ["Jwt:Issuer"] = "fcg-api",
            ["Jwt:Audience"] = "fcg-clients",
            ["Jwt:SigningKey"] = ValidSigningKey,
            ["Jwt:ExpirationMinutes"] = "60",
            // Estes testes exercitam o bootstrap do administrador; o seed do usuário comum é
            // desligado para que "o único usuário" continue significando o administrador.
            ["AdminBootstrap:PlayerEmail"] = string.Empty,
            ["AdminBootstrap:PlayerPassword"] = string.Empty,
            // Sem estes, os defaults de Development entrariam e não haveria cenário "não configurado".
            ["AdminBootstrap:Email"] = string.Empty,
            ["AdminBootstrap:Password"] = string.Empty,
        };

        if (email is not null)
        {
            values["AdminBootstrap:Email"] = email;
        }

        if (password is not null)
        {
            values["AdminBootstrap:Password"] = password;
        }

        return new HostBuilder()
            .UseEnvironment(environment)
            .ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(values))
            .ConfigureLogging(logging => logging.AddProvider(logs))
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IClock, SystemClock>();
                services.AddIdentityModule(context.Configuration);
                services.AddPersistenceModule(context.Configuration);
            })
            .Build();
    }

    private async Task CreateCommonUserAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = User.Register(
            "Common User",
            Email.Create(AdminEmail),
            passwordHasher.Hash(AdminPassword),
            DateTime.UtcNow);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }

    private async Task DeactivateOnlyUserAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var user = await dbContext.Users.SingleAsync(current => current.Email == Email.Create(AdminEmail));
        dbContext.Entry(user).Property(current => current.IsActive).CurrentValue = false;
        await dbContext.SaveChangesAsync();
    }

    private async Task<User?> FindOnlyUserAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        return await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Email == Email.Create(AdminEmail));
    }

    private async Task<int> CountUsersAsync()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();
        return await dbContext.Users.CountAsync();
    }

    private static void AssertLogsDoNotContainSecrets(
        CapturingLoggerProvider logs,
        string passwordHash)
    {
        var loggedText = string.Join('\n', logs.AllText());
        Assert.DoesNotContain(AdminEmail, loggedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AdminPassword, loggedText, StringComparison.Ordinal);
        Assert.DoesNotContain(passwordHash, loggedText, StringComparison.Ordinal);
    }
}
