using LibrarySystem.Modules.Books.Application.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace LibrarySystem.Modules.Books.Infrastructure.Storage;

internal sealed class MinioBookImageStorageBootstrapper(
    IMinioClient minioClient,
    IOptions<MinioOptions> minioOptions,
    ILogger<MinioBookImageStorageBootstrapper> logger) : IBookImageStorageBootstrapper
{
    private readonly MinioOptions options = minioOptions.Value;

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (!options.IsConfigured())
        {
            return;
        }

        try
        {
            var existsArgs = new BucketExistsArgs()
                .WithBucket(options.BucketName);

            if (await minioClient.BucketExistsAsync(existsArgs, cancellationToken))
            {
                logger.LogInformation("MinIO bucket '{BucketName}' already exists.", options.BucketName);
                return;
            }

            var makeBucketArgs = new MakeBucketArgs()
                .WithBucket(options.BucketName);

            await minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);
            logger.LogInformation("MinIO bucket '{BucketName}' was created.", options.BucketName);
        }
        catch (MinioException exception)
        {
            throw new ObjectStorageException("Failed to ensure MinIO bucket exists.", exception);
        }
    }
}
