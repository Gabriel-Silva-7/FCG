using FCG.Domain.Catalog;

namespace FCG.Domain.Tests.Catalog;

public sealed class PricingServiceTests
{
    private static readonly Guid GameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CreatorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime InstantUtc = DateTime.UnixEpoch.AddDays(10);

    [Fact]
    public void Calculate_WithoutActivePromotion_KeepsTheBasePrice()
    {
        var future = CreatePromotion(30m, InstantUtc.AddDays(1), InstantUtc.AddDays(2));
        var expired = CreatePromotion(40m, InstantUtc.AddDays(-2), InstantUtc);

        var result = PricingService.Calculate(59.90m, [future, expired], InstantUtc);

        Assert.Equal(59.90m, result.CurrentPrice);
        Assert.Equal(0m, result.DiscountPercentage);
    }

    [Fact]
    public void Calculate_WithOverlappingPromotions_UsesTheHighestActiveDiscount()
    {
        var lower = CreatePromotion(10m, InstantUtc.AddDays(-1), InstantUtc.AddDays(1));
        var higher = CreatePromotion(25m, InstantUtc.AddDays(-2), InstantUtc.AddDays(2));

        var result = PricingService.Calculate(59.90m, [lower, higher], InstantUtc);

        Assert.Equal(44.93m, result.CurrentPrice);
        Assert.Equal(25m, result.DiscountPercentage);
    }

    [Fact]
    public void Calculate_WithFullDiscount_ReturnsZero()
    {
        var result = PricingService.Calculate(59.90m, 100m);

        Assert.Equal(0m, result.CurrentPrice);
        Assert.Equal(100m, result.DiscountPercentage);
    }

    [Fact]
    public void Calculate_WhenResultIsHalfACent_RoundsAwayFromZero()
    {
        var result = PricingService.Calculate(0.05m, 50m);

        Assert.Equal(0.03m, result.CurrentPrice);
    }

    [Fact]
    public void Calculate_AtTheStartAndEnd_UsesTheSemiOpenInterval()
    {
        var promotion = CreatePromotion(20m, InstantUtc, InstantUtc.AddHours(1));

        var atStart = PricingService.Calculate(10m, [promotion], InstantUtc);
        var atEnd = PricingService.Calculate(10m, [promotion], InstantUtc.AddHours(1));

        Assert.Equal(8m, atStart.CurrentPrice);
        Assert.Equal(20m, atStart.DiscountPercentage);
        Assert.Equal(10m, atEnd.CurrentPrice);
        Assert.Equal(0m, atEnd.DiscountPercentage);
    }

    private static Promotion CreatePromotion(
        decimal discountPercentage,
        DateTime startsAtUtc,
        DateTime endsAtUtc) =>
        Promotion.Create(
            GameId,
            discountPercentage,
            startsAtUtc,
            endsAtUtc,
            CreatorId);
}
