using System.Net;
using FCG.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace FCG.IntegrationTests.Documentation;

[Collection(FcgApiCollection.Name)]
public sealed class OpenApiDocumentTests(FcgApiFixture fixture)
{
    private const string DocumentName = "v1";
    private const string SchemeName = "Bearer";

    [Fact]
    public void Document_DeclaresBearerSchemeUsableWithARawToken()
    {
        var document = GetDocument();

        Assert.True(document.Components.SecuritySchemes.ContainsKey(SchemeName));

        var scheme = document.Components.SecuritySchemes[SchemeName];

        Assert.Equal(SecuritySchemeType.Http, scheme.Type);
        Assert.Equal("bearer", scheme.Scheme);
        Assert.Equal("JWT", scheme.BearerFormat);
        Assert.Equal(ParameterLocation.Header, scheme.In);
    }

    [Fact]
    public void Document_AppliesBearerOnlyToProtectedOperations()
    {
        var document = GetDocument();

        var protectedOperation = document.Paths["/_test/documentation/protected"]
            .Operations[OperationType.Get];
        var publicOperation = document.Paths["/_test/documentation/public"]
            .Operations[OperationType.Get];

        Assert.Contains(
            SchemeName,
            protectedOperation.Security
                .SelectMany(requirement => requirement.Keys)
                .Select(scheme => scheme.Reference?.Id));
        Assert.Empty(publicOperation.Security);
        Assert.Empty(document.SecurityRequirements);
    }

    [Fact]
    public void ApiAssembly_ShipsTheXmlDocumentationFileNextToIt()
    {
        var apiAssembly = typeof(FCG.Api.Errors.ApiError).Assembly;
        var xmlPath = Path.ChangeExtension(apiAssembly.Location, ".xml");

        Assert.True(
            File.Exists(xmlPath),
            $"Documentação XML ausente em '{xmlPath}'. Sem ela IncludeXmlComments não faz nada.");
    }

    [Fact]
    public void Document_IdentifiesTheProjectRatherThanTheAssembly()
    {
        var document = GetDocument();

        Assert.Equal("FIAP Cloud Games API", document.Info.Title);
        Assert.Equal("v1", document.Info.Version);
        Assert.False(string.IsNullOrWhiteSpace(document.Info.Description));
    }

    [Fact]
    public void RegisterOperation_DocumentsItsPublicContract()
    {
        var document = GetDocument();
        var operation = document.Paths["/api/v1/auth/register"]
            .Operations[OperationType.Post];

        Assert.Equal("Registers a new user account.", operation.Summary);
        Assert.Empty(operation.Security);
        Assert.Equal(
            ["201", "400", "409", "429"],
            operation.Responses.Keys.Order(StringComparer.Ordinal));

        var requestSchema = operation.RequestBody.Content["application/json"].Schema;
        var schema = document.Components.Schemas[requestSchema.Reference.Id];

        Assert.Contains("name", schema.Properties.Keys);
        Assert.Contains("email", schema.Properties.Keys);
        Assert.Contains("password", schema.Properties.Keys);
        Assert.DoesNotContain("role", schema.Properties.Keys);
        Assert.DoesNotContain("isActive", schema.Properties.Keys);
        Assert.DoesNotContain("passwordHash", schema.Properties.Keys);

        var responseSchema = operation.Responses["201"].Content["application/json"].Schema;
        var response = document.Components.Schemas[responseSchema.Reference.Id];

        Assert.Contains("id", response.Properties.Keys);
        Assert.Contains("name", response.Properties.Keys);
        Assert.Contains("email", response.Properties.Keys);
        Assert.Contains("role", response.Properties.Keys);
        Assert.DoesNotContain("password", response.Properties.Keys);
        Assert.DoesNotContain("passwordHash", response.Properties.Keys);
    }

