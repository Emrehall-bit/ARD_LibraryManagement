namespace LibrarySystem.Modules.Identity.Application.Contracts;

public interface IUserDirectory
{
    Task<IReadOnlyList<UserDirectoryItem>> GetByIdsAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default);
}
