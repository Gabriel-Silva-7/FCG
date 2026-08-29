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

    // A fixture é compartilhada: com o limite de produção, os testes existentes de register/login
    // esgotariam a janela entre si. Quem testa o limite cria a própria factory com valor pequeno.
    private const int UnlimitedPermits = 100_000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:FcgDatabase", connectionString);
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("RateLimiting:PermitLimit", UnlimitedPermits.ToString());
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureServices(
            services => services
                .AddControllers()
                .AddApplicationPart(typeof(ErrorTestController).Assembly));
    }
}
