using System.Net.Mail;

namespace FCG.Domain.Identity;

public sealed class Email
{
    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Email Create(string value)
    {
        var normalizedValue = value.Trim().ToLowerInvariant();

        if (!MailAddress.TryCreate(normalizedValue, out var parsedEmail) ||
            parsedEmail.Address != normalizedValue)
        {
            throw new ArgumentException("Email format is invalid.", nameof(value));
        }

        return new Email(normalizedValue);
    }
}
