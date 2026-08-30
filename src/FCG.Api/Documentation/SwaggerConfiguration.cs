using System.Reflection;
using Microsoft.OpenApi.Models;

namespace FCG.Api.Documentation;

public static class SwaggerConfiguration
{
    public const string BearerSchemeName = "Bearer";

    public const string DocumentName = "v1";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "FIAP Cloud Games API",
                Version = DocumentName,
                Description =
                    "API REST da Fase 1 do FIAP Cloud Games: identidade, catálogo de jogos, "
                    + "promoções e biblioteca do usuário. Erros seguem Problem Details "
                    + "(RFC 9457).",
            });

            var xmlPath = Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".xml");

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            options.AddSecurityDefinition(BearerSchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Informe apenas o token JWT, sem o prefixo Bearer.",
            });

            options.OperationFilter<BearerSecurityRequirementOperationFilter>();
            options.SchemaFilter<CreateGameRequestSchemaFilter>();
        });

        return services;
    }
}
