namespace LibrarySystem.Modules.Identity.Application.Dtos;

public sealed record LoginRequestDto(
    string Username,
    string Password);
