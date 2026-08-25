namespace LibrarySystem.Tools.CoverImport;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        return CoverImportCommand.RunAsync(args);
    }
}
