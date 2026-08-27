using System.Text.Json;

namespace LibrarySystem.Tools.CoverImport;

internal sealed record CoverImportConfiguration(
    string ConnectionString,
    string? MinioEndpoint,
    string? MinioAccessKey,
    string? MinioSecretKey,
    string MinioBucketName,
    bool MinioUseSsl)
{
    public const string ExpectedDevelopmentDatabase = "library_system";

    public static CoverImportConfiguration Load()
    {
        var repoRoot = TryFindRepoRoot();
        var values = repoRoot is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : LoadDotEnv(Path.Combine(repoRoot.FullName, ".env"));

        AddEnvironmentVariables(values);

        var connectionString = Get(values, "ConnectionStrings__LibrarySystemDatabase", "CONNECTIONSTRINGS__LIBRARYSYSTEMDATABASE")
            ?? (repoRoot is null ? null : ReadConnectionStringFromAppSettings(repoRoot))
            ?? throw new InvalidOperationException(
                "Connection string not found. Set ConnectionStrings__LibrarySystemDatabase or configure backend/src/LibrarySystem.Api/appsettings.json.");

        var endpoint = Get(values, "Minio__Endpoint", "MINIO_ENDPOINT", "MINIO__ENDPOINT");
        var accessKey = Get(values, "Minio__AccessKey", "MINIO_ACCESS_KEY", "MINIO_ROOT_USER", "MINIO__ACCESSKEY");
        var secretKey = Get(values, "Minio__SecretKey", "MINIO_SECRET_KEY", "MINIO_ROOT_PASSWORD", "MINIO__SECRETKEY");
        var bucketName = Get(values, "Minio__BucketName", "MINIO_BUCKET_NAME", "MINIO__BUCKETNAME") ?? "library-books";
        var useSsl = bool.TryParse(Get(values, "Minio__UseSsl", "MINIO_USE_SSL", "MINIO__USESSL"), out var parsedUseSsl) && parsedUseSsl;

        return new CoverImportConfiguration(
            connectionString,
            endpoint,
            accessKey,
            secretKey,
            bucketName,
            useSsl);
    }

    public void RequireStorageConfiguration()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(MinioEndpoint))
        {
            missing.Add("Minio__Endpoint or MINIO_ENDPOINT");
        }

        if (string.IsNullOrWhiteSpace(MinioAccessKey))
        {
            missing.Add("Minio__AccessKey, MINIO_ACCESS_KEY, or MINIO_ROOT_USER");
        }

        if (string.IsNullOrWhiteSpace(MinioSecretKey))
        {
            missing.Add("Minio__SecretKey, MINIO_SECRET_KEY, or MINIO_ROOT_PASSWORD");
        }

        if (string.IsNullOrWhiteSpace(MinioBucketName))
        {
            missing.Add("Minio__BucketName or MINIO_BUCKET_NAME");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"MinIO configuration is incomplete. Missing: {string.Join(", ", missing)}.");
        }
    }

    private static DirectoryInfo? TryFindRepoRoot()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "backend", "LibrarySystem.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private static Dictionary<string, string> LoadDotEnv(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(path))
        {
            return values;
        }

        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"').Trim('\'');
            values[key] = value;
        }

        return values;
    }

    private static void AddEnvironmentVariables(Dictionary<string, string> values)
    {
        foreach (var key in new[]
        {
            "ConnectionStrings__LibrarySystemDatabase",
            "CONNECTIONSTRINGS__LIBRARYSYSTEMDATABASE",
            "Minio__Endpoint",
            "Minio__AccessKey",
            "Minio__SecretKey",
            "Minio__BucketName",
            "Minio__UseSsl",
            "MINIO_ENDPOINT",
            "MINIO_ACCESS_KEY",
            "MINIO_SECRET_KEY",
            "MINIO_BUCKET_NAME",
            "MINIO_USE_SSL",
            "MINIO_ROOT_USER",
            "MINIO_ROOT_PASSWORD",
            "MINIO__ENDPOINT",
            "MINIO__ACCESSKEY",
            "MINIO__SECRETKEY",
            "MINIO__BUCKETNAME",
            "MINIO__USESSL"
        })
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[key] = value;
            }
        }
    }

    private static string? ReadConnectionStringFromAppSettings(DirectoryInfo repoRoot)
    {
        var path = Path.Combine(repoRoot.FullName, "backend", "src", "LibrarySystem.Api", "appsettings.json");

        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);

        return document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("LibrarySystemDatabase")
            .GetString();
    }

    private static string? Get(Dictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
