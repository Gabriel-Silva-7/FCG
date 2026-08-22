namespace FCG.IntegrationTests.Infrastructure;

// Herde daqui em todo teste que ESCREVE no banco. O xUnit instancia a classe de teste uma
// vez por método, então o reset abaixo roda antes de cada teste e evita que dados de um
// vazem para o outro — o banco é um só, compartilhado por toda a collection.
[Collection(FcgApiCollection.Name)]
public abstract class DatabaseBackedTest(FcgApiFixture fixture) : IAsyncLifetime
{
    protected FcgApiFixture Fixture { get; } = fixture;

    public Task InitializeAsync() => Fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
