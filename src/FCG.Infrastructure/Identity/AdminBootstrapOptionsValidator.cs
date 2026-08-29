using FCG.Domain.Identity;
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

        if (hasEmail && !Email.TryCreate(options.Email, out _))
        {
            return ValidateOptionsResult.Fail(
                $"{AdminBootstrapOptions.SectionName}:Email não é um e-mail válido.");
        }

        if (hasPassword)
        {
            try
            {
                PasswordPolicy.EnsureIsValid(options.Password!);
            }
            catch (ArgumentException exception)
            {
                return ValidateOptionsResult.Fail(
                    $"{AdminBootstrapOptions.SectionName}:Password não atende à política. " +
                    exception.Message);
            }
        }

        return ValidateOptionsResult.Success;
    }
}
