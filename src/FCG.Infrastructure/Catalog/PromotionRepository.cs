using FCG.Application.Catalog;
using FCG.Domain.Catalog;
using FCG.Infrastructure.Persistence;

namespace FCG.Infrastructure.Catalog;

public sealed class PromotionRepository(FcgDbContext dbContext) : IPromotionRepository
{
    public void Add(Promotion promotion) => dbContext.Promotions.Add(promotion);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
