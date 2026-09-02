using System.ComponentModel.DataAnnotations;
using FCG.Domain.Identity;

namespace FCG.Api.Contracts;

public sealed record ChangeOwnPasswordRequest
{
    [Required]
    [StringLength(PasswordPolicy.MaximumLength)]
    public string? CurrentPassword { get; init; }

    [Required]
    [StringLength(
        PasswordPolicy.MaximumLength,
        MinimumLength = PasswordPolicy.MinimumLength)]
    public string? NewPassword { get; init; }
}
