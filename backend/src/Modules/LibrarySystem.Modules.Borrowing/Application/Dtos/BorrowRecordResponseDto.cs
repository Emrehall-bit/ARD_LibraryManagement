namespace LibrarySystem.Modules.Borrowing.Application.Dtos;

public sealed record BorrowRecordResponseDto(
    Guid Id,
    Guid BookId,
    string? BookName,
    string? Author,
    DateTime BorrowedAt,
    DateTime DueDate,
    DateTime? ReturnedAt,
    string Status);
