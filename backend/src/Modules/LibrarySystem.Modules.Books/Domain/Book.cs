namespace LibrarySystem.Modules.Books.Domain;

public sealed class Book
{
    private Book()
    {
    }

    public Book(Guid id, string name, string author, int stock)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Book id cannot be empty.", nameof(id));
        }

        Id = id;
        Update(name, author, stock);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Author { get; private set; } = string.Empty;

    public int Stock { get; private set; }

    public void Update(string name, string author, int stock)
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
}
