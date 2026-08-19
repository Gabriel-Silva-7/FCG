using FCG.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FCG.IntegrationTests.Persistence;

internal static class FcgDbContextModel
{
    private static readonly IModel Model = CreateModel();

    public static IEntityType Entity<TEntity>()
    {
        return Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not mapped.");
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<FcgDbContext>()
            .UseNpgsql("Host=localhost;Database=fcg_model_tests;Username=fcg;Password=not_used")
            .Options;

        using var context = new FcgDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
