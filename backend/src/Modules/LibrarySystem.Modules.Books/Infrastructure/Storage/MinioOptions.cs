namespace LibrarySystem.Modules.Books.Infrastructure.Storage;

public sealed class MinioOptions
{
    public const string SectionName = "Minio";

    public string? Endpoint { get; init; }

    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }

    public string? BucketName { get; init; }

    public bool UseSsl { get; init; }

    internal bool HasAnyConfiguredValue()
    {
        return !string.IsNullOrWhiteSpace(Endpoint) ||
            !string.IsNullOrWhiteSpace(AccessKey) ||
            !string.IsNullOrWhiteSpace(SecretKey) ||
            !string.IsNullOrWhiteSpace(BucketName);
    }

    internal bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(Endpoint) &&
            !string.IsNullOrWhiteSpace(AccessKey) &&
            !string.IsNullOrWhiteSpace(SecretKey) &&
            !string.IsNullOrWhiteSpace(BucketName);
    }
}
