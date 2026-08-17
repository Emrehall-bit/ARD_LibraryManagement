using System.Text.Json;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Infrastructure;
using LibrarySystem.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
    private const string TestOrBearerAuthenticationScheme = "TestOrBearer";
    private const string TestDatabaseName = "library_system_tests";
    public const string TestJwtIssuer = "LibrarySystem.Api.Tests";
    public const string TestJwtAudience = "LibrarySystem.Api.Tests";
    public const string TestJwtKey = "library-system-tests-jwt-key-32-chars";

    private readonly string connectionString = CreateTestConnectionString();

    public LibrarySystemApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);
        Environment.SetEnvironmentVariable("Jwt__Key", TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "60");
    }

    public HttpClient CreateApiClient()
    {
        var client = CreateUnauthenticatedApiClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.HeaderName, TestAuthenticationHandler.UserName);

        return client;
    }

    public HttpClient CreateUnauthenticatedApiClient()
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
        var borrowingDbContext = scope.ServiceProvider.GetRequiredService<BorrowingDbContext>();
        var identityDbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await identityDbContext.Database.MigrateAsync();
        await booksDbContext.Database.MigrateAsync();
        await borrowingDbContext.Database.MigrateAsync();
        await ResetDataAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await ResetDataAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("Jwt__Issuer", null);
            Environment.SetEnvironmentVariable("Jwt__Audience", null);
            Environment.SetEnvironmentVariable("Jwt__Key", null);
            Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", null);
        }
    }

    public async Task ResetBooksDataAsync()
    {
        await ResetDataAsync();
    }

    public async Task ResetDataAsync()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            TRUNCATE TABLE
                borrowing.borrow_records,
                books.books,
                identity."AspNetUserTokens",
                identity."AspNetUserLogins",
                identity."AspNetUserClaims",
                identity."AspNetUsers";
            """;

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
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:ExpirationMinutes"] = "60",
                ["Logging:EventLog:LogLevel:Default"] = "None"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BooksDbContext>>();
            services.RemoveAll<BooksDbContext>();
            services.RemoveAll<DbContextOptions<BorrowingDbContext>>();
            services.RemoveAll<BorrowingDbContext>();
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();
            services.RemoveAll<IdentityDbContext>();

            services.AddDbContext<BooksDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(BooksDbContext).Assembly.FullName);
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "books");
                }));

            services.AddDbContext<BorrowingDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(BorrowingDbContext).Assembly.FullName);
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "borrowing");
                }));

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName);
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "identity");
                }));

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestOrBearerAuthenticationScheme;
                    options.DefaultChallengeScheme = TestOrBearerAuthenticationScheme;
                })
                .AddPolicyScheme(TestOrBearerAuthenticationScheme, displayName: null, options =>
                {
                    options.ForwardDefaultSelector = context =>
                    {
                        var authorizationHeader = context.Request.Headers.Authorization.ToString();

                        if (authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            return JwtBearerDefaults.AuthenticationScheme;
                        }

                        return TestAuthenticationHandler.AuthenticationScheme;
                    };
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { });
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
