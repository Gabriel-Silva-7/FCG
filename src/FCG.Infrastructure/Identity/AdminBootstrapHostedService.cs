using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FCG.Infrastructure.Identity;

public sealed class AdminBootstrapHostedService(
    IHostEnvironment environment,
    IOptions<AdminBootstrapOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<AdminBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        var bootstrapOptions = options.Value;

        if (string.IsNullOrWhiteSpace(bootstrapOptions.Email) &&
            string.IsNullOrWhiteSpace(bootstrapOptions.Password))
        {
            logger.LogWarning(
                "AdminBootstrapSkipped {Reason}",
                "MissingConfiguration");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<AdminBootstrapService>();

        try
        {
            var result = await bootstrap.ExecuteAsync(
                bootstrapOptions.Email!,
                bootstrapOptions.Password!,
                cancellationToken);

            logger.LogInformation("AdminBootstrapCompleted {Result}", result);
        }
        catch (AdminBootstrapConflictException)
        {
            logger.LogError(
                "AdminBootstrapFailed {Reason}",
                "ConflictingAccount");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
