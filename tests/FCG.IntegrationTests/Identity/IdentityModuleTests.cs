using FCG.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FCG.IntegrationTests.Identity;

public sealed class IdentityModuleTests
{
    private const string ValidSigningKey = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task AddIdentityModule_WhenConfigurationIsValid_BindsOptionsAndStartsHost()
    {
        using var host = BuildHost(JwtConfiguration(ValidSigningKey));

        await host.StartAsync();

        var options = host.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        Assert.Equal("fcg-api", options.Issuer);
        Assert.Equal("fcg-clients", options.Audience);
        Assert.Equal(ValidSigningKey, options.SigningKey);
        Assert.Equal(60, options.ExpirationMinutes);
    }

    [Fact]
    public async Task AddIdentityModule_WhenSigningKeyIsTooShort_FailsOnHostStartup()
    {
        using var host = BuildHost(JwtConfiguration(new string('a', 31)));

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());

        Assert.Contains("SigningKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddIdentityModule_WhenSigningKeyIsMissing_FailsOnHostStartup()
    {
        using var host = BuildHost(JwtConfiguration(signingKey: null));

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task AddIdentityModule_WhenAdminBootstrapIsPartiallyConfigured_FailsOnHostStartup()
    {
        var values = JwtConfiguration(ValidSigningKey);
        values["AdminBootstrap:Email"] = "admin@example.com";
        using var host = BuildHost(values);

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    private static IHost BuildHost(Dictionary<string, string?> values)
    {
        return new HostBuilder()
            .ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(values))
            .ConfigureServices((context, services) =>
                services.AddIdentityModule(context.Configuration))
            .Build();
    }

    private static Dictionary<string, string?> JwtConfiguration(string? signingKey)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "fcg-api",
            ["Jwt:Audience"] = "fcg-clients",
            ["Jwt:ExpirationMinutes"] = "60",
        };

        if (signingKey is not null)
        {
            values["Jwt:SigningKey"] = signingKey;
        }

        return values;
    }
}
