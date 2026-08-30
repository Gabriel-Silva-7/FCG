using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FCG.Api.Catalog;
using FCG.Application.Catalog;
using FCG.Domain.Catalog;

namespace FCG.Api.Tests.Catalog;

public sealed class CreateGameRequestTests
{
    [Fact]
    public void CompleteRequest_IsValid()
    {
        var request = new CreateGameRequest
        {
            Title = "Celeste",
            Description = "Precision platformer",
            BasePrice = 59.90m,
        };

        Assert.True(IsValid(request));
    }

    [Fact]
    public void ZeroBasePrice_IsValid()
    {
        var request = new CreateGameRequest
        {
            Title = "Free game",
            BasePrice = 0m,
        };

        Assert.True(IsValid(request));
    }

    [Fact]
    public void MissingBasePrice_IsInvalid()
    {
        var request = new CreateGameRequest { Title = "Celeste" };

        Assert.False(IsValid(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingTitle_IsInvalid(string? title)
    {
        var request = new CreateGameRequest
        {
            Title = title,
            BasePrice = 59.90m,
        };

        Assert.False(IsValid(request));
    }

    [Fact]
    public void OversizedFields_AreInvalid()
    {
        var request = new CreateGameRequest
        {
            Title = new string('a', Game.MaxTitleLength + 1),
            Description = new string('a', Game.MaxDescriptionLength + 1),
            BasePrice = 59.90m,
        };

        Assert.False(IsValid(request));
    }

    [Fact]
    public void BasePriceOutsideTheStorageCapacity_IsInvalid()
    {
        var request = new CreateGameRequest
        {
            Title = "Celeste",
            BasePrice = 10_000_000_000_000_000m,
        };

        Assert.False(IsValid(request));
    }

    [Fact]
    public void MaximumSupportedBasePrice_IsValid()
    {
        var request = new CreateGameRequest
        {
            Title = "Celeste",
            BasePrice = GamePriceLimits.MaximumSupportedBasePrice,
        };

        Assert.True(IsValid(request));
    }

    [Fact]
    public void PublishedPriceLimit_MatchesItsDecimalValue()
    {
        var publishedLimit = decimal.Parse(
            GamePriceLimits.MaximumSupportedBasePriceText,
            CultureInfo.InvariantCulture);

        Assert.Equal(GamePriceLimits.MaximumSupportedBasePrice, publishedLimit);
    }

    private static bool IsValid(CreateGameRequest request) =>
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            new List<ValidationResult>(),
            validateAllProperties: true);
}
