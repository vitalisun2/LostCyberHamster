<#
.SYNOPSIS
    Auto-generates and keeps up-to-date three compact C# codebase snapshots for LLM consumption.
.DESCRIPTION
    Collects all .cs files from Assets/Scripts and Assets/Editor, normalizes whitespace,
    and writes three separate files in docs/Compacted Code/:
    - editor_scripts_compact.txt (Assets/Editor)
    - bot_compact.txt (Assets/Scripts/Bot)
    - game_scripts_compact.txt (Assets/Scripts excluding Bot)
    
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
$outputDir = Join-Path (Join-Path $repoRoot 'docs') 'Compacted Code'

# Define three module configurations
$modules = @(
    @{
        Name = 'editor_scripts_compact.txt'
        Title = 'EDITOR SCRIPTS COMPACT'
        IncludePaths = @(Join-Path (Join-Path $unityRoot 'Assets') 'Editor')
        ExcludePaths = @()
    },
    @{
        Name = 'bot_compact.txt'
        Title = 'BOT MODULE COMPACT'
        IncludePaths = @(Join-Path (Join-Path (Join-Path $unityRoot 'Assets') 'Scripts') 'Bot')
        ExcludePaths = @()
    },
    @{
        Name = 'game_scripts_compact.txt'
        Title = 'GAME SCRIPTS COMPACT (excluding Bot)'
        IncludePaths = @(Join-Path (Join-Path $unityRoot 'Assets') 'Scripts')
        ExcludePaths = @(Join-Path (Join-Path (Join-Path $unityRoot 'Assets') 'Scripts') 'Bot')
    }
)

$allWatchDirs = @(
    Join-Path (Join-Path $unityRoot 'Assets') 'Scripts'
    Join-Path (Join-Path $unityRoot 'Assets') 'Editor'
)

# --- Generation ---

function Get-FilesForModule($module) {
    $files = @()
    
    foreach ($includePath in $module.IncludePaths) {
        if (-not (Test-Path $includePath)) {
            continue
        }
        
        $candidateFiles = Get-ChildItem -Path $includePath -Filter '*.cs' -Recurse -File
        
        foreach ($f in $candidateFiles) {
            # Check if file should be excluded
            $shouldExclude = $false
            foreach ($excludePath in $module.ExcludePaths) {
                if ($f.FullName.StartsWith($excludePath, [StringComparison]::OrdinalIgnoreCase)) {
                    $shouldExclude = $true
                    break
                }
            }
            
            if (-not $shouldExclude) {
                $relPath = $f.FullName.Substring($repoRoot.Length + 1)
                $files += [PSCustomObject]@{
                    FullPath = $f.FullName
                    RelPath  = $relPath
                }
            }
        }
    }
    
    return $files | Sort-Object RelPath
}

function Invoke-GenerateModule($module) {
    $moduleFiles = Get-FilesForModule $module
    
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    $sourceRoots = ($module.IncludePaths | ForEach-Object {
        $_.Substring($repoRoot.Length + 1)
    }) -join ', '
    
    $sb = [System.Text.StringBuilder]::new(1024 * 1024)
    
    [void]$sb.AppendLine($module.Title)
    [void]$sb.AppendLine("GeneratedAtUtc: $timestamp")
    [void]$sb.AppendLine("SourceRoots: $sourceRoots")
    if ($module.ExcludePaths.Count -gt 0) {
        $excludeRoots = ($module.ExcludePaths | ForEach-Object {
            $_.Substring($repoRoot.Length + 1)
        }) -join ', '
        [void]$sb.AppendLine("ExcludedPaths: $excludeRoots")
    }
    [void]$sb.AppendLine("FileCount: $($moduleFiles.Count)")
    [void]$sb.AppendLine('Format: FILE blocks with normalized whitespace (trimmed line endings, collapsed blank lines)')
    
    foreach ($file in $moduleFiles) {
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
    
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    
    $outputFile = Join-Path $outputDir $module.Name
    [System.IO.File]::WriteAllText($outputFile, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
    
    $sizeKB = [math]::Round((Get-Item $outputFile).Length / 1024, 1)
    Write-Host "Generated: docs/Compacted Code/$($module.Name) ($($moduleFiles.Count) files, ${sizeKB} KB)"
}

function Invoke-Generate {
    foreach ($module in $modules) {
        Invoke-GenerateModule $module
    }
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

foreach ($dir in $allWatchDirs) {
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
