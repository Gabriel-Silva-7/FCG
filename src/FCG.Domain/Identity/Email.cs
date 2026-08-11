using System.Net.Mail;

namespace FCG.Domain.Identity;

public sealed record Email
{
    public const int MaxLength = 256;

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Email Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var normalizedValue = value.Trim().ToLowerInvariant();

        if (normalizedValue.Length > MaxLength ||
            !MailAddress.TryCreate(normalizedValue, out var parsedEmail) ||
            parsedEmail.Address != normalizedValue)
        {
            throw new ArgumentException("Email format is invalid.", nameof(value));
        }

        return new Email(normalizedValue);
    }

    public override string ToString() => Value;
}
