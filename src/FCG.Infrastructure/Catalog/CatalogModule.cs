using FCG.Application.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Infrastructure.Catalog;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<CreateGameHandler>();
        services.AddScoped<ListGamesHandler>();
        services.AddScoped<GetGameHandler>();

        return services;
    }
}
