using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Persistence;

public sealed class PersistenceModuleTests
{
    private const string ValidConnectionString =
        "Host=localhost;Database=fcg;Username=fcg;Password=secret";

    [Fact]
    public void AddPersistenceModule_WhenConnectionStringIsConfigured_UsesNpgsqlAndConfiguredValue()
    {
        var services = new ServiceCollection();
        services.AddPersistenceModule(Configuration(ValidConnectionString));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FcgDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal(ValidConnectionString, context.Database.GetConnectionString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddPersistenceModule_WhenConnectionStringIsMissing_Throws(string? connectionString)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddPersistenceModule(Configuration(connectionString)));

        Assert.Contains("FcgDatabase", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration Configuration(string? connectionString)
    {
        var values = new Dictionary<string, string?>();

        if (connectionString is not null)
        {
            values["ConnectionStrings:FcgDatabase"] = connectionString;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
