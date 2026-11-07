# new-patch-note.ps1
# Interactive script to add a new patch note entry to PatchNotes/ and update About.xml modVersion
# Run from the Empire/ directory (the one containing About/, PatchNotes/, etc.)

$ErrorActionPreference = "Stop"

$root = (Get-Location).Path
$notesDir = Join-Path $root "PatchNotes"
$modInfoPath = Join-Path $notesDir "_mod.md"
$aboutPath = "About\About.xml"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

# Verify we're in the right directory
if (-not (Test-Path $modInfoPath) -or -not (Test-Path $aboutPath)) {
    Write-Host "ERROR: Run this script from the Empire/ directory (the one containing About/ and PatchNotes/_mod.md)." -ForegroundColor Red
    exit 1
}

# Read current version from About.xml
$aboutContent = Get-Content $aboutPath -Raw
if ($aboutContent -match '<modVersion>([^<]+)</modVersion>') {
    $currentVersion = $Matches[1]
} else {
    $currentVersion = "unknown"
}
Write-Host "Current version: $currentVersion" -ForegroundColor Cyan
Write-Host ""

# --- Collect inputs ---

# Version
do {
    $version = Read-Host "New version (major.minor.patch, e.g. 0.51.0)"
} while ($version -notmatch '^\d+\.\d+\.\d+$')

$versionParts = $version -split '\.'
$major = [int]$versionParts[0]
$minor = [int]$versionParts[1]
$patch = [int]$versionParts[2]
$version = "$major.$minor.$patch"

$targetFile = Join-Path $notesDir "v$major.$minor.md"
$existing = $null
if (Test-Path $targetFile) {
    $existing = [System.IO.File]::ReadAllText($targetFile)
    if ($existing -match "(?m)^##\s+$([regex]::Escape($version))\s+-") {
        Write-Host "ERROR: $targetFile already has an entry for $version." -ForegroundColor Red
        exit 1
    }
}

# Label
do {
    $label = Read-Host "Label (short title for this update)"
} while ([string]::IsNullOrWhiteSpace($label))

# Description
do {
    $description = Read-Host "Description (one-line summary)"
} while ([string]::IsNullOrWhiteSpace($description))

# Patch note type
$types = @("Hotfix", "Patch", "Minor", "Major")
Write-Host ""
Write-Host "Patch note type:"
for ($i = 0; $i -lt $types.Count; $i++) {
    Write-Host "  $($i + 1)) $($types[$i])"
}
do {
    $typeChoice = Read-Host "Select type (1-4)"
} while ($typeChoice -notmatch '^[1-4]$')
$patchNoteType = $types[[int]$typeChoice - 1]

# Authors
$authorsInput = Read-Host "Author(s) (comma-separated)"
if ([string]::IsNullOrWhiteSpace($authorsInput)) {
    $authorsInput = ""
}
$authors = ($authorsInput -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }

# Change lines
Write-Host ""
Write-Host "Enter change lines (one per line, empty line to finish):"
$changeLines = @()
while ($true) {
    $line = Read-Host ">"
    if ([string]::IsNullOrWhiteSpace($line)) { break }
    $changeLines += $line.Trim()
}

if ($changeLines.Count -eq 0) {
    Write-Host "ERROR: At least one change line is required." -ForegroundColor Red
    exit 1
}

# --- Auto-fill date ---
$today = Get-Date
$releaseDateStr = $today.ToString("yyyy-MM-dd")

# --- Build the markdown entry ---
# A text line starting with '#', '-' or '\' would read as markdown structure; a leading '\' escapes it
function Escape-Line([string]$text) {
    $text = $text.Trim()
    if ($text -match '^[#\-\\]') { return "\$text" }
    return $text
}

$entryLines = @(
    "## $version - $($label.Trim())",
    "- Date: $releaseDateStr",
    "- Type: $patchNoteType",
    "- Authors: $($authors -join ', ')",
    "",
    (Escape-Line $description),
    "",
    "### Changes"
)
foreach ($change in $changeLines) {
    if ($change.StartsWith('\')) { $change = "\$change" }
    $entryLines += "- $change"
}
$entry = ($entryLines -join "`n") + "`n"

# --- Insert into the version file, newest entry first ---
if ($null -ne $existing) {
    $content = $existing -replace "`r`n", "`n"
    $headingMatch = [regex]::Match($content, '(?m)^## ')
    if ($headingMatch.Success) {
        $content = $content.Substring(0, $headingMatch.Index) + $entry + "`n" + $content.Substring($headingMatch.Index)
    } else {
        $content = $content.TrimEnd() + "`n`n" + $entry
    }
    [System.IO.File]::WriteAllText($targetFile, $content, $utf8NoBom)
    Write-Host "  Added to existing file: $targetFile" -ForegroundColor Cyan
} else {
    # New minor version: title the file after the mod name in _mod.md
    $modName = "Empire"
    $titleMatch = [regex]::Match([System.IO.File]::ReadAllText($modInfoPath), '(?m)^#\s+(.+?)\s*$')
    if ($titleMatch.Success) { $modName = $titleMatch.Groups[1].Value }
    $content = "# $modName v$major.$minor`n`n" + $entry
    [System.IO.File]::WriteAllText($targetFile, $content, $utf8NoBom)
    Write-Host "  Created new file: $targetFile" -ForegroundColor Cyan
}

# --- Update modVersion in About.xml ---
$aboutContent = Get-Content $aboutPath -Raw
if ($aboutContent -match '<modVersion>') {
    $aboutContent = $aboutContent -replace '<modVersion>[^<]+</modVersion>', "<modVersion>$version</modVersion>"
} else {
    $aboutContent = $aboutContent -replace '(<packageId>[^<]+</packageId>)', "`$1`n`t<modVersion>$version</modVersion>"
}
Set-Content $aboutPath -Value $aboutContent -NoNewline

# --- Update AssemblyVersion in .csproj ---
$csprojPath = "Source\Core\Empire.csproj"
if (Test-Path $csprojPath) {
    $assemblyVersion = "$major.$minor.$patch.0"
    $csprojContent = Get-Content $csprojPath -Raw
    $csprojContent = $csprojContent -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>"
    if ($csprojContent -match '<FileVersion>') {
        $csprojContent = $csprojContent -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$assemblyVersion</FileVersion>"
    } else {
        $csprojContent = $csprojContent -replace '(<AssemblyVersion>[^<]+</AssemblyVersion>)', "`$1`n        <FileVersion>$assemblyVersion</FileVersion>"
    }
    Set-Content $csprojPath -Value $csprojContent -NoNewline
    Write-Host "  Empire.csproj AssemblyVersion/FileVersion updated to $assemblyVersion" -ForegroundColor Cyan
} else {
    Write-Host "  WARNING: Could not find $csprojPath - AssemblyVersion not updated" -ForegroundColor Yellow
}

# --- Summary ---
Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Write-Host "  Patch note $version added to $targetFile"
Write-Host "  About.xml modVersion updated: $currentVersion -> $version"
Write-Host "  AssemblyVersion: $major.$minor.$patch.0"
Write-Host "  Type: $patchNoteType | Date: $releaseDateStr"
Write-Host "  Changes: $($changeLines.Count) line(s) | Authors: $($authors -join ', ')"
