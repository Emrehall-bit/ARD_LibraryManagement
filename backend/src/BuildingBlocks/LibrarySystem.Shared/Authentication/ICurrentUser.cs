namespace LibrarySystem.Shared.Authentication;

public interface ICurrentUser
{
    string? UserId { get; }

    bool IsAuthenticated { get; }
}
