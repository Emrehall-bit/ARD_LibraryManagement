# Books Development Seed Data

`books.seed.json` contains development/demo catalogue entries prepared from Open Library bibliographic metadata. Each entry contains only the fields used by the current domain model:

```json
{
  "name": "Book title",
  "author": "Author name",
  "stock": 5
}
```

The title and author values are generated from Open Library Search API results during development data preparation. Stock values are synthetic LibrarySystem demo data and are deterministically calculated in the `0-20` range from the book title and author.

The application does not call Open Library, or any other external book service, at runtime. The runtime seeder reads the embedded `books.seed.json` file only in Development and skips seeding when the Books database already contains records.

Regenerate the file with:

```powershell
.\generate-books-seed.ps1
```

The generator fetches bibliographic records, normalizes titles/authors, filters invalid metadata, removes duplicate `name + author` pairs, assigns deterministic stock values, and writes 200 seed entries by default.
