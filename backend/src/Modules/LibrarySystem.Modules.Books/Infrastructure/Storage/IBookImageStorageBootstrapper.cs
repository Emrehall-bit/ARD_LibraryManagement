namespace LibrarySystem.Modules.Books.Infrastructure.Storage;

internal interface IBookImageStorageBootstrapper
{
    Task EnsureAsync(CancellationToken cancellationToken = default);
}
