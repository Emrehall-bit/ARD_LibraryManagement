using LibrarySystem.Api.ExceptionHandling;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Borrowing.Infrastructure;
using LibrarySystem.Modules.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var databaseConnectionString = builder.Configuration.GetConnectionString("LibrarySystemDatabase")
    ?? throw new InvalidOperationException("Connection string 'LibrarySystemDatabase' is not configured.");

builder.Services.AddBooksInfrastructure(databaseConnectionString);
builder.Services.AddBorrowingInfrastructure(databaseConnectionString);
builder.Services.AddIdentityInfrastructure(databaseConnectionString);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseAuthorization();

app.MapControllers();

app.Run();
