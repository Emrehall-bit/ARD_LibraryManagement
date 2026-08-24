namespace LibrarySystem.Modules.Borrowing.Application.Dtos;

public sealed record OverdueBorrowRecordResponseDto(
    Guid Id,
    string UserId,
    string Username,
    Guid BookId,
    string? BookName,
    string? Author,
    DateTime BorrowedAt,
    DateTime DueDate,
    int OverdueDays,
    int RenewalCount,
    string Status);
