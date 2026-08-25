using LibrarySystem.Api.ExceptionHandling;
using LibrarySystem.Api.AdminDashboard;
using LibrarySystem.Api.Hubs;
using LibrarySystem.Modules.Books.Infrastructure;
using LibrarySystem.Modules.Books.Infrastructure.Seeding;
using LibrarySystem.Modules.Books.Infrastructure.Storage;
using LibrarySystem.Modules.Borrowing.Application.Interfaces;
using LibrarySystem.Modules.Borrowing.Infrastructure;
using LibrarySystem.Modules.Identity.Infrastructure;
using LibrarySystem.Modules.Identity.Infrastructure.AdminBootstrap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

const string AngularDevelopmentCorsPolicy = "AngularDevelopment";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSignalR();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Authorization header using the Bearer scheme."
        };

        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, _) =>
    {
        var endpointMetadata = context.Description.ActionDescriptor.EndpointMetadata;
        var allowsAnonymous = endpointMetadata.OfType<IAllowAnonymous>().Any();
        var requiresAuthorization = endpointMetadata.OfType<IAuthorizeData>().Any();

        if (allowsAnonymous || !requiresAuthorization)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });

        return Task.CompletedTask;
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevelopmentCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? [];

        policy
            .WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type", "X-Requested-With");
    });
});

var databaseConnectionString = builder.Configuration.GetConnectionString("LibrarySystemDatabase")
    ?? throw new InvalidOperationException("Connection string 'LibrarySystemDatabase' is not configured.");

builder.Services.AddBooksInfrastructure(databaseConnectionString);
builder.Services.AddBookImageStorage(builder.Configuration);
builder.Services.AddBorrowingInfrastructure(databaseConnectionString);
builder.Services.AddIdentityInfrastructure(databaseConnectionString, builder.Configuration);
builder.Services.AddScoped<IBookStockChangeNotifier, SignalRBookStockChangeNotifier>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();

var app = builder.Build();

await app.Services.SeedIdentityAsync();
await app.Services.EnsureBookImageStorageAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.Services.BootstrapDevelopmentAdminAsync(app.Configuration);
    await app.Services.SeedBooksAsync();
}

app.UseHttpsRedirection();

app.UseExceptionHandler();

app.UseCors(AngularDevelopmentCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LibraryHub>("/hubs/library");

app.Run();

public partial class Program;
