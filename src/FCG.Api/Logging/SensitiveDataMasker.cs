namespace FCG.Api.Logging;

public static class SensitiveDataMasker
{
    public const string FullyMasked = "***";

    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return FullyMasked;
        }

        var separatorIndex = email.IndexOf('@', StringComparison.Ordinal);

        if (separatorIndex <= 0 ||
            separatorIndex != email.LastIndexOf('@') ||
            separatorIndex == email.Length - 1)
        {
            return FullyMasked;
        }

        var domain = email[separatorIndex..];

        return separatorIndex == 1
            ? FullyMasked + domain
            : email[0] + FullyMasked + domain;
    }
}
