using FCG.IntegrationTests.Errors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Infrastructure;

internal sealed class FcgWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    private const string TestSigningKey = "fcg-integration-tests-signing-key-32-characters";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FcgDatabase", connectionString);
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.ConfigureServices(
            services => services
                .AddControllers()
                .AddApplicationPart(typeof(ErrorTestController).Assembly));
    }
}
