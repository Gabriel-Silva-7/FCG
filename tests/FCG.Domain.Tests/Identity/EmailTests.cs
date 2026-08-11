using FCG.Domain.Identity;

namespace FCG.Domain.Tests.Identity;

public sealed class EmailTests
{
    [Fact]
    public void Create_WhenValueIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Email.Create(null!));
    }

    [Fact]
    public void Create_WhenValueExceeds256Characters_ThrowsArgumentException()
    {
        var localPart = new string('a', 64);
        var domain = string.Join('.', new string('b', 48), new string('c', 48), new string('d', 48), new string('e', 45));
        var value = $"{localPart}@{domain}";

        Assert.Equal(257, value.Length);
        Assert.Throws<ArgumentException>(() => Email.Create(value));
    }

    [Fact]
    public void Create_NormalizesWhitespaceAndCasing()
    {
        var email = Email.Create("  Gabriel@Example.COM  ");

        Assert.Equal("gabriel@example.com", email.Value);
    }

    [Fact]
    public void Emails_WithSameNormalizedValue_AreEqual()
    {
        var first = Email.Create("User@Example.com");
        var second = Email.Create("user@example.com");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ToString_ReturnsNormalizedValue()
    {
        var email = Email.Create("User@Example.com");

        Assert.Equal("user@example.com", email.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local-part.com")]
    public void Create_WhenFormatIsInvalid_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => Email.Create(value));
    }
}
