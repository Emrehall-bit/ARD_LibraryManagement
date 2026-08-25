namespace LibrarySystem.Modules.Books.Domain;

public sealed class Book
{
    private Book()
    {
    }

    public Book(Guid id, string name, string author, int stock)
        : this(id, name, author, stock, BookCategory.Other)
    {
    }

    public Book(Guid id, string name, string author, int stock, BookCategory category)
        : this(id, name, author, stock, category, null, null, null, null)
    {
    }

    public Book(
        Guid id,
        string name,
        string author,
        int stock,
        BookCategory category,
        string? description,
        string? isbn,
        string? publisher,
        int? publishedYear)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Book id cannot be empty.", nameof(id));
        }

        Id = id;
        Update(name, author, stock, category, description, isbn, publisher, publishedYear);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Author { get; private set; } = string.Empty;

    public int Stock { get; private set; }

    public BookCategory Category { get; private set; } = BookCategory.Other;

    public string? Description { get; private set; }

    public string? Isbn { get; private set; }

    public string? Publisher { get; private set; }

    public int? PublishedYear { get; private set; }

    public List<BookImage> Images { get; private set; } = [];

    public void Update(string name, string author, int stock)
    {
        Update(name, author, stock, Category);
    }

    public void Update(string name, string author, int stock, BookCategory category)
    {
        Update(name, author, stock, category, Description, Isbn, Publisher, PublishedYear);
    }

    public void Update(
        string name,
        string author,
        int stock,
        BookCategory category,
        string? description,
        string? isbn,
        string? publisher,
        int? publishedYear)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Book name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(author))
        {
            throw new ArgumentException("Book author cannot be empty.", nameof(author));
        }

        if (stock < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stock), "Book stock cannot be negative.");
        }

        Name = name.Trim();
        Author = author.Trim();
        Stock = stock;
        Category = category;
        Description = NormalizeOptionalText(description);
        Isbn = NormalizeOptionalText(isbn);
        Publisher = NormalizeOptionalText(publisher);
        PublishedYear = publishedYear;
    }

    public void DecreaseStock()
    {
        if (Stock <= 0)
        {
            throw new InvalidOperationException("Book stock cannot be decreased below zero.");
        }

        Stock--;
    }

    public void IncreaseStock()
    {
        Stock++;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
