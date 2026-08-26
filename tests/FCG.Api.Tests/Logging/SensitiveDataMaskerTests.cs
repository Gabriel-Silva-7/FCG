using FCG.Api.Logging;

namespace FCG.Api.Tests.Logging;

public sealed class SensitiveDataMaskerTests
{
    [Theory]
    [InlineData("gabriel@example.com", "g***@example.com")]
    [InlineData("biel14022002@gmail.com", "b***@gmail.com")]
    [InlineData("ab@x.io", "a***@x.io")]
    public void MaskEmail_KeepsOnlyTheFirstCharacterOfTheLocalPart(string email, string expected)
    {
        Assert.Equal(expected, SensitiveDataMasker.MaskEmail(email));
    }

    [Fact]
    public void MaskEmail_HidesASingleCharacterLocalPartEntirely()
    {
        Assert.Equal("***@gmail.com", SensitiveDataMasker.MaskEmail("b@gmail.com"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sem-arroba")]
    [InlineData("@example.com")]
    [InlineData("gabriel@")]
    [InlineData("a@b@c.com")]
    public void MaskEmail_NeverEchoesInputThatIsNotAnEmail(string? value)
    {
        Assert.Equal("***", SensitiveDataMasker.MaskEmail(value));
    }
}
