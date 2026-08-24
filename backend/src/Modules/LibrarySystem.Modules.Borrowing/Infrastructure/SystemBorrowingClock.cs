using LibrarySystem.Modules.Borrowing.Application.Interfaces;

namespace LibrarySystem.Modules.Borrowing.Infrastructure;

internal sealed class SystemBorrowingClock : IBorrowingClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
