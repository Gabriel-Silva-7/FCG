using System.Net;
using System.Net.Http.Json;
using FCG.IntegrationTests.Infrastructure;

namespace FCG.IntegrationTests.Logging;

public sealed class SensitivePersistenceLoggingTests(FcgApiFixture fixture)
    : DatabaseBackedTest(fixture)
{
    [Fact]
    public async Task PersistingAUser_KeepsItsEmailAndPasswordHashOutOfTheEfCoreLogs()
    {
        const string email = "sentinela.persistencia@example.com";
        const string passwordHash = "SENTINELA_HASH_DE_SENHA_PERSISTIDO";

        Fixture.Logs.Clear();
        using var client = Fixture.Factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/_test/logging/persist-user",
            new { Email = email, PasswordHash = passwordHash });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Contains(
            Fixture.Logs.Entries,
            entry => entry.Category.StartsWith(
                "Microsoft.EntityFrameworkCore", StringComparison.Ordinal));

        foreach (var sensitiveValue in new[] { email, passwordHash })
        {
            var leak = Fixture.Logs.AllText().FirstOrDefault(text =>
                text.Contains(sensitiveValue, StringComparison.OrdinalIgnoreCase));

            Assert.True(
                leak is null,
                $"'{sensitiveValue}' vazou para o log do EF Core em: {leak}");
        }
    }
}
