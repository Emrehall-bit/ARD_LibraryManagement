namespace LibrarySystem.Modules.Identity.Infrastructure.AdminBootstrap;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public string? Username { get; init; }

    public string? Email { get; init; }

    public string? Password { get; init; }
}
