namespace LibrarySystem.Modules.Identity.Application.Dtos;

public sealed record RegisterRequestDto(
    string Username,
    string Email,
    string Password);
