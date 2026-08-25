using LibrarySystem.Modules.Books.Application.Contracts;

namespace LibrarySystem.Api.IntegrationTests.Infrastructure;

public sealed class TestBookImageStorage : IBookImageStorage
{
    private readonly List<BookImageUpload> uploads = [];
    private readonly List<string> deletes = [];

    public IReadOnlyList<BookImageUpload> Uploads => uploads;

    public IReadOnlyList<string> Deletes => deletes;

    public bool ThrowOnDelete { get; set; }

    public Task UploadAsync(
        string objectName,
        Stream stream,
        string contentType,
        long size,
        CancellationToken cancellationToken = default)
    {
        uploads.Add(new BookImageUpload(objectName, contentType, size));

        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        deletes.Add(objectName);

        if (ThrowOnDelete)
        {
            throw new ObjectStorageException("Storage delete failed in test.");
        }

        return Task.CompletedTask;
    }

    public Task<string> GetReadUrlAsync(
        string objectName,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"https://storage.example.test/{Uri.EscapeDataString(objectName)}?expires={expiry.TotalSeconds:0}");
    }

    public void Reset()
    {
        uploads.Clear();
        deletes.Clear();
        ThrowOnDelete = false;
    }
}

public sealed record BookImageUpload(
    string ObjectName,
    string ContentType,
    long Size);
