using System.Reflection;
using FCG.Api.Errors;

namespace FCG.Api.Tests.Errors;

public sealed class ApiErrorsCatalogTests
{
    private static readonly int[] AllowedStatuses = [400, 401, 403, 404, 409, 429, 500];

    public static TheoryData<ApiError> EveryCatalogEntry
    {
        get
        {
            var data = new TheoryData<ApiError>();

            foreach (var error in ApiErrors.All)
            {
                data.Add(error);
            }

            return data;
        }
    }

    [Theory]
    [InlineData("validation_error", 400)]
    [InlineData("unauthenticated", 401)]
    [InlineData("invalid_credentials", 401)]
    [InlineData("forbidden", 403)]
    [InlineData("resource_not_found", 404)]
    [InlineData("email_already_registered", 409)]
    [InlineData("invalid_current_password", 400)]
    [InlineData("concurrency_conflict", 409)]
    [InlineData("cannot_deactivate_self", 409)]
    [InlineData("cannot_delete_self", 409)]
    [InlineData("user_has_dependencies", 409)]
    [InlineData("game_already_acquired", 409)]
    [InlineData("rate_limit_exceeded", 429)]
    [InlineData("internal_error", 500)]
    public void TryGetByCode_ResolvesEveryPublishedCodeWithItsStatus(string code, int expectedStatus)
    {
        var found = ApiErrors.TryGetByCode(code, out var error);

        Assert.True(found);
        Assert.NotNull(error);
        Assert.Equal(expectedStatus, error.Status);
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public void Catalog_HasExactlyThePublishedNumberOfEntries()
    {
        Assert.Equal(14, ApiErrors.All.Count);
    }

    [Theory]
    [InlineData("does_not_exist")]
    [InlineData("")]
    [InlineData(null)]
    public void TryGetByCode_WhenCodeIsUnknown_ReturnsFalse(string? code)
    {
        var found = ApiErrors.TryGetByCode(code, out var error);

        Assert.False(found);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(400, "validation_error")]
    [InlineData(401, "unauthenticated")]
    [InlineData(403, "forbidden")]
    [InlineData(404, "resource_not_found")]
    [InlineData(429, "rate_limit_exceeded")]
    [InlineData(500, "internal_error")]
    public void ForStatus_ReturnsTheCuratedDefault(int status, string expectedCode)
    {
        var error = ApiErrors.ForStatus(status);

        Assert.Equal(expectedCode, error.Code);
        Assert.Equal(status, error.Status);
    }

    [Theory]
    [InlineData(409)]
    [InlineData(405)]
    [InlineData(415)]
    [InlineData(503)]
    public void ForStatus_WhenStatusHasNoCatalogEntry_PreservesTheHttpStatus(int status)
    {
        var error = ApiErrors.ForStatus(status);

        Assert.Equal($"http_{status}", error.Code);
        Assert.Equal(status, error.Status);
        Assert.Equal($"urn:fcg:error:http-{status}", error.Type);
    }

    [Theory]
    [InlineData(399)]
    [InlineData(600)]
    public void ForStatus_WhenStatusIsNotAnError_Throws(int status)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ApiErrors.ForStatus(status));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(429)]
    [InlineData(500)]
    public void Catalog_CoversEveryRequiredStatus(int status)
    {
        Assert.Contains(ApiErrors.All, error => error.Status == status);
    }

    [Theory]
    [MemberData(nameof(EveryCatalogEntry))]
    public void EveryEntry_HasSnakeCaseCode(ApiError error)
    {
        Assert.Matches("^[a-z][a-z0-9_]*$", error.Code);
    }

    [Theory]
    [MemberData(nameof(EveryCatalogEntry))]
    public void EveryEntry_HasNonEmptyTitle(ApiError error)
    {
        Assert.False(string.IsNullOrWhiteSpace(error.Title));
    }

    [Theory]
    [MemberData(nameof(EveryCatalogEntry))]
    public void EveryEntry_UsesAnAllowedStatus(ApiError error)
    {
        Assert.Contains(error.Status, AllowedStatuses);
    }

    [Theory]
    [MemberData(nameof(EveryCatalogEntry))]
    public void EveryEntry_HasProjectOwnedUrnType(ApiError error)
    {
        Assert.StartsWith("urn:fcg:error:", error.Type, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryDeclaredErrorFieldIsRegisteredInAll()
    {
        var declared = typeof(ApiErrors)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(ApiError))
            .Select(field => (Name: field.Name, Error: (ApiError)field.GetValue(null)!))
            .ToArray();

        Assert.NotEmpty(declared);

        var unregistered = declared
            .Where(entry => !ApiErrors.All.Any(registered => ReferenceEquals(registered, entry.Error)))
            .Select(entry => entry.Name)
            .ToArray();

        Assert.True(
            unregistered.Length == 0,
            "Campos de ApiError ausentes em ApiErrors.All: " + string.Join(", ", unregistered));
    }
}
