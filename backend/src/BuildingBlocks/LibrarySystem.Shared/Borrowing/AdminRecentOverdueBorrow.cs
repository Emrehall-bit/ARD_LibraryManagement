namespace LibrarySystem.Shared.Borrowing;

public sealed record AdminRecentOverdueBorrow(
    Guid Id,
    string UserId,
    Guid BookId,
    DateTime DueDate,
    int OverdueDays);
