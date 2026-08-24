namespace LibrarySystem.Shared.Borrowing;

public interface IUserBorrowingSummaryService
{
    Task<IReadOnlyList<UserBorrowingSummary>> GetByUserIdsAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default);
}
