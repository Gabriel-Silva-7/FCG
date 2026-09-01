using System.ComponentModel.DataAnnotations;

namespace FCG.Api.Contracts;

public sealed record ChangeUserStatusRequest
{
    public const int XminVersionMaxLength = 10;

    [Required]
    public bool? IsActive { get; init; }

    [Required]
    [StringLength(XminVersionMaxLength, MinimumLength = 1)]
    public string? Version { get; init; }
}
