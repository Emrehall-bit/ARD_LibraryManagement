using Microsoft.Extensions.Options;

namespace LibrarySystem.Modules.Books.Infrastructure.Storage;

internal sealed class MinioOptionsValidator : IValidateOptions<MinioOptions>
{
    public ValidateOptionsResult Validate(string? name, MinioOptions options)
    {
        if (!options.HasAnyConfiguredValue())
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            failures.Add("Minio:Endpoint is required when MinIO storage is configured.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey))
        {
            failures.Add("Minio:AccessKey is required when MinIO storage is configured.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            failures.Add("Minio:SecretKey is required when MinIO storage is configured.");
        }

        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            failures.Add("Minio:BucketName is required when MinIO storage is configured.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
