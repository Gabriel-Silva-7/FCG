using FCG.Application.Identity;
using FCG.Domain.Identity;
using FCG.Infrastructure.Persistence;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.IntegrationTests.Identity;

public sealed class UserRepositoryConcurrencyTests(FcgApiFixture fixture) : DatabaseBackedTest(fixture)
{
    [Fact]
    public async Task SaveChanges_WhenUniqueEmailConstraintWinsTheRace_TranslatesTheViolation()
    {
        await using var firstScope = Fixture.Factory.Services.CreateAsyncScope();
        await using var secondScope = Fixture.Factory.Services.CreateAsyncScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IUserRepository>();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IUserRepository>();
        var email = Email.Create("race@example.com");

        Assert.False(await firstRepository.ExistsByEmailAsync(email, CancellationToken.None));
        Assert.False(await secondRepository.ExistsByEmailAsync(email, CancellationToken.None));

        firstRepository.Add(CreateUser("First", email));
        secondRepository.Add(CreateUser("Second", email));

        await firstRepository.SaveChangesAsync(CancellationToken.None);
        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() =>
            secondRepository.SaveChangesAsync(CancellationToken.None));

        await using var verificationScope = Fixture.Factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<FcgDbContext>();
        Assert.Equal(1, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task SaveChanges_WhenAnotherConstraintIsViolated_DoesNotMaskTheFailure()
    {
        await using var firstScope = Fixture.Factory.Services.CreateAsyncScope();
        var firstRepository = firstScope.ServiceProvider.GetRequiredService<IUserRepository>();
        var firstUser = CreateUser("First", Email.Create("first@example.com"));
        firstRepository.Add(firstUser);
        await firstRepository.SaveChangesAsync(CancellationToken.None);

        await using var secondScope = Fixture.Factory.Services.CreateAsyncScope();
        var secondRepository = secondScope.ServiceProvider.GetRequiredService<IUserRepository>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<FcgDbContext>();
        var secondUser = CreateUser("Second", Email.Create("second@example.com"));
        secondRepository.Add(secondUser);
        secondDbContext.Entry(secondUser).Property(user => user.Id).CurrentValue = firstUser.Id;

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            secondRepository.SaveChangesAsync(CancellationToken.None));
    }

    private static User CreateUser(string name, Email email) =>
        User.Register(name, email, "HASHED_PASSWORD", DateTime.UnixEpoch);
}
