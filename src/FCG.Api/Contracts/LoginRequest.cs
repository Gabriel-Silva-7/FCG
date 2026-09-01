using System.ComponentModel.DataAnnotations;
using FCG.Domain.Identity;

namespace FCG.Api.Contracts;

public sealed record LoginRequest(
    [param: Required]
    [param: StringLength(Email.MaxLength)]
    string Email,
    [param: Required]
    [param: StringLength(PasswordPolicy.MaximumLength)]
    string Password);
