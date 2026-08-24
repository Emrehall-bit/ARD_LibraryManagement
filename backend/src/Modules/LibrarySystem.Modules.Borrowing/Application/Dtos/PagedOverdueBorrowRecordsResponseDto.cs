namespace LibrarySystem.Modules.Borrowing.Application.Dtos;

public sealed record PagedOverdueBorrowRecordsResponseDto(
    IReadOnlyList<OverdueBorrowRecordResponseDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
