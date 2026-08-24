using System.Text.Json.Serialization;

namespace LibrarySystem.Api.Hubs;

public sealed record BookStockChangedMessage(
    [property: JsonPropertyName("bookId")] Guid BookId,
    [property: JsonPropertyName("stock")] int Stock);
