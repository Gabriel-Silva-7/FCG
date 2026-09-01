using System.ComponentModel.DataAnnotations;
using FCG.Application.Catalog;
using FCG.Domain.Catalog;

namespace FCG.Api.Contracts;

public sealed record CreateGameRequest
{
    [Required]
    [StringLength(Game.MaxTitleLength, MinimumLength = 1)]
    public string? Title { get; init; }

    [StringLength(Game.MaxDescriptionLength)]
    public string? Description { get; init; }

    [Required]
    [Range(
        typeof(decimal),
        GamePriceLimits.MinimumBasePriceText,
        GamePriceLimits.MaximumSupportedBasePriceText,
        ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Base price must be between 0 and 9999999999999999.99.")]
    public decimal? BasePrice { get; init; }
}
