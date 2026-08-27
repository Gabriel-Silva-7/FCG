using System.ComponentModel.DataAnnotations;
using FCG.Domain.Identity;

namespace FCG.Api.Identity;

public sealed record RegisterRequest(
    [param: Required]
    [param: StringLength(User.MaxNameLength, MinimumLength = 1)]
    string Name,
    [param: Required]
    [param: StringLength(Email.MaxLength)]
    string Email,
    [param: Required]
    [param: StringLength(PasswordPolicy.MaximumLength, MinimumLength = PasswordPolicy.MinimumLength)]
    string Password);
