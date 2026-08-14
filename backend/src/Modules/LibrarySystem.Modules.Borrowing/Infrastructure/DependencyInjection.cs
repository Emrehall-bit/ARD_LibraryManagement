using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibrarySystem.Modules.Borrowing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBorrowingInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<BorrowingDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(BorrowingDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "borrowing");
            }));

        return services;
    }
}
