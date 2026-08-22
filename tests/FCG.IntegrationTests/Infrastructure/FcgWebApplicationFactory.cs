using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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
    }
}
