using FCG.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FCG.IntegrationTests.Infrastructure;

public sealed class FcgApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("fcg_integration_tests")
        .WithUsername("fcg")
        .WithPassword("fcg-integration-tests-password")
        .WithAutoRemove(true)
        .WithCleanUp(true)
        .Build();

    private FcgWebApplicationFactory? _factory;

    public WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("A factory de integração ainda não foi inicializada.");

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Não foi possível iniciar o PostgreSQL de integração. " +
                "Verifique se o Docker está instalado e em execução.",
                exception);
        }

        try
        {
            _factory = new FcgWebApplicationFactory(ConnectionString);

            await using var scope = _factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();

            await dbContext.Database.MigrateAsync();
        }
        catch
        {
            try
            {
                _factory?.Dispose();
            }
            finally
            {
                await _postgres.DisposeAsync();
            }

            throw;
        }
    }

    // Descobre as tabelas em tempo de execução para que uma tabela nova criada por uma
    // migration futura seja limpa sem ninguém precisar lembrar de atualizar esta lista.
    // __EFMigrationsHistory fica de fora: apagá-la faria o EF tentar reaplicar tudo.
    private const string TruncateAllTablesSql =
        """
        DO $$
        DECLARE tables text;
        BEGIN
            SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
            INTO tables
            FROM pg_tables
            WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory';

            IF tables IS NOT NULL THEN
                EXECUTE format('TRUNCATE TABLE %s RESTART IDENTITY CASCADE', tables);
            END IF;
        END $$;
        """;

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FcgDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(TruncateAllTablesSql);
    }

    public async Task DisposeAsync()
    {
        try
        {
            _factory?.Dispose();
        }
        finally
        {
            await _postgres.DisposeAsync();
        }
    }
}
