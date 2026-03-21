<#
.SYNOPSIS
    Auto-generates and keeps up-to-date a compact C# codebase snapshot for LLM consumption.
.DESCRIPTION
    Collects all .cs files from Assets/Scripts and Assets/Editor, normalizes whitespace,
    and writes docs/game_scripts_codebase_compact.txt.
    
    Then watches for file changes and re-generates automatically with debounce.
    Starts automatically when opening the repo in VS Code (via .vscode/tasks.json).
.EXAMPLE
    .\codebase_compact.ps1
    .\codebase_compact.ps1 -DebounceSeconds 5
    .\codebase_compact.ps1 -Once    # single generation, no watching
#>
param(
    [int]$DebounceSeconds = 3,
    [switch]$Once
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$unityRoot = Join-Path $repoRoot 'LostCyberHamster'
$outputFile = Join-Path (Join-Path $repoRoot 'docs') 'game_scripts_codebase_compact.txt'

$sourceDirs = @(
    Join-Path (Join-Path $unityRoot 'Assets') 'Scripts'
    Join-Path (Join-Path $unityRoot 'Assets') 'Editor'
)

# --- Generation ---

function Invoke-Generate {
    $allFiles = @()
    foreach ($dir in $sourceDirs) {
        if (Test-Path $dir) {
            $files = Get-ChildItem -Path $dir -Filter '*.cs' -Recurse -File
            foreach ($f in $files) {
                $relPath = $f.FullName.Substring($repoRoot.Length + 1)
                $allFiles += [PSCustomObject]@{
                    FullPath = $f.FullName
                    RelPath  = $relPath
                }
            }
        }
    }

    $allFiles = $allFiles | Sort-Object RelPath

    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $sourceRoots = ($sourceDirs | ForEach-Object {
        $_.Substring($repoRoot.Length + 1)
    }) -join ', '

    $sb = [System.Text.StringBuilder]::new(1024 * 1024)

    [void]$sb.AppendLine('GAME CODEBASE COMPACT MERGE')
    [void]$sb.AppendLine("GeneratedAtUtc: $timestamp")
    [void]$sb.AppendLine("SourceRoots: $sourceRoots")
    [void]$sb.AppendLine("FileCount: $($allFiles.Count)")
    [void]$sb.AppendLine('Format: FILE blocks with normalized whitespace (trimmed line endings, collapsed blank lines)')

    foreach ($file in $allFiles) {
        $rawContent = [System.IO.File]::ReadAllText($file.FullPath)

        $lines = $rawContent -split "`r?`n" | ForEach-Object { $_.TrimEnd() }

        $normalized = [System.Collections.Generic.List[string]]::new()
        $prevBlank = $false
        foreach ($line in $lines) {
            $isBlank = [string]::IsNullOrWhiteSpace($line)
            if ($isBlank -and $prevBlank) { continue }
            $normalized.Add($line)
            $prevBlank = $isBlank
        }

        while ($normalized.Count -gt 0 -and [string]::IsNullOrWhiteSpace($normalized[$normalized.Count - 1])) {
            $normalized.RemoveAt($normalized.Count - 1)
        }

        $normalizedText = $normalized -join "`n"
        $lineCount = $normalized.Count

        $bytes = [System.Text.Encoding]::UTF8.GetBytes($normalizedText)
        $sha = [System.Security.Cryptography.SHA256]::Create()
        $hash = ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('X2') }) -join ''

        [void]$sb.AppendLine('')
        [void]$sb.AppendLine("=== FILE START: $($file.RelPath) | lines=$lineCount | sha256=$hash ===")
        [void]$sb.AppendLine($normalizedText)
        [void]$sb.AppendLine("=== FILE END: $($file.RelPath) ===")
    }

    $outDir = Split-Path $outputFile -Parent
    if (-not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    }

    [System.IO.File]::WriteAllText($outputFile, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))

    $sizeKB = [math]::Round((Get-Item $outputFile).Length / 1024, 1)
    Write-Host "Generated: docs/game_scripts_codebase_compact.txt ($($allFiles.Count) files, ${sizeKB} KB)"
}

# --- Initial generation ---

Write-Host "[codebase-compact] Generating snapshot..." -ForegroundColor Cyan
Invoke-Generate

if ($Once) { exit 0 }

# --- Watch mode ---

Write-Host "[codebase-compact] Watching for .cs changes (debounce: ${DebounceSeconds}s)..." -ForegroundColor Green
Write-Host "[codebase-compact] Press Ctrl+C to stop." -ForegroundColor DarkGray

$watchers = @()
$pendingRegenerate = $false
$lastChangeTime = [DateTime]::MinValue

foreach ($dir in $sourceDirs) {
    if (-not (Test-Path $dir)) { continue }

    $watcher = [System.IO.FileSystemWatcher]::new($dir)
    $watcher.Filter = '*.cs'
    $watcher.IncludeSubdirectories = $true
    $watcher.NotifyFilter = [System.IO.NotifyFilters]::LastWrite -bor
                            [System.IO.NotifyFilters]::FileName -bor
                            [System.IO.NotifyFilters]::CreationTime
    $watcher.EnableRaisingEvents = $true
    $watchers += $watcher
}

try {
    while ($true) {
        foreach ($w in $watchers) {
            $result = $w.WaitForChanged(
                [System.IO.WatcherChangeTypes]::Changed -bor
                [System.IO.WatcherChangeTypes]::Created -bor
                [System.IO.WatcherChangeTypes]::Deleted -bor
                [System.IO.WatcherChangeTypes]::Renamed,
                200
            )
            if (-not $result.TimedOut) {
                $lastChangeTime = [DateTime]::Now
                $pendingRegenerate = $true
                $shortPath = $result.Name
                if ($result.ChangeType -eq 'Renamed') {
                    $shortPath = "$($result.OldName) -> $($result.Name)"
                }
                Write-Host "[codebase-compact] $($result.ChangeType): $shortPath" -ForegroundColor Yellow
            }
        }

        if ($pendingRegenerate) {
            $elapsed = ([DateTime]::Now - $lastChangeTime).TotalSeconds
            if ($elapsed -ge $DebounceSeconds) {
                $pendingRegenerate = $false
                Write-Host "[codebase-compact] Regenerating..." -ForegroundColor Cyan
                try {
                    Invoke-Generate
                    Write-Host "[codebase-compact] Done. Watching..." -ForegroundColor Green
                } catch {
                    Write-Host "[codebase-compact] ERROR: $_" -ForegroundColor Red
                }
            }
        }
    }
} finally {
    foreach ($w in $watchers) {
        $w.EnableRaisingEvents = $false
        $w.Dispose()
    }
    Write-Host "[codebase-compact] Stopped." -ForegroundColor DarkGray
}
