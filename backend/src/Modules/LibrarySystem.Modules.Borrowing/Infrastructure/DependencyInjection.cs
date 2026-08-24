using FluentValidation;
using LibrarySystem.Modules.Borrowing.Application.Dtos;
using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using LibrarySystem.Modules.Borrowing.Application.Services;
using LibrarySystem.Modules.Borrowing.Application.Validators;
using LibrarySystem.Modules.Borrowing.Infrastructure.Repositories;
using LibrarySystem.Modules.Borrowing.Infrastructure.Services;
using LibrarySystem.Modules.Borrowing.Infrastructure.Transactions;
using LibrarySystem.Shared.Borrowing;
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

        services.AddScoped<IBorrowRepository, BorrowRepository>();
        services.AddScoped<IBorrowingService, BorrowingService>();
        services.AddScoped<IAdminBorrowingDashboardSummaryService, AdminBorrowingDashboardSummaryService>();
        services.AddScoped<IUserBorrowingSummaryService, UserBorrowingSummaryService>();
        services.AddScoped<IValidator<GetOverdueBorrowRecordsQueryDto>, GetOverdueBorrowRecordsQueryValidator>();
        services.AddSingleton<IBorrowingClock, SystemBorrowingClock>();
        services.AddScoped<IBorrowingTransactionCoordinator, BorrowingTransactionCoordinator>();

        return services;
    }
}
