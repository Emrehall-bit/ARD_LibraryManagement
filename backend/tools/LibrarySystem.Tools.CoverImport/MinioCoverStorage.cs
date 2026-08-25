using Minio;
using Minio.DataModel.Args;

namespace LibrarySystem.Tools.CoverImport;

internal sealed class MinioCoverStorage(IMinioClient minioClient, string bucketName)
{
    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var existsArgs = new BucketExistsArgs()
            .WithBucket(bucketName);

        if (await minioClient.BucketExistsAsync(existsArgs, cancellationToken))
        {
            return;
        }

        var makeBucketArgs = new MakeBucketArgs()
            .WithBucket(bucketName);

        await minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);
    }

    public async Task UploadAsync(
        string objectName,
        DownloadedCover cover,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(cover.Bytes);
        var args = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(cover.Bytes.LongLength)
            .WithContentType(cover.ContentType);

        await minioClient.PutObjectAsync(args, cancellationToken);
    }

    public async Task DeleteAsync(string objectName, CancellationToken cancellationToken)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName);

        await minioClient.RemoveObjectAsync(args, cancellationToken);
    }
}
