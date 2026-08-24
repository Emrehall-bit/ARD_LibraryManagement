namespace LibrarySystem.Shared.Borrowing;

public interface IAdminBorrowingDashboardSummaryService
{
    Task<AdminBorrowingDashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
