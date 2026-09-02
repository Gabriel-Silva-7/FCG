using System.ComponentModel.DataAnnotations;
using FCG.Domain.Identity;

namespace FCG.Api.Contracts;

public sealed record UpdateCurrentUserRequest
{
    [Required]
    [StringLength(User.MaxNameLength, MinimumLength = 1)]
    public string? Name { get; init; }

    [Required]
    [StringLength(FCG.Domain.Identity.Email.MaxLength)]
    public string? Email { get; init; }
}
