using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using LibrarySystem.Modules.Books.Domain;

namespace LibrarySystem.Tools.CoverImport;

internal sealed class OpenLibraryClient(HttpClient httpClient)
{
    private const int MaxCoverBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(350);

    private DateTimeOffset lastRequestAt = DateTimeOffset.MinValue;

    public async Task<OpenLibraryCoverReference?> FindCoverAsync(Book book, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(book.Isbn))
        {
            return new OpenLibraryCoverReference(
                OpenLibraryLookupKind.Isbn,
                book.Isbn.Trim(),
                $"ISBN:{book.Isbn.Trim()}");
        }

        var uri = "https://openlibrary.org/search.json?" +
            $"title={Uri.EscapeDataString(book.Name)}&" +
            $"author={Uri.EscapeDataString(book.Author)}&" +
            "limit=5&fields=title,author_name,cover_i";

        using var stream = await GetStreamAsync(uri, cancellationToken);
        if (stream is null)
        {
            return null;
        }

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var candidates = new List<OpenLibraryBookCandidate>();

        foreach (var doc in docs.EnumerateArray())
        {
            var title = doc.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var authors = ReadStringArray(doc, "author_name");
            var coverId = doc.TryGetProperty("cover_i", out var coverElement) && coverElement.TryGetInt64(out var parsedCoverId)
                ? parsedCoverId
                : (long?)null;

            candidates.Add(new OpenLibraryBookCandidate(title, authors, coverId));
        }

        var match = OpenLibraryMatchSelector.SelectByTitleAndAuthor(book.Name, book.Author, candidates);

        return match is null
            ? null
            : new OpenLibraryCoverReference(
                OpenLibraryLookupKind.CoverId,
                match.CoverId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"{match.Title} / {match.Author}");
    }

    public async Task<DownloadedCover?> DownloadCoverAsync(
        OpenLibraryCoverReference cover,
        CancellationToken cancellationToken)
    {
        var key = cover.Kind == OpenLibraryLookupKind.Isbn ? "isbn" : "id";
        var uri = $"https://covers.openlibrary.org/b/{key}/{Uri.EscapeDataString(cover.Value)}-M.jpg?default=false";

        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, uri),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Open Library cover request failed with status {(int)response.StatusCode}.");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            return null;
        }

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength > MaxCoverBytes)
        {
            throw new InvalidOperationException($"Cover exceeds {MaxCoverBytes} bytes.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        if (buffer.Length == 0)
        {
            return null;
        }

        if (buffer.Length > MaxCoverBytes)
        {
            throw new InvalidOperationException($"Cover exceeds {MaxCoverBytes} bytes.");
        }

        return new DownloadedCover(buffer.ToArray(), contentType, ExtensionFor(contentType));
    }

    private async Task<Stream?> GetStreamAsync(string uri, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Get, uri),
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Open Library search request failed with status {(int)response.StatusCode}.");
        }

        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return buffer;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            await WaitForRateLimitAsync(cancellationToken);

            using var request = requestFactory();
            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode is HttpStatusCode.TooManyRequests)
            {
                if (attempt == 2)
                {
                    return response;
                }

                response.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                continue;
            }

            if ((int)response.StatusCode >= 500)
            {
                if (attempt == 2)
                {
                    return response;
                }

                response.Dispose();
                await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
                continue;
            }

            return response;
        }

        throw new InvalidOperationException("Open Library request retry loop ended unexpectedly.");
    }

    private async Task WaitForRateLimitAsync(CancellationToken cancellationToken)
    {
        var elapsed = DateTimeOffset.UtcNow - lastRequestAt;
        if (elapsed < RequestDelay)
        {
            await Task.Delay(RequestDelay - elapsed, cancellationToken);
        }

        lastRequestAt = DateTimeOffset.UtcNow;
    }

    private static IReadOnlyCollection<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static string ExtensionFor(string contentType)
    {
        return contentType switch
        {
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
    }
}

internal sealed record OpenLibraryCoverReference(OpenLibraryLookupKind Kind, string Value, string DisplayName);

internal enum OpenLibraryLookupKind
{
    Isbn,
    CoverId
}

internal sealed record DownloadedCover(byte[] Bytes, string ContentType, string Extension);
