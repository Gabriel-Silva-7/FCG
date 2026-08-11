using FCG.Domain.Identity;

namespace FCG.Domain.Tests.Identity;

public sealed class EmailTests
{
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
