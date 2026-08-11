using System.Net.Mail;

namespace FCG.Domain.Identity;

public sealed class Email : IEquatable<Email>
{
    private const int MaximumLength = 256;

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Email Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var normalizedValue = value.Trim().ToLowerInvariant();

        if (normalizedValue.Length > MaximumLength ||
            !MailAddress.TryCreate(normalizedValue, out var parsedEmail) ||
            parsedEmail.Address != normalizedValue)
        {
            throw new ArgumentException("Email format is invalid.", nameof(value));
        }

        return new Email(normalizedValue);
    }

    public bool Equals(Email? other) =>
        other is not null && StringComparer.Ordinal.Equals(Value, other.Value);

    public override bool Equals(object? obj) => obj is Email other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
