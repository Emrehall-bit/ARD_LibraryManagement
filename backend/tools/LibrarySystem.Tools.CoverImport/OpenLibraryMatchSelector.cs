using System.Globalization;
using System.Text;

namespace LibrarySystem.Tools.CoverImport;

public static class OpenLibraryMatchSelector
{
    public static OpenLibraryCoverMatch? SelectByTitleAndAuthor(
        string expectedTitle,
        string expectedAuthor,
        IEnumerable<OpenLibraryBookCandidate> candidates)
    {
        var normalizedTitle = Normalize(expectedTitle);
        var normalizedAuthor = Normalize(expectedAuthor);

        if (normalizedTitle.Length == 0 || normalizedAuthor.Length == 0)
        {
            return null;
        }

        var matches = candidates
            .Where(candidate => candidate.CoverId.HasValue)
            .SelectMany(candidate => candidate.Authors.Select(author => new
            {
                Candidate = candidate,
                Author = author,
                TitleMatches = Normalize(candidate.Title) == normalizedTitle,
                AuthorMatches = Normalize(author) == normalizedAuthor
            }))
            .Where(match => match.TitleMatches && match.AuthorMatches)
            .Select(match => new OpenLibraryCoverMatch(
                match.Candidate.CoverId!.Value,
                match.Candidate.Title,
                match.Author))
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = true;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }
}
