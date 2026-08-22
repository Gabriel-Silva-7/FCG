using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Infrastructure;

// Os dois testes gravam DELIBERADAMENTE o mesmo e-mail. Sem o reset por teste, o segundo a
// rodar falharia em UX_Users_Email. Se ambos passam, o isolamento está funcionando.
public sealed class DatabaseIsolationTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    private const string SharedEmail = "isolation-probe@example.com";

    [Fact]
    public Task FirstTest_WritesTheSharedEmail() => AssertSingleUserAfterInsert();

    [Fact]
    public Task SecondTest_WritesTheSameEmailAgain() => AssertSingleUserAfterInsert();

    private async Task AssertSingleUserAfterInsert()
    {
        await using var scope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();

        Assert.Empty(await dbContext.Users.ToListAsync());

        dbContext.Users.Add(User.Register(
            "Isolation Probe",
            Email.Create(SharedEmail),
            "hash",
            DateTime.UnixEpoch));

        await dbContext.SaveChangesAsync();

        Assert.Single(await dbContext.Users.ToListAsync());
    }
}
