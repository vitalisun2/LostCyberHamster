param(
    [string]$SourceWorktree = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$SandboxRoot = 'C:\BuildWorkspaces\LostCyberHamster_Android',
    [string]$BuildLabel = '',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Unity.exe',
    [switch]$Development,
    [switch]$SkipUnityEditorReferenceCheck,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$unityProjectRelativePath = 'LostCyberHamster'
$skillBuildScript = Join-Path $env:USERPROFILE '.codex\skills\publish-build-to-telegram-buffer\scripts\build_unity_player.ps1'

function Write-Step {
    param([string]$Message)

    if (-not $Json.IsPresent) {
        Write-Host "[build-android-telegram] $Message"
    }
}

function Get-FullPath {
    param([string]$Path)

    return [System.IO.Path]::GetFullPath($Path)
}

function Assert-PathExists {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description not found: $Path"
    }
}

function Get-SafeName {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return 'unknown'
    }

    return ($Value -replace '[^A-Za-z0-9_.-]', '_').Trim('_')
}

function Get-GitValue {
    param([string[]]$GitArgs)

    try {
        $value = & git -C $SourceWorktree @GitArgs 2>$null | Select-Object -First 1
        if ($null -eq $value) {
            return ''
        }

        return [string]$value
    }
    catch {
        return ''
    }
}

function Get-SourceDirty {
    $status = @(& git -C $SourceWorktree status --short 2>$null)
    return $status.Count -gt 0
}

function Get-FileSha256 {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ''
    }

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-SourceDiffHash {
    $builder = New-Object System.Text.StringBuilder

    try {
        [void]$builder.AppendLine((& git -C $SourceWorktree status --short 2>$null) -join "`n")
        [void]$builder.AppendLine((& git -C $SourceWorktree diff --binary 2>$null) -join "`n")

        $untracked = @(& git -C $SourceWorktree ls-files --others --exclude-standard 2>$null)
        foreach ($relativePath in $untracked) {
            $fullPath = Join-Path $SourceWorktree $relativePath
            [void]$builder.AppendLine("UNTRACKED $relativePath $(Get-FileSha256 -Path $fullPath)")
        }
    }
    catch {
        [void]$builder.AppendLine("diff-hash-error: $($_.Exception.Message)")
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($builder.ToString())
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Invoke-RobocopyMirror {
    param(
        [string]$Source,
        [string]$Destination
    )

    Assert-PathExists -Path $Source -Description 'Source sync directory'
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null

    Write-Step "Syncing $Source -> $Destination"
    & robocopy $Source $Destination /MIR /MT:8 /R:2 /W:2 /NFL /NDL /NP /NJH /NJS | Out-Null
    $exitCode = $LASTEXITCODE
    if ($exitCode -gt 7) {
        throw "robocopy failed with exit code $exitCode while syncing $Source to $Destination"
    }
}

function Sync-UnityProjectSnapshot {
    param(
        [string]$SourceRoot,
        [string]$TargetRoot
    )

    $sourceProject = Join-Path $SourceRoot $unityProjectRelativePath
    $targetProject = Join-Path $TargetRoot $unityProjectRelativePath
    Assert-PathExists -Path $sourceProject -Description 'Source Unity project'
    New-Item -ItemType Directory -Force -Path $targetProject | Out-Null

    foreach ($relativePath in @('Assets', 'Packages', 'ProjectSettings')) {
        Invoke-RobocopyMirror `
            -Source (Join-Path $sourceProject $relativePath) `
            -Destination (Join-Path $targetProject $relativePath)
    }

    return $targetProject
}

function Set-JsonProperty {
    param(
        [object]$Object,
        [string]$Name,
        [object]$Value
    )

    if ($Object.PSObject.Properties.Name -contains $Name) {
        $Object.$Name = $Value
        return
    }

    $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
}

function Write-BuildManifest {
    param(
        [string]$SandboxProjectPath,
        [object]$Manifest
    )

    $diagnosticsResources = Join-Path $SandboxProjectPath 'Assets\Resources\Diagnostics'
    New-Item -ItemType Directory -Force -Path $diagnosticsResources | Out-Null

    $manifestPath = Join-Path $diagnosticsResources 'build_manifest.json'
    $Manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $settingsPath = Join-Path $diagnosticsResources 'device_log_settings.json'
    if (Test-Path -LiteralPath $settingsPath) {
        $settings = Get-Content -LiteralPath $settingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    else {
        $settings = [pscustomobject]@{}
    }

    Set-JsonProperty -Object $settings -Name 'buildLabel' -Value $Manifest.buildId
    Set-JsonProperty -Object $settings -Name 'branch' -Value $Manifest.sourceBranch
    Set-JsonProperty -Object $settings -Name 'shortSha' -Value $Manifest.sourceCommit
    Set-JsonProperty -Object $settings -Name 'dirty' -Value $Manifest.sourceDirty
    $settings | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $settingsPath -Encoding UTF8

    return $manifestPath
}

function Invoke-SkillBuildHelper {
    param(
        [string]$ProjectPath,
        [string]$OutputRoot,
        [string]$LogPath
    )

    Assert-PathExists -Path $skillBuildScript -Description 'Skill Unity build helper'

    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $skillBuildScript,
        '-RepositoryRoot', $SourceWorktree,
        '-ProjectPath', $ProjectPath,
        '-UnityExe', $UnityExe,
        '-OutputRoot', $OutputRoot,
        '-Platform', 'Android'
    )

    if ($Development.IsPresent) {
        $arguments += '-Development'
    }

    if ($SkipUnityEditorReferenceCheck.IsPresent) {
        $arguments += '-SkipUnityEditorReferenceCheck'
    }

    Write-Step "Starting Unity Android build. Build helper log: $LogPath"
    $output = & powershell.exe @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output | Set-Content -LiteralPath $LogPath -Encoding UTF8

    if ($exitCode -ne 0) {
        $tail = ($output | Select-Object -Last 80) -join "`n"
        throw "Unity build helper failed with exit code $exitCode. Log: $LogPath`n$tail"
    }
}

function Get-LatestBuildSummary {
    param(
        [string]$OutputRoot,
        [datetime]$StartedAt
    )

    $summary = Get-ChildItem -LiteralPath $OutputRoot -Recurse -Filter 'build-summary.codex.json' -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -ge $StartedAt.AddMinutes(-5) } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $summary) {
        throw "Build summary was not found under $OutputRoot"
    }

    return $summary
}

