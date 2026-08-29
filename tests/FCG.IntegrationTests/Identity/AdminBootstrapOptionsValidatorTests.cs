using FCG.Infrastructure.Identity;
using Microsoft.Extensions.Options;

namespace FCG.IntegrationTests.Identity;

public sealed class AdminBootstrapOptionsValidatorTests
{
    private static readonly AdminBootstrapOptionsValidator Validator = new();

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Validate_WhenNeitherIsConfigured_Succeeds(string? email, string? password)
    {
        var result = Validate(email, password);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("admin@example.com", null)]
    [InlineData("admin@example.com", "")]
    [InlineData("admin@example.com", "   ")]
    [InlineData(null, "Str0ng!Pass")]
    [InlineData("", "Str0ng!Pass")]
    [InlineData("   ", "Str0ng!Pass")]
    public void Validate_WhenOnlyOneIsConfigured_Fails(string? email, string? password)
    {
        var result = Validate(email, password);

        Assert.True(result.Failed);
        Assert.Contains(
            AdminBootstrapOptions.SectionName,
            result.FailureMessage,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenBothAreConfiguredAndEmailIsValid_Succeeds()
    {
        var result = Validate("admin@example.com", "Str0ng!Pass");

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    public void Validate_WhenEmailIsInvalid_Fails(string email)
    {
        var result = Validate(email, "Str0ng!Pass");

        Assert.True(result.Failed);
        Assert.Contains("Email", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("weak")]
    [InlineData("onlyletters")]
    [InlineData("OnlyLetters1")]
    public void Validate_WhenPasswordViolatesTheSharedPolicy_Fails(string password)
    {
        var result = Validate("admin@example.com", password);

        Assert.True(result.Failed);
        Assert.Contains("Password", result.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(password, result.FailureMessage, StringComparison.Ordinal);
    }

    private static ValidateOptionsResult Validate(string? email, string? password) =>
        Validator.Validate(
            name: null,
            new AdminBootstrapOptions { Email = email, Password = password });
}
