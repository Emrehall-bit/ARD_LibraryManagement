namespace LibrarySystem.Api.AdminDashboard;

public sealed record RecentOverdueBorrowResponse(
    Guid Id,
    string UserId,
    string Username,
    Guid BookId,
    string? BookName,
    string? Author,
    DateTime DueDate,
    int OverdueDays);
