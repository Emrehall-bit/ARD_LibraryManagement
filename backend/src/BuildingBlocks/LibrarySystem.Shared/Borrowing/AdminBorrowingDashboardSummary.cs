namespace LibrarySystem.Shared.Borrowing;

public sealed record AdminBorrowingDashboardSummary(
    int ActiveBorrows,
    int OverdueBorrows,
    int ReturnedBorrows,
    IReadOnlyList<AdminRecentOverdueBorrow> RecentOverdueBorrows);
