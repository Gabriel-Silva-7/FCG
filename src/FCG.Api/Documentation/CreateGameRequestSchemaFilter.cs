using FCG.Api.Contracts;
using FCG.Application.Catalog;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FCG.Api.Documentation;

internal sealed class CreateGameRequestSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(CreateGameRequest) ||
            !schema.Properties.TryGetValue("basePrice", out var basePrice))
        {
            return;
        }

        // Swashbuckle 6 não respeita a cultura invariante do Range ao gerar limites decimais.
        basePrice.Minimum = 0m;
        basePrice.Maximum = GamePriceLimits.MaximumSupportedBasePrice;
    }
}