    [Fact]
    public void LoginOperation_DocumentsItsPublicContract()
    {
        var document = GetDocument();
        var operation = document.Paths["/api/v1/auth/login"]
            .Operations[OperationType.Post];

        Assert.Equal("Authenticates a user and issues an access token.", operation.Summary);
        Assert.Empty(operation.Security);
        Assert.Equal(
            ["200", "400", "401", "429"],
            operation.Responses.Keys.Order(StringComparer.Ordinal));

        var requestReference = operation.RequestBody.Content["application/json"].Schema.Reference.Id;
        var request = document.Components.Schemas[requestReference];
        Assert.Contains("email", request.Properties.Keys);
        Assert.Contains("password", request.Properties.Keys);
        Assert.DoesNotContain("role", request.Properties.Keys);

        var responseReference = operation.Responses["200"].Content["application/json"].Schema.Reference.Id;
        var response = document.Components.Schemas[responseReference];
        Assert.Contains("accessToken", response.Properties.Keys);
        Assert.Contains("tokenType", response.Properties.Keys);
        Assert.Contains("expiresIn", response.Properties.Keys);
        Assert.DoesNotContain("refreshToken", response.Properties.Keys);
    }

    [Fact]
    public void MeOperation_DerivesIdentityFromBearerWithoutExternalParameters()
    {
        var document = GetDocument();
        var operation = document.Paths["/api/v1/me"]
            .Operations[OperationType.Get];

        Assert.Equal("Returns the profile of the authenticated user.", operation.Summary);
        Assert.Empty(operation.Parameters);
        Assert.Equal(
            ["200", "401"],
            operation.Responses.Keys.Order(StringComparer.Ordinal));
        Assert.Contains(
            SchemeName,
            operation.Security
                .SelectMany(requirement => requirement.Keys)
                .Select(scheme => scheme.Reference?.Id));

        var responseReference = operation.Responses["200"].Content["application/json"].Schema.Reference.Id;
        var response = document.Components.Schemas[responseReference];
        Assert.Contains("id", response.Properties.Keys);
        Assert.Contains("name", response.Properties.Keys);
        Assert.Contains("email", response.Properties.Keys);
        Assert.Contains("role", response.Properties.Keys);
        Assert.DoesNotContain("passwordHash", response.Properties.Keys);
        Assert.DoesNotContain("isActive", response.Properties.Keys);
    }

    [Fact]
    public void AdminUsersOperation_DocumentsItsPagedAdminOnlyContract()
    {
        var document = GetDocument();
        var operation = document.Paths["/api/v1/admin/users"]
            .Operations[OperationType.Get];

        Assert.Equal(
            ["200", "400", "401", "403"],
            operation.Responses.Keys.Order(StringComparer.Ordinal));
        Assert.Contains(
            SchemeName,
            operation.Security
                .SelectMany(requirement => requirement.Keys)
                .Select(scheme => scheme.Reference?.Id));

        var parameters = operation.Parameters.Select(parameter => parameter.Name).ToArray();
        Assert.Contains("page", parameters);
        Assert.Contains("pageSize", parameters);
        Assert.Contains("search", parameters);

        var responseReference = operation.Responses["200"].Content["application/json"].Schema.Reference.Id;
        var response = document.Components.Schemas[responseReference];
        Assert.Equal(
            ["items", "page", "pageSize", "totalCount"],
            response.Properties.Keys.Order(StringComparer.Ordinal));

        var itemReference = response.Properties["items"].Items.Reference.Id;
        var item = document.Components.Schemas[itemReference];
        Assert.Contains("version", item.Properties.Keys);
        Assert.DoesNotContain("passwordHash", item.Properties.Keys);
    }

    [Fact]
    public async Task SwaggerEndpoints_AreAvailableInDevelopment()
    {
        using var factory = fixture.Factory.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Development));
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

        using var documentResponse = await client.GetAsync("/swagger/v1/swagger.json");
        using var uiResponse = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, documentResponse.StatusCode);
        Assert.Equal("application/json", documentResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
        Assert.Equal("text/html", uiResponse.Content.Headers.ContentType?.MediaType);
    }

    private OpenApiDocument GetDocument() =>
        fixture.Factory.Services
            .GetRequiredService<ISwaggerProvider>()
            .GetSwagger(DocumentName);
}
