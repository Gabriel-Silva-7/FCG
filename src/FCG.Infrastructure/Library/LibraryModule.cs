using FCG.Application.Library;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Infrastructure.Library;

public static class LibraryModule
{
    public static IServiceCollection AddLibraryModule(this IServiceCollection services)
    {
        services.AddScoped<ILibraryRepository, LibraryRepository>();
        services.AddScoped<AcquireGameHandler>();
        services.AddScoped<GetMyLibraryHandler>();

        return services;
    }
}
