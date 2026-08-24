namespace LibrarySystem.Modules.Borrowing.Domain;

public sealed class BorrowRecord
{
    private BorrowRecord()
    {
    }

    public BorrowRecord(Guid id, string userId, Guid bookId, DateTime borrowedAt)
        : this(id, userId, bookId, borrowedAt, borrowedAt.AddDays(BorrowingLoanPolicy.DefaultLoanPeriodDays))
    {
    }

    public BorrowRecord(Guid id, string userId, Guid bookId, DateTime borrowedAt, DateTime dueDate)
        : this(id, userId, bookId, borrowedAt, dueDate, renewalCount: 0)
    {
    }

    public BorrowRecord(
        Guid id,
        string userId,
        Guid bookId,
        DateTime borrowedAt,
        DateTime dueDate,
        int renewalCount)
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

        if (dueDate == default)
        {
            throw new ArgumentException("Due date must be specified.", nameof(dueDate));
        }

        if (dueDate < borrowedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(dueDate), "Due date cannot be earlier than borrowed at.");
        }

        if (renewalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(renewalCount), "Renewal count cannot be negative.");
        }

        Id = id;
        UserId = userId.Trim();
        BookId = bookId;
        BorrowedAt = borrowedAt;
        DueDate = dueDate;
        RenewalCount = renewalCount;
        ReturnedAt = null;
    }

    public Guid Id { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public Guid BookId { get; private set; }

    public DateTime BorrowedAt { get; private set; }

    public DateTime DueDate { get; private set; }

    public DateTime? ReturnedAt { get; private set; }

    public int RenewalCount { get; private set; }

    public BorrowStatus GetStatus(DateTime utcNow)
    {
        if (ReturnedAt is not null)
        {
            return BorrowStatus.Returned;
        }

        return DueDate < utcNow
            ? BorrowStatus.Overdue
            : BorrowStatus.Borrowed;
    }

    public void Renew(DateTime utcNow)
    {
        if (ReturnedAt is not null)
        {
            throw new InvalidOperationException("Returned borrow records cannot be renewed.");
        }

        if (GetStatus(utcNow) == BorrowStatus.Overdue)
        {
            throw new InvalidOperationException("Overdue borrow records cannot be renewed.");
        }

        if (RenewalCount >= BorrowingLoanPolicy.MaxRenewalCount)
        {
            throw new InvalidOperationException("The borrow record has already been renewed.");
        }

        DueDate = DueDate.AddDays(BorrowingLoanPolicy.RenewalPeriodDays);
        RenewalCount++;
    }

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
