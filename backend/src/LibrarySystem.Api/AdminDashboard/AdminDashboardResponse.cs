namespace LibrarySystem.Api.AdminDashboard;

public sealed record AdminDashboardResponse(
    int TotalUsers,
    int TotalBooks,
    int TotalStock,
    int OutOfStockBooks,
    int ActiveBorrows,
    int OverdueBorrows,
    int ReturnedBorrows,
    IReadOnlyList<RecentOverdueBorrowResponse> RecentOverdueBorrows);
