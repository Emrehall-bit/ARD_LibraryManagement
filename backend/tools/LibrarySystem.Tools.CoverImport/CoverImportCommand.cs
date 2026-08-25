using LibrarySystem.Modules.Books.Domain;
using LibrarySystem.Modules.Books.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Minio;
using Npgsql;

namespace LibrarySystem.Tools.CoverImport;

internal static class CoverImportCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = CoverImportOptions.Parse(args);
            var configuration = CoverImportConfiguration.Load();

            if (!options.DryRun)
            {
                configuration.RequireStorageConfiguration();
            }

            await using var dbContext = CreateDbContext(configuration.ConnectionString);
            await EnsureDevelopmentDatabaseAsync(dbContext);

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ARD-LibraryManagement-CoverImport/1.0 (development tooling)");
            var openLibraryClient = new OpenLibraryClient(httpClient);

            MinioCoverStorage? storage = null;
            if (!options.DryRun)
            {
                var minioClient = new MinioClient()
                    .WithEndpoint(configuration.MinioEndpoint)
                    .WithCredentials(configuration.MinioAccessKey, configuration.MinioSecretKey)
                    .WithSSL(configuration.MinioUseSsl)
                    .Build();

                storage = new MinioCoverStorage(minioClient, configuration.MinioBucketName);
                await storage.EnsureBucketAsync(CancellationToken.None);
            }

            var summary = await ImportAsync(dbContext, openLibraryClient, storage, options, CancellationToken.None);
            summary.Print();

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<CoverImportSummary> ImportAsync(
        BooksDbContext dbContext,
        OpenLibraryClient openLibraryClient,
        MinioCoverStorage? storage,
        CoverImportOptions options,
        CancellationToken cancellationToken)
    {
        IQueryable<Book> query = dbContext.Books
            .AsNoTracking()
            .OrderBy(book => book.Name)
            .ThenBy(book => book.Id);

        if (options.Limit.HasValue)
        {
            query = query.Take(options.Limit.Value);
        }

        var books = await query.ToListAsync(cancellationToken);
        var summary = new CoverImportSummary { TotalConsidered = books.Count };

        Console.WriteLine(options.DryRun
            ? $"Dry run cover import starting. Limit: {options.Limit?.ToString() ?? "all"}."
            : $"Cover import starting. Limit: {options.Limit?.ToString() ?? "all"}.");

        foreach (var book in books)
        {
            try
            {
                if (await HasCoverAsync(dbContext, book.Id, cancellationToken))
                {
                    summary.ExistingCoverSkipped++;
                    Console.WriteLine($"skip existing cover: {book.Name} / {book.Author}");
                    continue;
                }

                var coverReference = await openLibraryClient.FindCoverAsync(book, cancellationToken);
                if (coverReference is null)
                {
                    summary.NoMatch++;
                    Console.WriteLine($"no match: {book.Name} / {book.Author}");
                    continue;
                }

                summary.Matched++;
                Console.WriteLine($"matched: {book.Name} / {book.Author} -> {coverReference.DisplayName}");

                if (options.DryRun)
                {
                    continue;
                }

                var cover = await openLibraryClient.DownloadCoverAsync(coverReference, cancellationToken);
                if (cover is null)
                {
                    summary.NoCover++;
                    Console.WriteLine($"no cover: {book.Name} / {book.Author}");
                    continue;
                }

                var imageId = Guid.NewGuid();
                var objectName = $"books/{book.Id}/{imageId:N}{cover.Extension}";

                await storage!.UploadAsync(objectName, cover, cancellationToken);

                try
                {
                    if (await HasCoverAsync(dbContext, book.Id, cancellationToken))
                    {
                        summary.ExistingCoverSkipped++;
                        await storage.DeleteAsync(objectName, cancellationToken);
                        Console.WriteLine($"skip existing cover after upload race: {book.Name} / {book.Author}");
                        continue;
                    }

                    dbContext.BookImages.Add(new BookImage(imageId, book.Id, objectName, isCover: true, sortOrder: 0));
                    await dbContext.SaveChangesAsync(cancellationToken);
                    summary.Uploaded++;
                    Console.WriteLine($"uploaded: {book.Name} / {book.Author} -> {objectName}");
                }
                catch
                {
                    await TryDeleteUploadedObjectAsync(storage, objectName, cancellationToken);
                    throw;
                }
            }
            catch (Exception exception)
            {
                summary.Failed++;
                Console.WriteLine($"failed: {book.Name} / {book.Author}: {exception.Message}");
            }
        }

        return summary;
    }

    private static BooksDbContext CreateDbContext(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<BooksDbContext>()
            .UseNpgsql(connectionString);

        return new BooksDbContext(builder.Options);
    }

    private static async Task EnsureDevelopmentDatabaseAsync(BooksDbContext dbContext)
    {
        await dbContext.Database.OpenConnectionAsync();

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT current_database()";
        var currentDatabase = Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        if (string.Equals(currentDatabase, "library_system_tests", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to run cover import against library_system_tests.");
        }

        if (!string.Equals(currentDatabase, CoverImportConfiguration.ExpectedDevelopmentDatabase, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to run cover import against database '{currentDatabase}'. Expected '{CoverImportConfiguration.ExpectedDevelopmentDatabase}'.");
        }

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        var builder = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
        if (builder.Database is not null &&
            !string.Equals(builder.Database, CoverImportConfiguration.ExpectedDevelopmentDatabase, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Connection string database '{builder.Database}' is not allowed for cover import.");
        }
    }

    private static Task<bool> HasCoverAsync(
        BooksDbContext dbContext,
        Guid bookId,
        CancellationToken cancellationToken)
    {
        return dbContext.BookImages.AnyAsync(
            image => image.BookId == bookId && image.IsCover,
            cancellationToken);
    }

    private static async Task TryDeleteUploadedObjectAsync(
        MinioCoverStorage storage,
        string objectName,
        CancellationToken cancellationToken)
    {
        try
        {
            await storage.DeleteAsync(objectName, cancellationToken);
        }
        catch (Exception deleteException)
        {
            Console.WriteLine($"warning: uploaded object cleanup failed for {objectName}: {deleteException.Message}");
        }
    }
}
