using FCG.Domain.Identity;

namespace FCG.Domain.Tests.Identity;

public sealed class EmailTests
{
    [Fact]
    public void Create_NormalizesWhitespaceAndCasing()
    {
        var email = Email.Create("  Gabriel@Example.COM  ");

        Assert.Equal("gabriel@example.com", email.Value);
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
