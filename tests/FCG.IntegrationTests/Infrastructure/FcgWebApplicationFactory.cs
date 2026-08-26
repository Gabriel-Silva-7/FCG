using FCG.IntegrationTests.Errors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCG.IntegrationTests.Infrastructure;

internal sealed class FcgWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    public CapturingLoggerProvider Logs { get; } = new();

    private const string TestSigningKey = "fcg-integration-tests-signing-key-32-characters";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FcgDatabase", connectionString);
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureServices(
            services => services
                .AddControllers()
                .AddApplicationPart(typeof(ErrorTestController).Assembly));
    }
}
