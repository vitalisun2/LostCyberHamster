param(
    [int]$Days = 3,
    [switch]$DryRun,
    [switch]$Force,
    [string[]]$DirNames = @('EditorLogs','Logs')
)

$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
$repoRoot = Split-Path $scriptDir -Parent
$stateDir = Join-Path $repoRoot '.temp'
if (-not (Test-Path $stateDir)) { New-Item -ItemType Directory -Path $stateDir -Force | Out-Null }
$lastRunFile = Join-Path $stateDir 'last_logs_cleaned.txt'
$reportFile = Join-Path $stateDir 'cleanup_old_logs_report.json'

$today = (Get-Date).ToString('yyyy-MM-dd')
if (-not $Force) {
    if (Test-Path $lastRunFile) {
        try { $prev = Get-Content $lastRunFile -ErrorAction SilentlyContinue } catch { $prev = '' }
        if ($prev -eq $today) {
            Write-Host "[cleanup-old-logs] Already run today ($today). Use -Force to override." -ForegroundColor Yellow
            exit 0
        }
    }
}

$cutoff = (Get-Date).AddDays(-$Days)
Write-Host "[cleanup-old-logs] Searching for directories named: $($DirNames -join ', ')" -ForegroundColor Cyan
$dirs = Get-ChildItem -Path $repoRoot -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object { $DirNames -contains $_.Name }
if ($dirs.Count -eq 0) {
    Write-Host "[cleanup-old-logs] No matching directories found." -ForegroundColor Yellow
    Set-Content -Path $lastRunFile -Value $today -Encoding UTF8
    exit 0
}

$files = @()
foreach ($d in $dirs) {
    $found = Get-ChildItem -Path $d.FullName -File -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -lt $cutoff }
    foreach ($f in $found) { $files += $f }
}

$results = @()
$totalFreed = 0
$countDeleted = 0

if ($files.Count -eq 0) {
    Write-Host "[cleanup-old-logs] No files older than $Days days found in $($dirs.Count) directories." -ForegroundColor Green
    Set-Content -Path $lastRunFile -Value $today -Encoding UTF8
    $summary = [PSCustomObject]@{
        RunAt = (Get-Date).ToString('o')
        Days = $Days
        DirNames = $DirNames
        CandidateCount = 0
        DeletedCount = 0
        FreedBytes = 0
        Results = @()
    }
    $summary | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportFile -Encoding UTF8
    Write-Host "Report: $reportFile"
    exit 0
}

foreach ($f in $files) {
    $entry = [PSCustomObject]@{
        FullName = $f.FullName
        LastWriteTime = $f.LastWriteTime
        Length = $f.Length
        Deleted = $false
        Error = $null
    }
    if ($DryRun) {
        $results += $entry
        continue
    }
    try {
        $len = $f.Length
        Remove-Item -LiteralPath $f.FullName -Force -ErrorAction Stop
        $entry.Deleted = $true
        $totalFreed += $len
        $countDeleted += 1
    } catch {
        $entry.Error = $_.Exception.Message
    }
    $results += $entry
}

$summary = [PSCustomObject]@{
    RunAt = (Get-Date).ToString('o')
    Days = $Days
    DirNames = $DirNames
    CandidateCount = $files.Count
    DeletedCount = $countDeleted
    FreedBytes = $totalFreed
    Results = $results
}

$summary | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportFile -Encoding UTF8

if (-not $DryRun) {
    Set-Content -Path $lastRunFile -Value $today -Encoding UTF8
    Write-Host "[cleanup-old-logs] Deleted $countDeleted files, freed $totalFreed bytes. Report: $reportFile" -ForegroundColor Green
} else {
    Write-Host "[cleanup-old-logs] Dry run: $($files.Count) candidates. Report: $reportFile" -ForegroundColor Yellow
}
