using FCG.Api.Errors;

namespace FCG.Api.Tests.Errors;

public sealed class ApiErrorTests
{
    [Theory]
    [InlineData("email_already_registered", "urn:fcg:error:email-already-registered")]
    [InlineData("concurrency_conflict", "urn:fcg:error:concurrency-conflict")]
    [InlineData("validation_error", "urn:fcg:error:validation-error")]
    [InlineData("forbidden", "urn:fcg:error:forbidden")]
    public void Type_IsUrnDerivedFromCode(string code, string expectedType)
    {
        var error = new ApiError(code, 409, "Some title");

        Assert.Equal(expectedType, error.Type);
    }

    [Fact]
    public void Type_DoesNotPointToTheGenericHttpSpecification()
    {
        var error = new ApiError("email_already_registered", 409, "Email already registered");

        Assert.DoesNotContain("rfc", error.Type, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ietf", error.Type, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("about:blank", error.Type, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToProblemDetails_CopiesStatusTypeAndTitleWithoutDiverging()
    {
        var error = new ApiError("email_already_registered", 409, "Email already registered");

        var problem = error.ToProblemDetails("/api/v1/auth/register");

        Assert.Equal(409, problem.Status);
        Assert.Equal("urn:fcg:error:email-already-registered", problem.Type);
        Assert.Equal("Email already registered", problem.Title);
        Assert.Equal("/api/v1/auth/register", problem.Instance);
    }

    [Fact]
    public void ToProblemDetails_CarriesCodeAsExtension()
    {
        var error = new ApiError("game_already_acquired", 409, "Game already acquired");

        var problem = error.ToProblemDetails("/api/v1/me/library");

        Assert.Equal("game_already_acquired", Assert.Contains("code", problem.Extensions));
    }

    [Fact]
    public void ToProblemDetails_OmitsDetailByDefault()
    {
        var error = new ApiError("internal_error", 500, "Unexpected error");

        var problem = error.ToProblemDetails("/api/v1/games");

        Assert.Null(problem.Detail);
    }

    [Fact]
    public void ToProblemDetails_IncludesDetailOnlyWhenExplicitlyProvided()
    {
        var error = new ApiError("validation_error", 400, "Validation failed");

        var problem = error.ToProblemDetails("/api/v1/auth/register", "A senha é obrigatória.");

        Assert.Equal("A senha é obrigatória.", problem.Detail);
    }
}
