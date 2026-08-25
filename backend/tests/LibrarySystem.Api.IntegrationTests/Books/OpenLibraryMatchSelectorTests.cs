using LibrarySystem.Tools.CoverImport;

namespace LibrarySystem.Api.IntegrationTests.Books;

public sealed class OpenLibraryMatchSelectorTests
{
    [Fact]
    public void SelectByTitleAndAuthor_WithExactTitleAndAuthorMatch_ReturnsCover()
    {
        var match = OpenLibraryMatchSelector.SelectByTitleAndAuthor(
            "Dune",
            "Frank Herbert",
            [
                new OpenLibraryBookCandidate("Dune", ["Frank Herbert"], 12345)
            ]);

        Assert.NotNull(match);
        Assert.Equal(12345, match.CoverId);
    }

    [Fact]
    public void SelectByTitleAndAuthor_NormalizesPunctuationAndCase()
    {
        var match = OpenLibraryMatchSelector.SelectByTitleAndAuthor(
            "The Hobbit",
            "J.R.R. Tolkien",
            [
                new OpenLibraryBookCandidate("the hobbit!", ["J R R Tolkien"], 67890)
            ]);

        Assert.NotNull(match);
        Assert.Equal(67890, match.CoverId);
    }

    [Fact]
    public void SelectByTitleAndAuthor_WithWrongAuthor_ReturnsNull()
    {
        var match = OpenLibraryMatchSelector.SelectByTitleAndAuthor(
            "Dune",
            "Frank Herbert",
            [
                new OpenLibraryBookCandidate("Dune", ["Brian Herbert"], 12345)
            ]);

        Assert.Null(match);
    }

    [Fact]
    public void SelectByTitleAndAuthor_WithNoCover_ReturnsNull()
    {
        var match = OpenLibraryMatchSelector.SelectByTitleAndAuthor(
            "Dune",
            "Frank Herbert",
            [
                new OpenLibraryBookCandidate("Dune", ["Frank Herbert"], null)
            ]);

        Assert.Null(match);
    }

    [Fact]
    public void SelectByTitleAndAuthor_WithAmbiguousMatches_ReturnsNull()
    {
        var match = OpenLibraryMatchSelector.SelectByTitleAndAuthor(
            "Dune",
            "Frank Herbert",
            [
                new OpenLibraryBookCandidate("Dune", ["Frank Herbert"], 12345),
                new OpenLibraryBookCandidate("Dune", ["Frank Herbert"], 67890)
            ]);

        Assert.Null(match);
    }
}
