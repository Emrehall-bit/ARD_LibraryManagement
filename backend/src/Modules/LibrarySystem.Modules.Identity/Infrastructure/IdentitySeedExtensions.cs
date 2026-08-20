using LibrarySystem.Modules.Identity.Domain;
using LibrarySystem.Shared.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibrarySystem.Modules.Identity.Infrastructure;

public static class IdentitySeedExtensions
{
    public static async Task SeedIdentityAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureRoleExistsAsync(roleManager, IdentityRoles.Admin);
        await EnsureRoleExistsAsync(roleManager, IdentityRoles.Member);
        await AssignMemberRoleToUsersWithoutRolesAsync(userManager, cancellationToken);
    }

    private static async Task EnsureRoleExistsAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(CreateIdentityErrorMessage(
                $"Failed to create identity role '{roleName}'.",
                result));
        }
    }

    private static async Task AssignMemberRoleToUsersWithoutRolesAsync(
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var users = await userManager.Users.ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);

            if (roles.Count > 0)
            {
                continue;
            }

            var result = await userManager.AddToRoleAsync(user, IdentityRoles.Member);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(CreateIdentityErrorMessage(
                    $"Failed to assign identity role '{IdentityRoles.Member}' to user '{user.Id}'.",
                    result));
            }
        }
    }

    private static string CreateIdentityErrorMessage(string message, IdentityResult result)
    {
        var errors = string.Join("; ", result.Errors.Select(error => error.Description));

        return string.IsNullOrWhiteSpace(errors)
            ? message
            : $"{message} {errors}";
    }
}
