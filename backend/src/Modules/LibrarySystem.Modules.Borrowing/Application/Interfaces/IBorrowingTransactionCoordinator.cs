namespace LibrarySystem.Modules.Borrowing.Application.Interfaces;

public interface IBorrowingTransactionCoordinator
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
