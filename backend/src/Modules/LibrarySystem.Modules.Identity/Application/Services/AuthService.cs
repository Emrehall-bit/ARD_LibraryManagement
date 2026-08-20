using FluentValidation;
using LibrarySystem.Modules.Identity.Application.Dtos;
using LibrarySystem.Modules.Identity.Application.Interfaces;
using LibrarySystem.Modules.Identity.Domain;
using LibrarySystem.Shared.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace LibrarySystem.Modules.Identity.Application.Services;

internal sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IValidator<RegisterRequestDto> registerRequestValidator,
    IValidator<LoginRequestDto> loginRequestValidator) : IAuthService
{
    public async Task<AuthResponseDto> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await registerRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await userManager.FindByNameAsync(request.Username) is not null)
        {
            throw new ConflictException("Username is already taken.");
        }

        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            throw new ConflictException("Email is already taken.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Username,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new ValidationException(result.Errors.Select(error =>
                new FluentValidation.Results.ValidationFailure(nameof(request.Password), error.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, IdentityRoles.Member);

        if (!roleResult.Succeeded)
        {
            var deleteResult = await userManager.DeleteAsync(user);
            var message = CreateIdentityErrorMessage(
                $"Failed to assign identity role '{IdentityRoles.Member}' to new user.",
                roleResult);

            if (!deleteResult.Succeeded)
            {
                message = CreateIdentityErrorMessage(
                    $"{message} Failed to rollback created user.",
                    deleteResult);
            }

            throw new InvalidOperationException(message);
        }

        return tokenService.CreateAccessToken(user);
    }

    public async Task<AuthResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await loginRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await userManager.FindByNameAsync(request.Username);

        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new AuthenticationFailedException("Invalid username or password.");
        }

        return tokenService.CreateAccessToken(user);
    }

    private static string CreateIdentityErrorMessage(string message, IdentityResult result)
    {
        var errors = string.Join("; ", result.Errors.Select(error => error.Description));

        return string.IsNullOrWhiteSpace(errors)
            ? message
            : $"{message} {errors}";
    }
}
