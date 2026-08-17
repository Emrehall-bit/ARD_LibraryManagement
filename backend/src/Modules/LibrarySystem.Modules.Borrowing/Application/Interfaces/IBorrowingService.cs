using LibrarySystem.Modules.Borrowing.Application.Dtos;

namespace LibrarySystem.Modules.Borrowing.Application.Interfaces;

public interface IBorrowingService
{
    Task<BorrowRecordResponseDto> BorrowBookAsync(
        Guid bookId,
        CancellationToken cancellationToken = default);

    Task<BorrowRecordResponseDto> ReturnBookAsync(
        Guid bookId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BorrowRecordResponseDto>> GetMyBooksAsync(
        CancellationToken cancellationToken = default);
}
