using System.Data;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace LibrarySystem.Modules.Borrowing.Infrastructure.Transactions;

internal sealed class BorrowingTransactionCoordinator(
    BooksDbContext booksDbContext,
    BorrowingDbContext borrowingDbContext) : IBorrowingTransactionCoordinator
{
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        var executionStrategy = booksDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            var connection = booksDbContext.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            borrowingDbContext.Database.SetDbConnection(connection, contextOwnsConnection: false);

            await using var transaction = await booksDbContext.Database.BeginTransactionAsync(
                IsolationLevel.RepeatableRead,
                cancellationToken);

            await borrowingDbContext.Database.UseTransactionAsync(
                transaction.GetDbTransaction(),
                cancellationToken);

            try
            {
                var result = await operation(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                await borrowingDbContext.Database.UseTransactionAsync(null, cancellationToken);

                if (shouldCloseConnection)
                {
                    await connection.CloseAsync();
                }
            }
        });
    }
}
