namespace LibrarySystem.Modules.Identity.Application.Contracts;

public interface IUserDirectory
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserDirectoryItem>> GetByIdsAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default);
}
