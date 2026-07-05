param(
    [string]$SourceWorktree = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$SandboxRoot = 'C:\BuildWorkspaces\LostCyberHamster_Android',
    [string]$BuildLabel = '',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Unity.exe',
    [string]$AndroidSigningConfigPath = '',
    [switch]$Development,
    [switch]$SkipUnityEditorReferenceCheck,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$unityProjectRelativePath = 'LostCyberHamster'
$buildAutomationRelativePath = 'Assets\Editor\LostCyberHamsterBuildAutomation.cs'

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

function Test-UnityEditorReferences {
    param([string]$UnityProjectPath)

    $scriptsPath = Join-Path $UnityProjectPath 'Assets\Scripts'
    Assert-PathExists -Path $scriptsPath -Description 'Project scripts folder'

    $violations = New-Object System.Collections.Generic.List[string]
    $files = Get-ChildItem -LiteralPath $scriptsPath -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\Editor\\' }

    foreach ($file in $files) {
        $lines = @(Get-Content -LiteralPath $file.FullName -Encoding UTF8)
        $editorGuardDepth = 0

        for ($index = 0; $index -lt $lines.Count; $index++) {
            $trimmed = $lines[$index].Trim()

            if ($trimmed -match '^#if\s+UNITY_EDITOR\b') {
                $editorGuardDepth++
            }
            elseif ($trimmed -match '^#endif\b' -and $editorGuardDepth -gt 0) {
                $editorGuardDepth--
            }

            if ($editorGuardDepth -gt 0) {
                continue
            }

            if ($lines[$index] -match '^\s*using\s+UnityEditor\s*;|UnityEditor\.') {
                $violations.Add(("{0}:{1}: {2}" -f $file.FullName, ($index + 1), $trimmed))
            }
        }
    }

    if ($violations.Count -gt 0) {
        $message = "Potential player-build blockers: unguarded runtime UnityEditor references found under Assets/Scripts.`n" +
            (($violations | Select-Object -First 40) -join "`n")
        throw "$message`nFix or guard the references, or rerun with -SkipUnityEditorReferenceCheck if you intentionally accept the risk."
    }
}

function Get-UnityProjectProcesses {
    param([string]$UnityProjectPath)

    $projectFullPath = [System.IO.Path]::GetFullPath($UnityProjectPath).TrimEnd('\', '/')
    $projectName = Split-Path -Leaf $projectFullPath
    $normalizedProjectPath = $projectFullPath.Replace('/', '\')

    $processesById = @{}
    foreach ($process in Get-Process Unity -ErrorAction SilentlyContinue) {
        $processesById[[int]$process.Id] = $process
    }

    foreach ($cimProcess in Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue) {
        $process = $processesById[[int]$cimProcess.ProcessId]
        if ($null -eq $process) {
            continue
        }

        $commandLine = if ($cimProcess.CommandLine) { $cimProcess.CommandLine.Replace('/', '\') } else { '' }
        $title = $process.MainWindowTitle
        $openedThisProject =
            $commandLine.Contains($normalizedProjectPath) -or
            (-not [string]::IsNullOrWhiteSpace($title) -and $title.Contains($projectName))

        if ($openedThisProject) {
            $process
        }
    }
}

function Close-OpenUnityProjectEditors {
    param([string]$UnityProjectPath)

    $projectProcesses = @(Get-UnityProjectProcesses -UnityProjectPath $UnityProjectPath)
    if ($projectProcesses.Count -eq 0) {
        return
    }

    Write-Step "Closing open Unity editor instances for project: $UnityProjectPath"
    foreach ($process in $projectProcesses) {
        try {
            if ($process.MainWindowHandle -ne 0) {
                [void]$process.CloseMainWindow()
            }
        }
        catch {
            Write-Step "[warn] Failed to request Unity editor close for PID $($process.Id): $($_.Exception.Message)"
        }
    }

    $deadline = (Get-Date).AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 500
        $remaining = @(Get-UnityProjectProcesses -UnityProjectPath $UnityProjectPath)
    } while ($remaining.Count -gt 0 -and (Get-Date) -lt $deadline)

    foreach ($process in $remaining) {
        try {
            Stop-Process -Id $process.Id -Force
            Write-Step "Stopped Unity editor PID $($process.Id)."
        }
        catch {
            Write-Step "[warn] Failed to stop Unity editor PID $($process.Id): $($_.Exception.Message)"
        }
    }
}

function Convert-ToProcessArgumentLine {
    param([string[]]$Arguments)

    $escapedArguments = foreach ($argument in $Arguments) {
        if ($null -eq $argument) {
            continue
        }

        $escaped = $argument.Replace('"', '\"')
        if ($escaped -match '\s|"') {
            '"' + $escaped + '"'
        }
        else {
            $escaped
        }
    }

    return ($escapedArguments -join ' ')
}

function Get-DefaultAndroidSigningConfigPath {
    $userProfile = $env:USERPROFILE
    if ([string]::IsNullOrWhiteSpace($userProfile)) {
        $userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    }

    return Join-Path $userProfile '.lostcyberhamster\android-dev-signing\signing.local.json'
}

function Resolve-ConfiguredPath {
    param(
        [string]$Path,
        [string]$BaseDirectory
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BaseDirectory $Path))
}

function Get-AndroidSigningMetadata {
    param([string]$ConfigPath)

    Assert-PathExists -Path $ConfigPath -Description 'Android dev signing config'
    $config = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($property in @('keystorePath', 'keystorePass', 'keyaliasName', 'keyaliasPass')) {
        if (-not ($config.PSObject.Properties.Name -contains $property) -or [string]::IsNullOrWhiteSpace($config.$property)) {
            throw "Android dev signing config field '$property' is missing: $ConfigPath"
        }
    }

    $configDirectory = Split-Path -Parent $ConfigPath
    $keystorePath = Resolve-ConfiguredPath -Path $config.keystorePath -BaseDirectory $configDirectory
    Assert-PathExists -Path $keystorePath -Description 'Android dev signing keystore'

    return [pscustomobject]@{
        configPath = $ConfigPath
        keystorePath = $keystorePath
        keyAliasName = $config.keyaliasName
        certificateSha256 = if ($config.PSObject.Properties.Name -contains 'certificateSha256') { $config.certificateSha256 } else { '' }
    }
}

function Invoke-UnityAndroidBuild {
    param(
        [string]$ProjectPath,
        [string]$OutputDir,
        [string]$LogPath,
        [string]$SigningConfigPath
    )

    Assert-PathExists -Path (Join-Path $ProjectPath $buildAutomationRelativePath) -Description 'Repo-owned Unity build automation'

    $developmentValue = if ($Development.IsPresent) { 'true' } else { 'false' }
    $unityArgs = @(
        '-batchmode',
        '-quit',
        '-nographics',
        '-projectPath', $ProjectPath,
        '-buildTarget', 'Android',
        '-executeMethod', 'LostCyberHamster.Editor.LostCyberHamsterBuildAutomation.BuildAndroidApk',
        '-codexBuildOutput', $OutputDir,
        '-codexBuildDevelopment', $developmentValue,
        '-lostCyberHamsterAndroidSigningConfig', $SigningConfigPath,
        '-logFile', $LogPath
    )

    Write-Step "Starting Unity Android build. Unity log: $LogPath"
    $argumentLine = Convert-ToProcessArgumentLine -Arguments $unityArgs
    $process = Start-Process -FilePath $UnityExe -ArgumentList $argumentLine -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Unity Android build failed with exit code $($process.ExitCode). See log: $LogPath"
    }
}

$SourceWorktree = Get-FullPath -Path $SourceWorktree
$SandboxRoot = Get-FullPath -Path $SandboxRoot
$AndroidSigningConfigPath = if ([string]::IsNullOrWhiteSpace($AndroidSigningConfigPath)) {
    Get-DefaultAndroidSigningConfigPath
}
else {
    Get-FullPath -Path $AndroidSigningConfigPath
}

$sourceProjectPath = Join-Path $SourceWorktree $unityProjectRelativePath
$sandboxProjectPath = Join-Path $SandboxRoot $unityProjectRelativePath
$outputRoot = Join-Path $SourceWorktree 'Builds\telegram-buffer'

Assert-PathExists -Path $SourceWorktree -Description 'Source worktree'
Assert-PathExists -Path $sourceProjectPath -Description 'Source Unity project'
Assert-PathExists -Path $UnityExe -Description 'Unity executable'
Assert-PathExists -Path (Join-Path $sourceProjectPath 'ProjectSettings\EditorBuildSettings.asset') -Description 'Editor build settings'
$unityRoot = Split-Path -Parent (Split-Path -Parent $UnityExe)
Assert-PathExists -Path (Join-Path $unityRoot 'Editor\Data\PlaybackEngines\AndroidPlayer') -Description 'Unity Android module'
Assert-PathExists -Path (Join-Path $unityRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK') -Description 'Unity Android SDK'
Assert-PathExists -Path (Join-Path $unityRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\NDK') -Description 'Unity Android NDK'
Assert-PathExists -Path (Join-Path $unityRoot 'Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK') -Description 'Unity Android OpenJDK'
$androidSigning = Get-AndroidSigningMetadata -ConfigPath $AndroidSigningConfigPath

if ($SourceWorktree.TrimEnd('\', '/') -ieq $SandboxRoot.TrimEnd('\', '/')) {
    throw 'SourceWorktree and SandboxRoot must be different directories.'
}

New-Item -ItemType Directory -Force -Path $SandboxRoot | Out-Null
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

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
$outputDirStamp = Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'
$outputDir = Join-Path $outputRoot ("{0}_{1}_{2}" -f $outputDirStamp, (Get-SafeName -Value $branch), $safeSha)
$unityLogPath = Join-Path $outputDir 'unity-android.log'
$summaryPath = Join-Path $outputDir 'build-summary.codex.json'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Write-Step "Preparing sandbox: $SandboxRoot"
$sandboxProjectPath = Sync-UnityProjectSnapshot -SourceRoot $SourceWorktree -TargetRoot $SandboxRoot
if (-not $SkipUnityEditorReferenceCheck.IsPresent) {
    Test-UnityEditorReferences -UnityProjectPath $sandboxProjectPath
}

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
    androidSigningKeyAlias = $androidSigning.keyAliasName
    androidSigningCertificateSha256 = $androidSigning.certificateSha256
}

$manifestPath = Write-BuildManifest -SandboxProjectPath $sandboxProjectPath -Manifest ([pscustomobject]$manifest)
Write-Step "Build manifest written: $manifestPath"

Close-OpenUnityProjectEditors -UnityProjectPath $sandboxProjectPath
Invoke-UnityAndroidBuild `
    -ProjectPath $sandboxProjectPath `
    -OutputDir $outputDir `
    -LogPath $unityLogPath `
    -SigningConfigPath $AndroidSigningConfigPath

$apkPath = Join-Path $outputDir 'LostCyberHamster.apk'
if (-not (Test-Path -LiteralPath $apkPath)) {
    throw "Android build completed but APK was not found: $apkPath"
}

$apk = Get-Item -LiteralPath $apkPath
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
    androidSigningConfigPath = $AndroidSigningConfigPath
    androidSigningKeyAlias = $androidSigning.keyAliasName
    androidSigningCertificateSha256 = $androidSigning.certificateSha256
    unityLogPath = $unityLogPath
    buildHelperLog = $unityLogPath
    skillSummaryPath = $summaryPath
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

if ($Json.IsPresent) {
    [pscustomobject]$result | ConvertTo-Json -Depth 8
}
else {
    [pscustomobject]$result | Format-List
}
