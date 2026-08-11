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
        if (!MailAddress.TryCreate(value, out var parsedEmail) || parsedEmail.Address != value)
        {
            throw new ArgumentException("Email format is invalid.", nameof(value));
        }

        return new Email(value);
    }
}
