using LibrarySystem.Modules.Books.Application.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;

namespace LibrarySystem.Modules.Books.Infrastructure.Storage;

public static class BookImageStorageExtensions
{
    public static IServiceCollection AddBookImageStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<MinioOptions>()
            .Bind(configuration.GetSection(MinioOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<MinioOptions>, MinioOptionsValidator>();

        var options = configuration
            .GetSection(MinioOptions.SectionName)
            .Get<MinioOptions>()
            ?? new MinioOptions();

        if (!options.IsConfigured())
        {
            services.AddSingleton<IBookImageStorage, DisabledBookImageStorage>();
            services.AddSingleton<IBookImageStorageBootstrapper, DisabledBookImageStorageBootstrapper>();

            return services;
        }

        services.AddMinio(configureClient => configureClient
            .WithEndpoint(options.Endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithSSL(options.UseSsl)
            .Build());

        services.AddScoped<IBookImageStorage, MinioBookImageStorage>();
        services.AddScoped<IBookImageStorageBootstrapper, MinioBookImageStorageBootstrapper>();

        return services;
    }

    public static async Task EnsureBookImageStorageAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<MinioOptions>>().Value;

        if (!options.IsConfigured())
        {
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("BookImageStorage");

            logger.LogInformation("MinIO image storage is not configured. Bucket bootstrap was skipped.");
            return;
        }

        var bootstrapper = scope.ServiceProvider.GetRequiredService<IBookImageStorageBootstrapper>();

        await bootstrapper.EnsureAsync(cancellationToken);
    }
}
