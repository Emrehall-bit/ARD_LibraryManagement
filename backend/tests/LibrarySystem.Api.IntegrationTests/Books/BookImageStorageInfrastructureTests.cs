using LibrarySystem.Modules.Books.Application.Contracts;
using LibrarySystem.Modules.Books.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LibrarySystem.Api.IntegrationTests.Books;

public sealed class BookImageStorageInfrastructureTests
{
    [Fact]
    public async Task AddBookImageStorage_WithoutMinioConfiguration_RegistersDisabledStorage()
    {
        var configuration = CreateConfiguration([]);
        using var services = CreateServices(configuration);
        var storage = services.GetRequiredService<IBookImageStorage>();

        var exception = await Assert.ThrowsAsync<ObjectStorageException>(() =>
            storage.GetReadUrlAsync(
                "books/book-id/cover.webp",
                BookImageStorageDefaults.DefaultReadUrlExpiry));

        Assert.Contains("not configured", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddBookImageStorage_WithPartialMinioConfiguration_FailsOptionsValidation()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Minio:Endpoint"] = "localhost:9000"
        });
        using var services = CreateServices(configuration);

        Assert.Throws<OptionsValidationException>(() =>
            _ = services.GetRequiredService<IOptions<MinioOptions>>().Value);
    }

    [Fact]
    public void AddBookImageStorage_WithCompleteMinioConfiguration_RegistersStorageAbstraction()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Minio:Endpoint"] = "localhost:9000",
            ["Minio:AccessKey"] = "minioadmin",
            ["Minio:SecretKey"] = "local-development-secret",
            ["Minio:BucketName"] = "library-books",
            ["Minio:UseSsl"] = "false"
        });
        using var services = CreateServices(configuration);

        var storage = services.GetRequiredService<IBookImageStorage>();

        Assert.NotNull(storage);
    }

    [Fact]
    public async Task BookImageStorageAbstraction_CanBeReplacedByFakeForDependentServices()
    {
        var fakeStorage = new FakeBookImageStorage();
        using var services = new ServiceCollection()
            .AddSingleton<IBookImageStorage>(fakeStorage)
            .AddSingleton<BookImageUrlConsumer>()
            .BuildServiceProvider();
        var consumer = services.GetRequiredService<BookImageUrlConsumer>();

        var url = await consumer.CreateReadUrlAsync("books/book-id/cover.webp");

        Assert.Equal("https://storage.example.test/books/book-id/cover.webp", url);
        Assert.Equal("books/book-id/cover.webp", fakeStorage.LastObjectName);
        Assert.Equal(BookImageStorageDefaults.DefaultReadUrlExpiry, fakeStorage.LastExpiry);
    }

    private static ServiceProvider CreateServices(IConfiguration configuration)
    {
        return new ServiceCollection()
            .AddLogging()
            .AddBookImageStorage(configuration)
            .BuildServiceProvider();
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class BookImageUrlConsumer(IBookImageStorage storage)
    {
        public Task<string> CreateReadUrlAsync(string objectName)
        {
            return storage.GetReadUrlAsync(objectName, BookImageStorageDefaults.DefaultReadUrlExpiry);
        }
    }

    private sealed class FakeBookImageStorage : IBookImageStorage
    {
        public string? LastObjectName { get; private set; }

        public TimeSpan? LastExpiry { get; private set; }

        public Task UploadAsync(
            string objectName,
            Stream stream,
            string contentType,
            long size,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            string objectName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> GetReadUrlAsync(
            string objectName,
            TimeSpan expiry,
            CancellationToken cancellationToken = default)
        {
            LastObjectName = objectName;
            LastExpiry = expiry;

            return Task.FromResult($"https://storage.example.test/{objectName}");
        }
    }
}
