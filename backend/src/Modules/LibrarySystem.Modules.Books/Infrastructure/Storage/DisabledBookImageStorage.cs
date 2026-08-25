using LibrarySystem.Modules.Books.Application.Contracts;

namespace LibrarySystem.Modules.Books.Infrastructure.Storage;

internal sealed class DisabledBookImageStorage : IBookImageStorage
{
    public Task UploadAsync(
        string objectName,
        Stream stream,
        string contentType,
        long size,
        CancellationToken cancellationToken = default)
    {
        throw CreateNotConfiguredException();
    }

    public Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        throw CreateNotConfiguredException();
    }

    public Task<string> GetReadUrlAsync(
        string objectName,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        throw CreateNotConfiguredException();
    }

    private static ObjectStorageException CreateNotConfiguredException()
    {
        return new ObjectStorageException(
            "Book image storage is not configured. Configure the Minio section before using image storage operations.");
    }
}
