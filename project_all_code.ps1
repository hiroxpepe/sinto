# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\project_all_code.ps1
# Optional:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\project_all_code.ps1 -ExcludeProjects "Tests~"

Param(
    [string]$OutFile = "project_all_code.md",
    [string[]]$ExcludeProjects = @()
)

$repo = $PSScriptRoot
if (-not $repo) { $repo = (Get-Location).ProviderPath }
$fullOut = Join-Path $repo $OutFile

# UTF-8 without BOM encoder for output
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# Delete old file
if (Test-Path $fullOut) { Remove-Item $fullOut -Force }

# Header
$header = "# Aggregated Sources (C#, asmdef, package.json, docs)`n`nRepository: $repo`nDate: $(Get-Date -Format u)`n`n"
[System.IO.File]::WriteAllText($fullOut, $header, $utf8NoBom)

# Excluded directories.
# Note: 'Tests~' (tilde-suffixed) is INCLUDED on purpose. Unity ignores it,
# but we want its contents (test code & fixtures) in the aggregated output.
$excludeDirs = @(
    'bin', 'obj',
    '.git', '.vs', '.vscode', '.idea',
    'Library', 'Temp', 'Logs',
    'artifacts'
) + $ExcludeProjects

# Collect target files
$files = Get-ChildItem -Path $repo -Recurse -File | Where-Object {
    # C# source: always
    $_.Extension -eq ".cs" -or
    # Assembly definition (JSON syntax): always
    $_.Extension -eq ".asmdef" -or
    # package.json: only at repo root
    ($_.Name -eq "package.json" -and $_.DirectoryName -eq $repo) -or
    # README.md: only at repo root
    ($_.Name -eq "README.md" -and $_.DirectoryName -eq $repo) -or
    # Anything (.md or .json) under docs/
    ($_.Extension -match "\.(md|json)$" -and $_.DirectoryName -match "[\\/]docs([\\/]|$)") -or
    # JSON fixtures and schemas (test data, validation schemas)
    ($_.Extension -eq ".json" -and $_.DirectoryName -match "[\\/](Fixtures|Schemas)([\\/]|$)") -or
    # JSON sample personas under examples/
    ($_.Extension -eq ".json" -and $_.DirectoryName -match "[\\/]examples([\\/]|$)") -or
    # validate_examples.mjs: only at repo root
    ($_.Name -eq "validate_examples.mjs" -and $_.DirectoryName -eq $repo) -or
    # validation_log.txt: only at repo root
    ($_.Name -eq "validation_log.txt" -and $_.DirectoryName -eq $repo) -or
    # csproj for the test project
    $_.Extension -eq ".csproj"
} | Sort-Object FullName

# Buffer the output content; write once at the end
$sb = New-Object System.Text.StringBuilder

foreach ($f in $files) {
    # Skip the output file itself (defensive)
    if ($f.FullName -eq $fullOut) { continue }

    $rel = $f.FullName.Substring($repo.Length + 1)
    $skip = $false

    # Filter by ExcludeProjects (path prefix match)
    if ($ExcludeProjects) {
        foreach ($p in $ExcludeProjects) {
            $pn = $p.ToString().Trim()
            if ($pn -ne '' -and $rel.StartsWith($pn)) {
                $skip = $true
                break
            }
        }
    }
    if ($skip) { continue }

    # Filter by directory segments (tree -I style)
    $segments = $rel.Split([System.IO.Path]::DirectorySeparatorChar)
    foreach ($d in $excludeDirs) {
        if ($segments -contains $d) {
            $skip = $true
            break
        }
    }
    if ($skip) { continue }

    # Progress
    Write-Host "Adding: $rel"

    # Use "## FILE:" prefix instead of bare "## " to avoid colliding with
    # markdown headings inside collected files (e.g. README.md's own ## headings).
    [void]$sb.AppendLine("## FILE: $rel")
    [void]$sb.AppendLine("")

    # Language tag for syntax highlighting
    $lang = switch -Regex ($f.Extension) {
        "\.cs$"     { "csharp" }
        "\.asmdef$" { "json" }
        "\.json$"   { "json" }
        "\.md$"     { "markdown" }
        "\.csproj$" { "xml" }
        "\.mjs$"    { "javascript" }
        "\.txt$"    { "" }
        default     { "" }
    }

    [void]$sb.AppendLine("``````$lang")

    # CRITICAL FIX: read the source file as UTF-8 explicitly.
    # Default Get-Content on Windows PowerShell 5.1 uses the system code page,
    # which corrupts UTF-8 Japanese / multibyte characters (mojibake).
    $content = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
    [void]$sb.Append($content)

    # Ensure a trailing newline before the closing fence
    if (-not $content.EndsWith("`n")) {
        [void]$sb.AppendLine("")
    }

    [void]$sb.AppendLine("``````")
    [void]$sb.AppendLine("")
}

# Append everything in one shot, UTF-8 without BOM
[System.IO.File]::AppendAllText($fullOut, $sb.ToString(), $utf8NoBom)

Write-Host "DONE: Generated $OutFile"
