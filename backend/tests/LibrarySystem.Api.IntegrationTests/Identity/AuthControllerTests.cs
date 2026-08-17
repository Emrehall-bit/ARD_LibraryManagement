using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using LibrarySystem.Api.IntegrationTests.Infrastructure;
using LibrarySystem.Modules.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
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

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await dbContext.Users.SingleAsync(user => user.UserName == request.Username);

        Assert.Equal(request.Email, user.Email);
        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
        Assert.NotEqual(request.Password, user.PasswordHash);
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

        var response = await client.GetAsync("/api/books");

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

        var response = await client.GetAsync("/api/books");

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
}
