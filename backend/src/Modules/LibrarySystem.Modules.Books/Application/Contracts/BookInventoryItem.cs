namespace LibrarySystem.Modules.Books.Application.Contracts;

public sealed record BookInventoryItem(Guid BookId, int Stock);
