using FCG.Domain.Catalog;

namespace FCG.Application.Catalog;

public interface IPromotionRepository
{
    void Add(Promotion promotion);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
