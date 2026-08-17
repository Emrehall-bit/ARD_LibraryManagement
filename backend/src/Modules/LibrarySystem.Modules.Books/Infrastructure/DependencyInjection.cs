using FluentValidation;
using LibrarySystem.Modules.Books.Application.Contracts;
using LibrarySystem.Modules.Books.Application.Dtos;
using LibrarySystem.Modules.Books.Application.Interfaces;
using LibrarySystem.Modules.Books.Application.Services;
using LibrarySystem.Modules.Books.Application.Validators;
using LibrarySystem.Modules.Books.Infrastructure.Repositories;
using LibrarySystem.Modules.Books.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibrarySystem.Modules.Books.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBooksInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<BooksDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(BooksDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "books");
            }));

        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<IBookInventoryService, BookInventoryService>();
        services.AddScoped<IValidator<CreateBookRequestDto>, CreateBookRequestValidator>();

        return services;
    }
}
