namespace LibrarySystem.Shared.Borrowing;

public sealed record UserBorrowingSummary(
    string UserId,
    int ActiveBorrowCount,
    int OverdueBorrowCount);
