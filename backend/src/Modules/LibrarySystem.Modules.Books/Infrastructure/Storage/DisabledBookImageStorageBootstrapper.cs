namespace LibrarySystem.Modules.Books.Infrastructure.Storage;

internal sealed class DisabledBookImageStorageBootstrapper : IBookImageStorageBootstrapper
{
    public Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
