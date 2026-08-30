using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using FCG.Api.Catalog;
using FCG.Application.Catalog;
using FCG.Domain.Catalog;

namespace FCG.Api.Tests.Catalog;

public sealed class ListGamesRequestTests
{
    [Fact]
    public void Defaults_MatchThePublicCatalogContract()
    {
        var request = new ListGamesRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Null(request.Search);
        Assert.Null(request.SortBy);
        Assert.True(IsValid(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000001)]
    public void Page_OutsideTheSupportedRange_IsInvalid(int page)
    {
        Assert.False(IsValid(new ListGamesRequest { Page = page }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSize_OutsideTheSupportedRange_IsInvalid(int pageSize)
    {
        Assert.False(IsValid(new ListGamesRequest { PageSize = pageSize }));
    }

    [Theory]
    [InlineData(GameSortFields.Title)]
    [InlineData(GameSortFields.BasePrice)]
    [InlineData(GameSortFields.CreatedAt)]
    public void AllowListedSortField_IsValid(string sortBy)
    {
        Assert.True(IsValid(new ListGamesRequest { SortBy = sortBy }));
    }

    [Theory]
    [InlineData("Title")]
    [InlineData("description")]
    [InlineData("title desc")]
    public void NonAllowListedSortField_IsInvalid(string sortBy)
    {
        Assert.False(IsValid(new ListGamesRequest { SortBy = sortBy }));
    }

    [Fact]
    public void EveryDeclaredSortField_AgreesWithTheParserAndRequestPattern()
    {
        var declaredFields = typeof(GameSortFields)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field =>
                field.IsLiteral &&
                field.FieldType == typeof(string) &&
                field.Name != nameof(GameSortFields.Pattern))
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue()))
            .ToArray();

        Assert.NotEmpty(declaredFields);
        Assert.All(declaredFields, field =>
        {
            Assert.Matches(GameSortFields.Pattern, field);
            Assert.True(GameSortFields.TryParse(field, out _));
        });

        Assert.DoesNotMatch(GameSortFields.Pattern, "unknown");
        Assert.False(GameSortFields.TryParse("unknown", out _));
    }

    [Fact]
    public void SearchLongerThanATitle_IsInvalid()
    {
        var request = new ListGamesRequest
        {
            Search = new string('a', Game.MaxTitleLength + 1),
        };

        Assert.False(IsValid(request));
    }

    private static bool IsValid(ListGamesRequest request) =>
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            new List<ValidationResult>(),
            validateAllProperties: true);
}
