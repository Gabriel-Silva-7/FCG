using System.ComponentModel.DataAnnotations;

namespace FCG.Api.Contracts;

public sealed record CreatePromotionRequest
{
    /// <summary>
    /// Percentage discount greater than zero and at most 100.
    /// </summary>
    [Required]
    public decimal? DiscountPercentage { get; init; }

    [Required]
    public DateTime? StartsAt { get; init; }

    [Required]
    public DateTime? EndsAt { get; init; }
}
