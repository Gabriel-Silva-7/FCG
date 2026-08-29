using System.ComponentModel.DataAnnotations;
using FCG.Api.Identity;

namespace FCG.Api.Tests.Identity;

public sealed class ListUsersRequestTests
{
    // Skip((page - 1) * pageSize) é aritmética de int: sem teto em Page, esses valores viram
    // offset negativo e o PostgreSQL responde 500 a uma entrada que o contrato declara válida.
    [Theory]
    [InlineData(2147483647)]
    [InlineData(30000000)]
    [InlineData(1000001)]
    [InlineData(0)]
    public void Page_OutsideTheSupportedRange_IsInvalid(int page)
    {
        Assert.False(IsValid(new ListUsersRequest { Page = page }));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(1000000)]
    public void Page_WithinTheSupportedRange_IsValid(int page)
    {
        Assert.True(IsValid(new ListUsersRequest { Page = page }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSize_OutsideTheSupportedRange_IsInvalid(int pageSize)
    {
        Assert.False(IsValid(new ListUsersRequest { PageSize = pageSize }));
    }

    [Fact]
    public void Defaults_MatchTheDocumentedPaginationContract()
    {
        var request = new ListUsersRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Null(request.Search);
    }

    private static bool IsValid(ListUsersRequest request) =>
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            new List<ValidationResult>(),
            validateAllProperties: true);
}