$SourceWorktree = Get-FullPath -Path $SourceWorktree
$SandboxRoot = Get-FullPath -Path $SandboxRoot
$sourceProjectPath = Join-Path $SourceWorktree $unityProjectRelativePath
$sandboxProjectPath = Join-Path $SandboxRoot $unityProjectRelativePath
$outputRoot = Join-Path $SourceWorktree 'Builds\telegram-buffer'
$entrypointLogRoot = Join-Path $outputRoot 'entrypoint-logs'

Assert-PathExists -Path $SourceWorktree -Description 'Source worktree'
Assert-PathExists -Path $sourceProjectPath -Description 'Source Unity project'
Assert-PathExists -Path $UnityExe -Description 'Unity executable'

if ($SourceWorktree.TrimEnd('\', '/') -ieq $SandboxRoot.TrimEnd('\', '/')) {
    throw 'SourceWorktree and SandboxRoot must be different directories.'
}

New-Item -ItemType Directory -Force -Path $SandboxRoot | Out-Null
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $entrypointLogRoot | Out-Null

$branch = Get-GitValue -GitArgs @('rev-parse', '--abbrev-ref', 'HEAD')
$shortSha = Get-GitValue -GitArgs @('rev-parse', '--short', 'HEAD')
$dirty = Get-SourceDirty
$diffHash = Get-SourceDiffHash

if ([string]::IsNullOrWhiteSpace($branch)) {
    $branch = 'unknown'
}

if ([string]::IsNullOrWhiteSpace($shortSha)) {
    $shortSha = 'unknown'
}

if ([string]::IsNullOrWhiteSpace($BuildLabel)) {
    $BuildLabel = if ($branch.Contains('/')) { ($branch -split '/')[-1] } else { $branch }
}

$safeLabel = Get-SafeName -Value $BuildLabel
$safeSha = Get-SafeName -Value $shortSha
$dirtySuffix = if ($dirty) { 'dirty' } else { 'clean' }
$buildStamp = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$builtAtUtc = (Get-Date).ToUniversalTime().ToString('o')
$buildId = "${buildStamp}_android_${safeLabel}_${safeSha}_${dirtySuffix}"
$entrypointLogPath = Join-Path $entrypointLogRoot "$buildId.log"

Write-Step "Preparing sandbox: $SandboxRoot"
$sandboxProjectPath = Sync-UnityProjectSnapshot -SourceRoot $SourceWorktree -TargetRoot $SandboxRoot

$manifest = [ordered]@{
    buildId = $buildId
    buildLabel = $BuildLabel
    sourceWorktree = $SourceWorktree.Replace('\', '/')
    sourceBranch = $branch
    sourceCommit = $shortSha
    sourceDirty = $dirty
    sourceDiffHash = $diffHash
    sandboxRoot = $SandboxRoot.Replace('\', '/')
    builtAtUtc = $builtAtUtc
    platform = 'Android'
    development = $Development.IsPresent
}

$manifestPath = Write-BuildManifest -SandboxProjectPath $sandboxProjectPath -Manifest ([pscustomobject]$manifest)
Write-Step "Build manifest written: $manifestPath"

$startedAt = Get-Date
Invoke-SkillBuildHelper -ProjectPath $sandboxProjectPath -OutputRoot $outputRoot -LogPath $entrypointLogPath

$summaryPath = Get-LatestBuildSummary -OutputRoot $outputRoot -StartedAt $startedAt
$skillSummary = Get-Content -LiteralPath $summaryPath.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $skillSummary.apk -or -not (Test-Path -LiteralPath $skillSummary.apk)) {
    throw "Build summary does not point to an existing APK: $($summaryPath.FullName)"
}

$apk = Get-Item -LiteralPath $skillSummary.apk
$outputDir = Split-Path -Parent $apk.FullName
$outputManifestPath = Join-Path $outputDir 'build-manifest.codex.json'
([pscustomobject]$manifest) | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputManifestPath -Encoding UTF8

$result = [ordered]@{
    buildId = $buildId
    buildLabel = $BuildLabel
    sourceWorktree = $SourceWorktree
    sourceBranch = $branch
    sourceCommit = $shortSha
    sourceDirty = $dirty
    sourceDiffHash = $diffHash
    sandboxRoot = $SandboxRoot
    sandboxProjectPath = $sandboxProjectPath
    manifestResourcePath = $manifestPath
    outputManifestPath = $outputManifestPath
    outputDir = $outputDir
    apk = $apk.FullName
    apkSizeBytes = $apk.Length
    platform = 'Android'
    development = $Development.IsPresent
    builtAtUtc = $builtAtUtc
    buildHelperLog = $entrypointLogPath
    skillSummaryPath = $summaryPath.FullName
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath.FullName -Encoding UTF8

if ($Json.IsPresent) {
    [pscustomobject]$result | ConvertTo-Json -Depth 8
}
else {
    [pscustomobject]$result | Format-List
}
