namespace LibrarySystem.Modules.Books.Domain;

public static class BookImagePolicy
{
    public const int MaxImagesPerBook = 5;
    public const long MaxImageSizeBytes = 5 * 1024 * 1024;

    public static readonly IReadOnlySet<string> SupportedContentTypes = new HashSet<string>(
        ["image/jpeg", "image/png", "image/webp"],
        StringComparer.OrdinalIgnoreCase);
}
