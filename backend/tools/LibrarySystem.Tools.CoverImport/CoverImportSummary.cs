namespace LibrarySystem.Tools.CoverImport;

internal sealed class CoverImportSummary
{
    public int TotalConsidered { get; set; }

    public int ExistingCoverSkipped { get; set; }

    public int Matched { get; set; }

    public int Uploaded { get; set; }

    public int NoMatch { get; set; }

    public int NoCover { get; set; }

    public int Failed { get; set; }

    public void Print()
    {
        Console.WriteLine();
        Console.WriteLine("Summary");
        Console.WriteLine($"Total considered: {TotalConsidered}");
        Console.WriteLine($"Existing cover skipped: {ExistingCoverSkipped}");
        Console.WriteLine($"Matched: {Matched}");
        Console.WriteLine($"Uploaded: {Uploaded}");
        Console.WriteLine($"No match: {NoMatch}");
        Console.WriteLine($"No cover: {NoCover}");
        Console.WriteLine($"Failed: {Failed}");
    }
}
