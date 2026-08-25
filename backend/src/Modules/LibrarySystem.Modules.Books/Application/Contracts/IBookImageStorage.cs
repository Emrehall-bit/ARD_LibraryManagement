namespace LibrarySystem.Modules.Books.Application.Contracts;

public interface IBookImageStorage
{
    Task UploadAsync(
        string objectName,
        Stream stream,
        string contentType,
        long size,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken = default);

    Task<string> GetReadUrlAsync(
        string objectName,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);
}
