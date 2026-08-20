$ErrorActionPreference = 'Stop'

$adjectives = @(
    'Adaptive', 'Balanced', 'Bright', 'Calm', 'Clear', 'Compact', 'Creative', 'Digital',
    'Dynamic', 'Emerging', 'Essential', 'Focused', 'Gentle', 'Hidden', 'Modern', 'Open',
    'Practical', 'Quiet', 'Rapid', 'Resilient', 'Robust', 'Signal', 'Silent', 'Simple',
    'Steady'
)

$subjects = @(
    'Archive', 'Atlas', 'Bridge', 'Catalog', 'Circuit', 'Compass', 'Dataset', 'Design',
    'Garden', 'Harbor', 'Index', 'Journey', 'Kernel', 'Library', 'Map', 'Notebook',
    'Pattern', 'Protocol', 'River', 'System'
)

$themes = @(
    'for Beginners', 'in Practice', 'Field Notes', 'Handbook', 'Reference', 'Workbook',
    'Case Studies', 'Primer', 'Companion', 'Guide'
)

$firstNames = @(
    'Alex', 'Avery', 'Casey', 'Devon', 'Drew', 'Emery', 'Finley', 'Harper', 'Jordan',
    'Kai', 'Logan', 'Morgan', 'Parker', 'Quinn', 'Reese', 'Riley', 'Rowan', 'Sage',
    'Taylor', 'Terry'
)

$lastNames = @(
    'Anders', 'Bennett', 'Carter', 'Dawson', 'Ellis', 'Foster', 'Gray', 'Hayes',
    'Irwin', 'Jensen', 'Keller', 'Lane', 'Morris', 'Nolan', 'Owen', 'Porter',
    'Reed', 'Stone', 'Turner', 'Vale'
)

$books = for ($i = 0; $i -lt 5000; $i++) {
    $adjective = $adjectives[$i % $adjectives.Count]
    $subject = $subjects[[Math]::Floor($i / $adjectives.Count) % $subjects.Count]
    $theme = $themes[[Math]::Floor($i / ($adjectives.Count * $subjects.Count)) % $themes.Count]
    $volume = [Math]::Floor($i / ($adjectives.Count * $subjects.Count * $themes.Count)) + 1
    $firstName = $firstNames[($i * 7 + 3) % $firstNames.Count]
    $lastName = $lastNames[($i * 11 + 5) % $lastNames.Count]
    $stock = ($i * 5 + 3) % 21

    [ordered]@{
        name = "Synthetic Catalogue - $adjective $subject $theme Vol. $volume"
        author = "$firstName $lastName"
        stock = $stock
    }
}

$outputPath = Join-Path $PSScriptRoot 'books.seed.json'
$books | ConvertTo-Json -Depth 3 | Set-Content -Path $outputPath -Encoding utf8
