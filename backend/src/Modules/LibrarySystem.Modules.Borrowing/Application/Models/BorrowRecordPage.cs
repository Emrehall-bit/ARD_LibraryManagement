using LibrarySystem.Modules.Borrowing.Domain;

namespace LibrarySystem.Modules.Borrowing.Application.Models;

public sealed record BorrowRecordPage(
    IReadOnlyList<BorrowRecord> Items,
    int Page,
    int PageSize,
    int TotalCount);
