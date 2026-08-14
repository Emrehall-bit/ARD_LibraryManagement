namespace LibrarySystem.Modules.Identity.Application.Dtos;

public sealed record AuthResponseDto(
    string AccessToken,
    int ExpiresIn,
    string TokenType);
