namespace LibrarySystem.Api.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiIntegrationTestCollection : ICollectionFixture<LibrarySystemApiFactory>
{
    public const string Name = "API integration tests";
}
