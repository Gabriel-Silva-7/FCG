using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace FCG.Infrastructure.Identity;

public sealed class AdminBootstrapOptionsValidator : IValidateOptions<AdminBootstrapOptions>
{
    public ValidateOptionsResult Validate(string? name, AdminBootstrapOptions options)
    {
        var hasEmail = !string.IsNullOrWhiteSpace(options.Email);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);

        if (hasEmail != hasPassword)
        {
            return ValidateOptionsResult.Fail(
                $"{AdminBootstrapOptions.SectionName}: informe Email e Password juntos, ou nenhum dos dois. " +
                "Sem ambos, o bootstrap do administrador simplesmente não é executado.");
        }

        if (hasEmail && !new EmailAddressAttribute().IsValid(options.Email))
        {
            return ValidateOptionsResult.Fail(
                $"{AdminBootstrapOptions.SectionName}:Email não é um e-mail válido.");
        }

        return ValidateOptionsResult.Success;
    }
}
