param(
    [int] $TargetCount = 5000,
    [int] $PageSize = 100
)

$ErrorActionPreference = 'Stop'

$queries = @(
    'subject:fiction',
    'subject:literature',
    'subject:science_fiction',
    'subject:fantasy',
    'subject:mystery',
    'subject:history',
    'subject:biography',
    'subject:philosophy',
    'subject:science',
    'subject:poetry',
    'subject:plays',
    'subject:children',
    'subject:classics',
    'subject:turkish_literature'
)

$outputPath = Join-Path $PSScriptRoot 'books.seed.json'
$books = [System.Collections.Generic.List[object]]::new()
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

function Normalize-Text([string] $value) {
    return ($value -replace '\s+', ' ').Trim()
}

function Test-BookText([string] $value) {
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $false
    }

    $text = Normalize-Text $value

    if ($text.Length -gt 200) {
        return $false
    }

    if ($text -match '^\W+$') {
        return $false
    }

    return $true
}

function Test-Author([string] $value) {
    if (-not (Test-BookText $value)) {
        return $false
    }

    $text = Normalize-Text $value
    $blockedAuthors = @(
        'unknown',
        'unknown author',
        'anonymous',
        'not available',
        'n/a'
    )

    return -not $blockedAuthors.Contains($text.ToLowerInvariant())
}

function Get-DeterministicStock([string] $name, [string] $author) {
    $inputText = "$name|$author"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($inputText)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()

    try {
        $hash = $sha256.ComputeHash($bytes)
        $number = [System.BitConverter]::ToUInt32($hash, 0)
    }
    finally {
        $sha256.Dispose()
    }

    return [int]($number % 21)
}

function New-OpenLibrarySearchUri([string] $query, [int] $page, [int] $limit) {
    $encodedQuery = [Uri]::EscapeDataString($query)
    $encodedFields = [Uri]::EscapeDataString('title,author_name')

    return "https://openlibrary.org/search.json?q=$encodedQuery&fields=$encodedFields&limit=$limit&page=$page"
}

function Invoke-OpenLibrarySearch([string] $uri) {
    $maxAttempts = 3

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        try {
            return Invoke-RestMethod -Uri $uri -TimeoutSec 30 -Headers @{
                'User-Agent' = 'LibrarySystem seed generator (development data preparation)'
            }
        }
        catch {
            if ($attempt -eq $maxAttempts) {
                throw
            }

            Start-Sleep -Seconds ($attempt * 2)
        }
    }
}

function Add-Book([string] $name, [string] $author) {
    $normalizedName = Normalize-Text $name
    $normalizedAuthor = Normalize-Text $author

    if (-not (Test-BookText $normalizedName) -or -not (Test-Author $normalizedAuthor)) {
        return
    }

    $key = "$normalizedName|$normalizedAuthor"

    if (-not $seen.Add($key)) {
        return
    }

    $books.Add([pscustomobject][ordered]@{
        name = $normalizedName
        author = $normalizedAuthor
        stock = Get-DeterministicStock $normalizedName $normalizedAuthor
    })
}

$pagesByQuery = @{}
$activeQueries = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

$queries | ForEach-Object {
    $pagesByQuery[$_] = 1
    $null = $activeQueries.Add($_)
}

while ($books.Count -lt $TargetCount -and $activeQueries.Count -gt 0) {
    foreach ($query in $queries) {
        if ($books.Count -ge $TargetCount) {
            break
        }

        if (-not $activeQueries.Contains($query)) {
            continue
        }

        $page = $pagesByQuery[$query]
        $uri = New-OpenLibrarySearchUri $query $page $PageSize

        Write-Host "Fetching $query page $page..."

        $response = Invoke-OpenLibrarySearch $uri

        if ($null -eq $response.docs -or $response.docs.Count -eq 0) {
            $null = $activeQueries.Remove($query)
            continue
        }

        foreach ($doc in $response.docs) {
            if ($books.Count -ge $TargetCount) {
                break
            }

            if ($null -eq $doc.author_name -or $doc.author_name.Count -eq 0) {
                continue
            }

            Add-Book $doc.title $doc.author_name[0]
        }

        $pagesByQuery[$query] = $page + 1
    }
}

if ($books.Count -lt $TargetCount) {
    throw "Only $($books.Count) valid unique books were collected; target was $TargetCount."
}

$orderedBooks = $books |
    Sort-Object @{ Expression = 'author'; Ascending = $true }, @{ Expression = 'name'; Ascending = $true } |
    Select-Object -First $TargetCount

$orderedBooks |
    ConvertTo-Json -Depth 3 |
    Set-Content -Path $outputPath -Encoding utf8

$uniqueAuthors = ($orderedBooks | Select-Object -ExpandProperty author -Unique).Count
$outOfStockCount = ($orderedBooks | Where-Object { $_.stock -eq 0 }).Count

Write-Host "Generated $($orderedBooks.Count) books."
Write-Host "Unique authors: $uniqueAuthors"
Write-Host "Out-of-stock entries: $outOfStockCount"
Write-Host "Output: $outputPath"
