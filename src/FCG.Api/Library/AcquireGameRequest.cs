using System.ComponentModel.DataAnnotations;

namespace FCG.Api.Library;

public sealed record AcquireGameRequest
{
    [Required]
    public Guid? GameId { get; init; }
}
