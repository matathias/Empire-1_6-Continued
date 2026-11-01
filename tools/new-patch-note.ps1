# new-patch-note.ps1
# Interactive script to add a new PatchNoteDef and update About.xml modVersion
# Run from the Empire/ directory (the one containing About/, 1.7/, etc.)

$ErrorActionPreference = "Stop"

$defsDir = "1.7\Defs\FCPatchNoteDefs"
$aboutPath = "About\About.xml"

# Verify we're in the right directory
if (-not (Test-Path $defsDir) -or -not (Test-Path $aboutPath)) {
    Write-Host "ERROR: Run this script from the Empire/ directory (the one containing About/ and 1.7/)." -ForegroundColor Red
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
$defName = "${major}_${minor}_${patch}"

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
    $changeLines += $line
}

if ($changeLines.Count -eq 0) {
    Write-Host "ERROR: At least one change line is required." -ForegroundColor Red
    exit 1
}

# --- Auto-fill date ---
$today = Get-Date
$releaseDateStr = $today.ToString("yyyy-MM-dd")

# --- Build XML block ---
$indent = "`t"

$changeLinesXml = ($changeLines | ForEach-Object { "${indent}${indent}<li>$([System.Security.SecurityElement]::Escape($_))</li>" }) -join "`n"
$authorsXml = ($authors | ForEach-Object { "${indent}${indent}<li>$([System.Security.SecurityElement]::Escape($_))</li>" }) -join "`n"

$xmlBlock = @"

${indent}<FactionColonies.PatchNoteDef ParentName="EmpirePatchBase">
${indent}${indent}<defName>$defName</defName>
${indent}${indent}<label>$([System.Security.SecurityElement]::Escape($label))</label>
${indent}${indent}<description>$([System.Security.SecurityElement]::Escape($description))</description>
${indent}${indent}<releaseDate>$releaseDateStr</releaseDate>
${indent}${indent}<patchNoteType>$patchNoteType</patchNoteType>
${indent}${indent}<patchNoteLines>
$changeLinesXml
${indent}${indent}</patchNoteLines>
${indent}${indent}<authors>
$authorsXml
${indent}${indent}</authors>
${indent}</FactionColonies.PatchNoteDef>
"@

# --- Insert into the correct version file ---
$targetFile = "$defsDir\PatchNoteDefs_v$major.$minor.xml"

if (Test-Path $targetFile) {
    # Append to existing file: insert new def before </Defs>
    $content = Get-Content $targetFile -Raw
    $content = $content -replace '</Defs>', "$xmlBlock`n</Defs>"
    Set-Content $targetFile -Value $content -NoNewline
    Write-Host "  Appended to existing file: $targetFile" -ForegroundColor Cyan
} else {
    # Create new file for this minor version
    $fileContent = "<?xml version=`"1.0`" encoding=`"utf-8`" ?>`n<Defs>$xmlBlock`n</Defs>`n"
    Set-Content $targetFile -Value $fileContent -NoNewline
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
Write-Host "  PatchNoteDef '$defName' added to $targetFile"
Write-Host "  About.xml modVersion updated: $currentVersion -> $version"
Write-Host "  AssemblyVersion: $major.$minor.$patch.0"
Write-Host "  Type: $patchNoteType | Date: $releaseDateStr"
Write-Host "  Changes: $($changeLines.Count) line(s) | Authors: $($authors -join ', ')"
