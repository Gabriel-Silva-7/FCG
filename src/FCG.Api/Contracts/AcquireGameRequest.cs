using System.ComponentModel.DataAnnotations;

namespace FCG.Api.Contracts;

public sealed record AcquireGameRequest
{
    [Required]
    public Guid? GameId { get; init; }
}
