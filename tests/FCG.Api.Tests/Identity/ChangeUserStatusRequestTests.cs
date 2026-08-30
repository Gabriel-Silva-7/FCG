using System.ComponentModel.DataAnnotations;
using System.Globalization;
using FCG.Api.Identity;

namespace FCG.Api.Tests.Identity;

public sealed class ChangeUserStatusRequestTests
{
    [Fact]
    public void VersionLengthLimit_MatchesTheLargestXminRepresentation()
    {
        var maximumXmin = uint.MaxValue.ToString(CultureInfo.InvariantCulture);

        Assert.Equal(ChangeUserStatusRequest.XminVersionMaxLength, maximumXmin.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitStatusAndVersion_AreValid(bool isActive)
    {
        var request = new ChangeUserStatusRequest
        {
            IsActive = isActive,
            Version = "42",
        };

        Assert.True(IsValid(request));
    }

    [Fact]
    public void MissingStatus_IsInvalid()
    {
        var request = new ChangeUserStatusRequest { Version = "42" };

        Assert.False(IsValid(request));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678901")]
    public void MissingOrOversizedVersion_IsInvalid(string? version)
    {
        var request = new ChangeUserStatusRequest
        {
            IsActive = false,
            Version = version,
        };

        Assert.False(IsValid(request));
    }

    private static bool IsValid(ChangeUserStatusRequest request) =>
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            new List<ValidationResult>(),
            validateAllProperties: true);
}
