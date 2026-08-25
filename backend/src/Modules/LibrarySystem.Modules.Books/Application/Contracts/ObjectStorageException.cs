namespace LibrarySystem.Modules.Books.Application.Contracts;

public sealed class ObjectStorageException : Exception
{
    public ObjectStorageException(string message)
        : base(message)
    {
    }

    public ObjectStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
