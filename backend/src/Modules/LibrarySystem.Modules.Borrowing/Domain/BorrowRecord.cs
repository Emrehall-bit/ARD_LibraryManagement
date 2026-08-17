namespace LibrarySystem.Modules.Borrowing.Domain;

public sealed class BorrowRecord
{
    private BorrowRecord()
    {
    }

    public BorrowRecord(Guid id, string userId, Guid bookId, DateTime borrowedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Borrow record id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Borrow record user id cannot be empty.", nameof(userId));
        }

        if (bookId == Guid.Empty)
        {
            throw new ArgumentException("Borrow record book id cannot be empty.", nameof(bookId));
        }

        if (borrowedAt == default)
        {
            throw new ArgumentException("Borrowed at must be specified.", nameof(borrowedAt));
        }

        Id = id;
        UserId = userId.Trim();
        BookId = bookId;
        BorrowedAt = borrowedAt;
        ReturnedAt = null;
    }

    public Guid Id { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public Guid BookId { get; private set; }

    public DateTime BorrowedAt { get; private set; }

    public DateTime? ReturnedAt { get; private set; }

    public void Return(DateTime returnedAt)
    {
        if (ReturnedAt is not null)
        {
            throw new InvalidOperationException("Borrow record has already been returned.");
        }

        if (returnedAt == default)
        {
            throw new ArgumentException("Returned at must be specified.", nameof(returnedAt));
        }

        if (returnedAt < BorrowedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(returnedAt), "Returned at cannot be earlier than borrowed at.");
        }

        ReturnedAt = returnedAt;
    }
}
