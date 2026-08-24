using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Identity.Domain;
using LibrarySystem.Modules.Identity.Infrastructure;
using LibrarySystem.Modules.Identity.Infrastructure.AdminBootstrap;
using LibrarySystem.Shared.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LibrarySystem.Api.IntegrationTests.Identity;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AuthControllerTests(LibrarySystemApiFactory factory) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await factory.ResetDataAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsSuccess()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var request = CreateRegisterRequest();

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(authResponse);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.AccessToken));
        Assert.True(authResponse.ExpiresIn > 0);
        Assert.Equal("Bearer", authResponse.TokenType);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(authResponse.AccessToken);

        AssertClaimValue(token, ClaimTypes.Role, IdentityRoles.Member);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await dbContext.Users.SingleAsync(user => user.UserName == request.Username);

        Assert.Equal(request.Email, user.Email);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
        Assert.NotEqual(request.Password, user.PasswordHash);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.True(await userManager.IsInRoleAsync(user, IdentityRoles.Member));
        Assert.False(await userManager.IsInRoleAsync(user, IdentityRoles.Admin));
    }

    [Fact]
    public async Task Startup_SeedsIdentityRoles()
    {
        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        Assert.True(await roleManager.RoleExistsAsync(IdentityRoles.Admin));
        Assert.True(await roleManager.RoleExistsAsync(IdentityRoles.Member));
    }

    [Fact]
    public async Task SeedIdentityAsync_WhenRunRepeatedly_DoesNotCreateDuplicateRoles()
    {
        await factory.Services.SeedIdentityAsync();
        await factory.Services.SeedIdentityAsync();

        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var adminRoleCount = await roleManager.Roles.CountAsync(role => role.Name == IdentityRoles.Admin);
        var memberRoleCount = await roleManager.Roles.CountAsync(role => role.Name == IdentityRoles.Member);

        Assert.Equal(1, adminRoleCount);
        Assert.Equal(1, memberRoleCount);
    }

    [Fact]
    public async Task SeedIdentityAsync_AssignsMemberRoleToUsersWithoutRoles()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var request = CreateRegisterRequest();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = request.Username,
                Email = request.Email
            };

            var createResult = await userManager.CreateAsync(user, request.Password);

            Assert.True(createResult.Succeeded);
            Assert.Empty(await userManager.GetRolesAsync(user));
        }

        await factory.Services.SeedIdentityAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.Users.SingleAsync(user => user.UserName != null);

            Assert.True(await userManager.IsInRoleAsync(user, IdentityRoles.Member));
        }
    }

    [Fact]
    public async Task BootstrapDevelopmentAdminAsync_WithNoConfiguration_DoesNotCreateAdminUser()
    {
        await factory.Services.BootstrapDevelopmentAdminAsync(new ConfigurationBuilder().Build());

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Equal(0, await userManager.Users.CountAsync());
    }

    [Fact]
    public async Task BootstrapDevelopmentAdminAsync_WithIncompleteConfiguration_DoesNotCreateAdminUser()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AdminBootstrapOptions.SectionName}:Username"] = CreateUsername(),
                [$"{AdminBootstrapOptions.SectionName}:Password"] = "ValidAdminPassword123!"
            })
            .Build();

        await factory.Services.BootstrapDevelopmentAdminAsync(configuration);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.Equal(0, await userManager.Users.CountAsync());
    }

    [Fact]
    public async Task BootstrapDevelopmentAdminAsync_WithConfiguration_CreatesAdminUser()
    {
        var credentials = CreateAdminBootstrapCredentials();

        await factory.Services.BootstrapDevelopmentAdminAsync(CreateAdminBootstrapConfiguration(credentials));

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(credentials.Username);

        Assert.NotNull(user);
        Assert.Equal(credentials.Email, user.Email);
        Assert.True(await userManager.IsInRoleAsync(user, IdentityRoles.Admin));
    }

    [Fact]
    public async Task BootstrapDevelopmentAdminAsync_WhenRunRepeatedly_DoesNotCreateDuplicateUserOrRole()
    {
        var credentials = CreateAdminBootstrapCredentials();
        var configuration = CreateAdminBootstrapConfiguration(credentials);

        await factory.Services.BootstrapDevelopmentAdminAsync(configuration);
        await factory.Services.BootstrapDevelopmentAdminAsync(configuration);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.Users.SingleAsync(user =>
            user.UserName == credentials.Username && user.Email == credentials.Email);
        var roles = await userManager.GetRolesAsync(user);

        Assert.Single(await userManager.Users.ToListAsync());
        Assert.Single(roles);
        Assert.Contains(IdentityRoles.Admin, roles);
    }

    [Fact]
    public async Task BootstrapDevelopmentAdminAsync_WhenExistingMatchingUserIsNotAdmin_AddsAdminRole()
    {
        var credentials = CreateAdminBootstrapCredentials();

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = credentials.Username,
                Email = credentials.Email
            };
            var createResult = await userManager.CreateAsync(user, credentials.Password);

            Assert.True(createResult.Succeeded);
            Assert.False(await userManager.IsInRoleAsync(user, IdentityRoles.Admin));
        }

        await factory.Services.BootstrapDevelopmentAdminAsync(CreateAdminBootstrapConfiguration(credentials));

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.Users.SingleAsync();

            Assert.True(await userManager.IsInRoleAsync(user, IdentityRoles.Admin));
        }
    }

    [Fact]
    public async Task BootstrapDevelopmentAdminAsync_WhenUsernameAndEmailMatchDifferentUsers_DoesNotElevateEitherUser()
    {
        var credentials = CreateAdminBootstrapCredentials();
        var usernameMatchedEmail = CreateEmail();
        var emailMatchedUsername = CreateUsername();

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var userByName = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = credentials.Username,
                Email = usernameMatchedEmail
            };
            var userByEmail = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = emailMatchedUsername,
                Email = credentials.Email
            };

            Assert.True((await userManager.CreateAsync(userByName, credentials.Password)).Succeeded);
            Assert.True((await userManager.CreateAsync(userByEmail, credentials.Password)).Succeeded);
        }

        await factory.Services.BootstrapDevelopmentAdminAsync(CreateAdminBootstrapConfiguration(credentials));

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var users = await userManager.Users.ToListAsync();

            Assert.Equal(2, users.Count);
            foreach (var user in users)
            {
                Assert.False(await userManager.IsInRoleAsync(user, IdentityRoles.Admin));
            }
        }
    }

    [Fact]
    public async Task Login_WithBootstrappedAdminCredentials_ReturnsAdminRoleClaim()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var credentials = CreateAdminBootstrapCredentials();

        await factory.Services.BootstrapDevelopmentAdminAsync(CreateAdminBootstrapConfiguration(credentials));

        var authResponse = await LoginAsync(client, credentials.Username, credentials.Password);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(authResponse.AccessToken);

        AssertClaimValue(token, ClaimTypes.Role, IdentityRoles.Admin);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsConflict()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var request = CreateRegisterRequest();
        var duplicateRequest = request with { Email = CreateEmail() };

        var firstResponse = await client.PostAsJsonAsync("/api/auth/register", request);
        var secondResponse = await client.PostAsJsonAsync("/api/auth/register", duplicateRequest);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var request = CreateRegisterRequest();
        var duplicateRequest = request with { Username = CreateUsername() };

        var firstResponse = await client.PostAsJsonAsync("/api/auth/register", request);
        var secondResponse = await client.PostAsJsonAsync("/api/auth/register", duplicateRequest);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Register_WithInvalidRequest_ReturnsBadRequest()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var request = new RegisterRequest(string.Empty, "not-an-email", "short");

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var content = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var errors = content.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("Username", out var usernameErrors));
        Assert.True(usernameErrors.GetArrayLength() > 0);
        Assert.True(errors.TryGetProperty("Email", out var emailErrors));
        Assert.True(emailErrors.GetArrayLength() > 0);
        Assert.True(errors.TryGetProperty("Password", out var passwordErrors));
        Assert.True(passwordErrors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwt()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var request = CreateRegisterRequest();
        await RegisterAsync(client, request);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(request.Username, request.Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(authResponse);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.AccessToken));
        Assert.True(authResponse.ExpiresIn > 0);
        Assert.Equal("Bearer", authResponse.TokenType);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(authResponse.AccessToken);

        Assert.Equal(LibrarySystemApiFactory.TestJwtIssuer, token.Issuer);
        Assert.Contains(LibrarySystemApiFactory.TestJwtAudience, token.Audiences);
        Assert.NotEqual(default, token.ValidTo);
        AssertClaimExists(token, JwtRegisteredClaimNames.Sub);
        AssertClaimExists(token, ClaimTypes.NameIdentifier);
        AssertClaimValue(token, JwtRegisteredClaimNames.UniqueName, request.Username);
        AssertClaimValue(token, ClaimTypes.Name, request.Username);
        AssertClaimValue(token, JwtRegisteredClaimNames.Email, request.Email);
        AssertClaimValue(token, ClaimTypes.Email, request.Email);
        AssertClaimValue(token, ClaimTypes.Role, IdentityRoles.Member);
        AssertClaimExists(token, JwtRegisteredClaimNames.Exp);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var request = CreateRegisterRequest();
        await RegisterAsync(client, request);
        const string wrongPassword = "WrongPassword123!";

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(request.Username, wrongPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(request.Username, content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request.Password, content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(wrongPassword, content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithUnknownUser_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(CreateUsername(), "ValidPassword123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("not found", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unknown", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        using var client = factory.CreateUnauthenticatedApiClient();

        var response = await client.GetAsync("/api/borrow/my-books");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithRealJwt_ReturnsSuccess()
    {
        using var client = factory.CreateUnauthenticatedApiClient();
        var request = CreateRegisterRequest();
        await RegisterAsync(client, request);
        var authResponse = await LoginAsync(client, request.Username, request.Password);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            authResponse.TokenType,
            authResponse.AccessToken);

        var response = await client.GetAsync("/api/borrow/my-books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task RegisterAsync(HttpClient client, RegisterRequest request)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        response.EnsureSuccessStatusCode();
    }

    private static async Task<AuthResponse> LoginAsync(
        HttpClient client,
        string username,
        string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, password));

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new InvalidOperationException("Login response body was empty.");
    }

    private static RegisterRequest CreateRegisterRequest()
    {
        var uniqueValue = Guid.NewGuid().ToString("N");

        return new RegisterRequest(
            $"user-{uniqueValue}",
            $"user-{uniqueValue}@example.test",
            "ValidPassword123!");
    }

    private static AdminBootstrapCredentials CreateAdminBootstrapCredentials()
    {
        var uniqueValue = Guid.NewGuid().ToString("N");

        return new AdminBootstrapCredentials(
            $"admin-{uniqueValue}",
            $"admin-{uniqueValue}@example.test",
            "ValidAdminPassword123!");
    }

    private static IConfiguration CreateAdminBootstrapConfiguration(AdminBootstrapCredentials credentials)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AdminBootstrapOptions.SectionName}:Username"] = credentials.Username,
                [$"{AdminBootstrapOptions.SectionName}:Email"] = credentials.Email,
                [$"{AdminBootstrapOptions.SectionName}:Password"] = credentials.Password
            })
            .Build();
    }

    private static string CreateUsername()
    {
        return $"user-{Guid.NewGuid():N}";
    }

    private static string CreateEmail()
    {
        return $"user-{Guid.NewGuid():N}@example.test";
    }

    private static void AssertClaimExists(JwtSecurityToken token, string claimType)
    {
        Assert.Contains(token.Claims, claim => claim.Type == claimType && !string.IsNullOrWhiteSpace(claim.Value));
    }

    private static void AssertClaimValue(JwtSecurityToken token, string claimType, string expectedValue)
    {
        Assert.Contains(token.Claims, claim => claim.Type == claimType && claim.Value == expectedValue);
    }

    private sealed record RegisterRequest(string Username, string Email, string Password);

    private sealed record LoginRequest(string Username, string Password);

    private sealed record AuthResponse(string AccessToken, int ExpiresIn, string TokenType);

    private sealed record AdminBootstrapCredentials(string Username, string Email, string Password);
}
