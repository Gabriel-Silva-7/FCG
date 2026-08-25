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
