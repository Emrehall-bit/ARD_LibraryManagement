namespace LibrarySystem.Modules.Borrowing.Application.Interfaces;

public interface IBorrowingClock
{
    DateTime UtcNow { get; }
}
