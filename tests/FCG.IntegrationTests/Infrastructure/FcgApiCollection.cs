namespace FCG.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class FcgApiCollection : ICollectionFixture<FcgApiFixture>
{
    public const string Name = "FCG API integration";
}
