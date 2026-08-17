namespace LibrarySystem.Modules.Borrowing.Application.Dtos;

public sealed record BorrowRecordResponseDto(
    Guid Id,
    Guid BookId,
    DateTime BorrowedAt,
    DateTime? ReturnedAt);
