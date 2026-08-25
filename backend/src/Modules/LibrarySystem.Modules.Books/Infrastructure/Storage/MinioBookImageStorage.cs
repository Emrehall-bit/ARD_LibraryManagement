using LibrarySystem.Modules.Books.Application.Contracts;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace LibrarySystem.Modules.Books.Infrastructure.Storage;

internal sealed class MinioBookImageStorage(
    IMinioClient minioClient,
    IOptions<MinioOptions> minioOptions) : IBookImageStorage
{
    private readonly MinioOptions options = RequireConfiguredOptions(minioOptions.Value);

    public async Task UploadAsync(
        string objectName,
        Stream stream,
        string contentType,
        long size,
        CancellationToken cancellationToken = default)
    {
        ValidateObjectName(objectName);

        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanRead)
        {
            throw new ArgumentException("Upload stream must be readable.", nameof(stream));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type cannot be empty.", nameof(contentType));
        }

        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Object size cannot be negative.");
        }

        try
        {
            var args = new PutObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(size)
                .WithContentType(contentType);

            await minioClient.PutObjectAsync(args, cancellationToken);
        }
        catch (MinioException exception)
        {
            throw new ObjectStorageException($"Failed to upload object '{objectName}'.", exception);
        }
    }

    public async Task DeleteAsync(
        string objectName,
        CancellationToken cancellationToken = default)
    {
        ValidateObjectName(objectName);

        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectName);

            await minioClient.RemoveObjectAsync(args, cancellationToken);
        }
        catch (MinioException exception)
        {
            throw new ObjectStorageException($"Failed to delete object '{objectName}'.", exception);
        }
    }

    public async Task<string> GetReadUrlAsync(
        string objectName,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        ValidateObjectName(objectName);
        cancellationToken.ThrowIfCancellationRequested();

        if (expiry <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expiry), "Read URL expiry must be positive.");
        }

        try
        {
            var args = new PresignedGetObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectName)
                .WithExpiry((int)Math.Ceiling(expiry.TotalSeconds));

            return await minioClient.PresignedGetObjectAsync(args);
        }
        catch (MinioException exception)
        {
            throw new ObjectStorageException($"Failed to create read URL for object '{objectName}'.", exception);
        }
    }

    private static void ValidateObjectName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Object name cannot be empty.", nameof(objectName));
        }
    }

    private static MinioOptions RequireConfiguredOptions(MinioOptions options)
    {
        return options.IsConfigured()
            ? options
            : throw new ObjectStorageException(
                "Book image storage is not configured. Configure the Minio section before using image storage operations.");
    }
}
