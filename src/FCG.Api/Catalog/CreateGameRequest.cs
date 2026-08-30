using System.ComponentModel.DataAnnotations;
using FCG.Application.Catalog;
using FCG.Domain.Catalog;

namespace FCG.Api.Catalog;

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
        ParseLimitsInInvariantCulture = true)]
    public decimal? BasePrice { get; init; }
}
