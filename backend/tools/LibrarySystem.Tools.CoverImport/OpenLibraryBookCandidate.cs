namespace LibrarySystem.Tools.CoverImport;

public sealed record OpenLibraryBookCandidate(
    string Title,
    IReadOnlyCollection<string> Authors,
    long? CoverId);
