using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Domain.Library;
using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FCG.IntegrationTests.Persistence;

public sealed class FcgDbContextTests
{
    [Fact]
    public void Context_MapsExactlyTheFourExpectedEntities()
    {
        using var context = CreateContext();

        var mapped = context.Model
            .GetEntityTypes()
            .Select(entity => entity.ClrType)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [typeof(Game), typeof(LibraryEntry), typeof(Promotion), typeof(User)],
            mapped);
    }

    private static FcgDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FcgDbContext>()
            .UseNpgsql("Host=localhost;Database=fcg_context_tests;Username=fcg;Password=not_used")
            .Options;

        return new FcgDbContext(options);
    }
}
