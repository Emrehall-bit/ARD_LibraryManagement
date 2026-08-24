using LibrarySystem.Modules.Identity.Domain;
using LibrarySystem.Shared.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibrarySystem.Modules.Identity.Infrastructure.AdminBootstrap;

public static class AdminBootstrapExtensions
{
    public static async Task BootstrapDevelopmentAdminAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider
            .GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(AdminBootstrapExtensions).FullName!);
        var options = configuration
            .GetSection(AdminBootstrapOptions.SectionName)
            .Get<AdminBootstrapOptions>()
            ?? new AdminBootstrapOptions();

        var hasUsername = !string.IsNullOrWhiteSpace(options.Username);
        var hasEmail = !string.IsNullOrWhiteSpace(options.Email);
        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);

        if (!hasUsername && !hasEmail && !hasPassword)
        {
            logger?.LogInformation("Development admin bootstrap configuration was not provided.");
            return;
        }

        if (!hasUsername || !hasEmail || !hasPassword)
        {
            logger?.LogWarning(
                "Development admin bootstrap configuration is incomplete. Admin user was not created.");
            return;
        }

        var username = options.Username!.Trim();
        var email = options.Email!.Trim();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var userByName = await userManager.FindByNameAsync(username);
        var userByEmail = await userManager.FindByEmailAsync(email);

        if (IsAmbiguousMatch(userByName, userByEmail, username, email))
        {
            logger?.LogWarning(
                "Development admin bootstrap configuration matched conflicting existing users. No account was elevated.");
            return;
        }

        var user = userByName ?? userByEmail;

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = username,
                Email = email
            };

            var createResult = await userManager.CreateAsync(user, options.Password!);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(CreateIdentityErrorMessage(
                    "Failed to create development admin bootstrap user.",
                    createResult));
            }
        }

        if (await userManager.IsInRoleAsync(user, IdentityRoles.Admin))
        {
            return;
        }

        var addRoleResult = await userManager.AddToRoleAsync(user, IdentityRoles.Admin);

        if (!addRoleResult.Succeeded)
        {
            throw new InvalidOperationException(CreateIdentityErrorMessage(
                $"Failed to assign identity role '{IdentityRoles.Admin}' to development admin bootstrap user.",
                addRoleResult));
        }
    }

    private static bool IsAmbiguousMatch(
        ApplicationUser? userByName,
        ApplicationUser? userByEmail,
        string username,
        string email)
    {
        if (userByName is not null &&
            !string.Equals(userByName.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (userByEmail is not null &&
            !string.Equals(userByEmail.UserName, username, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return userByName is not null &&
            userByEmail is not null &&
            userByName.Id != userByEmail.Id;
    }

    private static string CreateIdentityErrorMessage(string message, IdentityResult result)
    {
        var errors = string.Join("; ", result.Errors.Select(error => error.Description));

        return string.IsNullOrWhiteSpace(errors)
            ? message
            : $"{message} {errors}";
    }
}
