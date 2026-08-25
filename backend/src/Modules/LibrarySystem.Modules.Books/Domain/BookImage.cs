namespace LibrarySystem.Modules.Books.Domain;

public sealed class BookImage
{
    private BookImage()
    {
    }

    public BookImage(Guid id, Guid bookId, string objectName, bool isCover, int sortOrder)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Book image id cannot be empty.", nameof(id));
        }

        if (bookId == Guid.Empty)
        {
            throw new ArgumentException("Book id cannot be empty.", nameof(bookId));
        }

        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Book image object name cannot be empty.", nameof(objectName));
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Book image sort order cannot be negative.");
        }

        Id = id;
        BookId = bookId;
        ObjectName = objectName.Trim();
        IsCover = isCover;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid BookId { get; private set; }

    public string ObjectName { get; private set; } = string.Empty;

    public bool IsCover { get; private set; }

    public int SortOrder { get; private set; }

    public void SetCover(bool isCover)
    {
        IsCover = isCover;
    }
}
