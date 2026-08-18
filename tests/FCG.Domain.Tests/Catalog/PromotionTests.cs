using FCG.Domain.Catalog;

namespace FCG.Domain.Tests.Catalog;

public sealed class PromotionTests
{
    private static readonly Guid ValidGameId =
        Guid.Parse("5bc43011-d565-477d-95c7-c0d3ff009ea2");

    private static readonly Guid ValidCreatorId =
        Guid.Parse("d80b6477-c013-4366-86d6-7d25a0e98f69");

    private static readonly DateTime ValidStartsAtUtc = DateTime.UnixEpoch;
    private static readonly DateTime ValidEndsAtUtc = ValidStartsAtUtc.AddDays(7);

    public static TheoryData<decimal> InvalidDiscountPercentages =>
        new()
        {
            -0.01m,
            0m,
            100.01m,
        };

    public static TheoryData<decimal> BoundaryDiscountPercentages =>
        new()
        {
            0.01m,
            100m,
        };

    [Fact]
    public void Create_WhenDataIsValid_CreatesPromotion()
    {
        var promotion = CreatePromotion();

        Assert.NotEqual(Guid.Empty, promotion.Id);
        Assert.Equal(ValidGameId, promotion.GameId);
        Assert.Equal(15.50m, promotion.DiscountPercentage);
        Assert.Equal(ValidStartsAtUtc, promotion.StartsAtUtc);
        Assert.Equal(ValidEndsAtUtc, promotion.EndsAtUtc);
        Assert.Equal(ValidCreatorId, promotion.CreatedByUserId);
    }

    [Fact]
    public void Create_WhenGameIdentifierIsEmpty_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreatePromotion(gameId: Guid.Empty));

        Assert.Equal("gameId", exception.ParamName);
    }

    [Fact]
    public void Create_WhenCreatorIdentifierIsEmpty_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreatePromotion(createdByUserId: Guid.Empty));

        Assert.Equal("createdByUserId", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidDiscountPercentages))]
    public void Create_WhenDiscountPercentageIsOutsideRange_ThrowsArgumentOutOfRangeException(
        decimal discountPercentage)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CreatePromotion(discountPercentage: discountPercentage));

        Assert.Equal("discountPercentage", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(BoundaryDiscountPercentages))]
    public void Create_WhenDiscountPercentageIsAtValidBoundary_AcceptsPercentage(
        decimal discountPercentage)
    {
        var promotion = CreatePromotion(discountPercentage: discountPercentage);

        Assert.Equal(discountPercentage, promotion.DiscountPercentage);
    }

    [Fact]
    public void Create_WhenDiscountPercentageHasMoreThanTwoDecimalPlaces_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreatePromotion(discountPercentage: 10.001m));

        Assert.Equal("discountPercentage", exception.ParamName);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Create_WhenStartDateIsNotUtc_ThrowsArgumentException(DateTimeKind kind)
    {
        var startsAt = DateTime.SpecifyKind(ValidStartsAtUtc, kind);

        var exception = Assert.Throws<ArgumentException>(
            () => CreatePromotion(startsAtUtc: startsAt));

        Assert.Equal("startsAtUtc", exception.ParamName);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Create_WhenEndDateIsNotUtc_ThrowsArgumentException(DateTimeKind kind)
    {
        var endsAt = DateTime.SpecifyKind(ValidEndsAtUtc, kind);

        var exception = Assert.Throws<ArgumentException>(
            () => CreatePromotion(endsAtUtc: endsAt));

        Assert.Equal("endsAtUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WhenEndDateEqualsStartDate_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreatePromotion(endsAtUtc: ValidStartsAtUtc));

        Assert.Equal("endsAtUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WhenEndDatePrecedesStartDate_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreatePromotion(endsAtUtc: ValidStartsAtUtc.AddTicks(-1)));

        Assert.Equal("endsAtUtc", exception.ParamName);
    }

    [Fact]
    public void IsActiveAt_WhenInstantEqualsStart_ReturnsTrue()
    {
        var promotion = CreatePromotion();

        Assert.True(promotion.IsActiveAt(ValidStartsAtUtc));
    }

    [Fact]
    public void IsActiveAt_WhenInstantIsImmediatelyBeforeEnd_ReturnsTrue()
    {
        var promotion = CreatePromotion();

        Assert.True(promotion.IsActiveAt(ValidEndsAtUtc.AddTicks(-1)));
    }

    [Fact]
    public void IsActiveAt_WhenInstantEqualsEnd_ReturnsFalse()
    {
        var promotion = CreatePromotion();

        Assert.False(promotion.IsActiveAt(ValidEndsAtUtc));
    }

    [Fact]
    public void IsActiveAt_WhenInstantIsOutsideInterval_ReturnsFalse()
    {
        var promotion = CreatePromotion();

        Assert.False(promotion.IsActiveAt(ValidStartsAtUtc.AddTicks(-1)));
        Assert.False(promotion.IsActiveAt(ValidEndsAtUtc.AddTicks(1)));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void IsActiveAt_WhenInstantIsNotUtc_ThrowsArgumentException(DateTimeKind kind)
    {
        var promotion = CreatePromotion();
        var instant = DateTime.SpecifyKind(ValidStartsAtUtc, kind);

        var exception = Assert.Throws<ArgumentException>(() => promotion.IsActiveAt(instant));

        Assert.Equal("instantUtc", exception.ParamName);
    }

    [Fact]
    public void Create_WhenIntervalsOverlap_AllowsBothPromotions()
    {
        var first = CreatePromotion();
        var second = CreatePromotion(
            startsAtUtc: ValidStartsAtUtc.AddDays(1),
            endsAtUtc: ValidEndsAtUtc.AddDays(1));
        var overlappingInstant = ValidStartsAtUtc.AddDays(2);

        Assert.True(first.IsActiveAt(overlappingInstant));
        Assert.True(second.IsActiveAt(overlappingInstant));
    }

    [Fact]
    public void Promotion_HasNoPublicConstructor()
    {
        Assert.Empty(typeof(Promotion).GetConstructors());
    }

    [Fact]
    public void Promotion_PropertiesHaveNoPublicSetters()
    {
        Assert.All(
            typeof(Promotion).GetProperties(),
            property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    private static Promotion CreatePromotion(
        Guid? gameId = null,
        decimal discountPercentage = 15.50m,
        DateTime? startsAtUtc = null,
        DateTime? endsAtUtc = null,
        Guid? createdByUserId = null) =>
        Promotion.Create(
            gameId ?? ValidGameId,
            discountPercentage,
            startsAtUtc ?? ValidStartsAtUtc,
            endsAtUtc ?? ValidEndsAtUtc,
            createdByUserId ?? ValidCreatorId);
}
