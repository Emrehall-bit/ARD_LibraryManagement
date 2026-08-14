using System.Text.Json;
using LibrarySystem.Modules.Books.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace LibrarySystem.Api.IntegrationTests.Infrastructure;

public sealed class LibrarySystemApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestDatabaseName = "library_system_tests";

    private readonly string connectionString = CreateTestConnectionString();

    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    public async Task InitializeAsync()
    {
        await EnsureTestDatabaseExistsAsync();

        using var scope = Services.CreateScope();
        var booksDbContext = scope.ServiceProvider.GetRequiredService<BooksDbContext>();

        await booksDbContext.Database.MigrateAsync();
        await ResetBooksDataAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await ResetBooksDataAsync();
    }

    public async Task ResetBooksDataAsync()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE books.books;";

        await command.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:LibrarySystemDatabase"] = connectionString,
                ["Logging:EventLog:LogLevel:Default"] = "None"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BooksDbContext>>();
            services.RemoveAll<BooksDbContext>();

            services.AddDbContext<BooksDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(BooksDbContext).Assembly.FullName);
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "books");
                }));
        });
    }

    private async Task EnsureTestDatabaseExistsAsync()
    {
        var testConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = testConnectionStringBuilder.Database;

        testConnectionStringBuilder.Database = "postgres";

        await using var connection = new NpgsqlConnection(testConnectionStringBuilder.ConnectionString);
        await connection.OpenAsync();

        await using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @databaseName;";
            existsCommand.Parameters.AddWithValue("databaseName", databaseName!);

            if (await existsCommand.ExecuteScalarAsync() is not null)
            {
                return;
            }
        }

        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE {QuoteIdentifier(databaseName!)};";

        await createCommand.ExecuteNonQueryAsync();
    }

    private static string CreateTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "LibrarySystemIntegrationTests__ConnectionString");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = ReadDevelopmentConnectionString();

            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Database = TestDatabaseName
            };

            connectionString = builder.ConnectionString;
        }

        EnsureSafeTestDatabase(connectionString);

        return connectionString;
    }

    private static string ReadDevelopmentConnectionString()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appSettingsPath = Path.Combine(repositoryRoot, "src", "LibrarySystem.Api", "appsettings.json");

        using var appSettings = JsonDocument.Parse(File.ReadAllText(appSettingsPath));

        return appSettings.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("LibrarySystemDatabase")
            .GetString()
            ?? throw new InvalidOperationException(
                "Connection string 'LibrarySystemDatabase' is not configured.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LibrarySystem.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root containing 'LibrarySystem.slnx' could not be found.");
    }

    private static void EnsureSafeTestDatabase(string connectionString)
    {
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Integration test connection string must include a database name.");
        }

        if (string.Equals(databaseName, "library_system", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Integration tests cannot run against the development database 'library_system'.");
        }
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
