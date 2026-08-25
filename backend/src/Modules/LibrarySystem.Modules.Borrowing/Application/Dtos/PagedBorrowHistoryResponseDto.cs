namespace LibrarySystem.Modules.Borrowing.Application.Dtos;

public sealed record PagedBorrowHistoryResponseDto(
    IReadOnlyList<BorrowRecordResponseDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
