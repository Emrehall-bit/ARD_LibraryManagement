using System.Data;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using LibrarySystem.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

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
                IsolationLevel.Serializable,
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
            catch (Exception exception)
            {
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch (Exception rollbackException) when (IsPostgreSqlSerializationFailure(exception))
                {
                    throw new ConcurrencyConflictException(
                        "The borrow operation could not be completed because the book inventory or active borrow count was changed concurrently.",
                        new AggregateException(exception, rollbackException));
                }

                if (IsPostgreSqlSerializationFailure(exception))
                {
                    throw new ConcurrencyConflictException(
                        "The borrow operation could not be completed because the book inventory or active borrow count was changed concurrently.",
                        exception);
                }

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

    private static bool IsPostgreSqlSerializationFailure(Exception exception)
    {
        for (var currentException = exception; currentException is not null; currentException = currentException.InnerException)
        {
            if (currentException is PostgresException postgresException &&
                postgresException.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                return true;
            }
        }

        return false;
    }
}
