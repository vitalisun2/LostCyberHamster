param(
    [string]$RepositoryRoot = '',
    [string]$SandboxRoot = 'C:\BuildWorkspaces\LostCyberHamster_Android',
    [string]$BuildLabel = 'telegram-worker',
    [string]$TelegramConfigPath = '',
    [string]$SkillRoot = '',
    [switch]$ShowDevelopmentConsole,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedBranch = 'integration/unity-live'
$result = [ordered]@{
    contractVersion = 1
    ok = $false
    status = 'failed'
    errorCode = ''
    errorMessage = ''
    buildId = ''
    apkPath = ''
    apkSizeBytes = 0
    buildSummaryPath = ''
    sourceBranch = ''
    sourceCommit = ''
    sourceDirty = $false
    sourceDiffHash = ''
    telegramMessageId = ''
}

function Stop-Workflow {
    param(
        [string]$Code,
        [string]$Message
    )

    $exception = [System.InvalidOperationException]::new($Message)
    $exception.Data['WorkflowErrorCode'] = $Code
    throw $exception
}

function Get-FullPath {
    param([string]$Path)

    return [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
}

function Test-PathInside {
    param(
        [string]$Child,
        [string]$Parent
    )

    $childPath = Get-FullPath -Path $Child
    $parentPath = Get-FullPath -Path $Parent
    return $childPath.Equals($parentPath, [System.StringComparison]::OrdinalIgnoreCase) -or
        $childPath.StartsWith(
            $parentPath + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase
        )
}

function Invoke-GitLines {
    param([string[]]$Arguments)

    $lines = @(& git -C $RepositoryRoot -c core.safecrlf=false @Arguments 2>$null)
    $gitExitCode = $LASTEXITCODE
    if ($gitExitCode -ne 0) {
        Stop-Workflow -Code 'GitReadFailed' -Message 'Git repository preflight failed.'
    }

    return $lines
}

function Get-FileSha256 {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ''
    }

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-TextSha256 {
    param([string]$Text)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-SourceDiffHash {
    $builder = New-Object System.Text.StringBuilder
    $status = @(Invoke-GitLines -Arguments @('status', '--short'))
    $worktreeDiff = @(Invoke-GitLines -Arguments @('diff', '--binary'))
    $untracked = @(Invoke-GitLines -Arguments @('ls-files', '--others', '--exclude-standard'))

    [void]$builder.AppendLine($status -join "`n")
    [void]$builder.AppendLine($worktreeDiff -join "`n")
    foreach ($relativePath in $untracked) {
        $fullPath = Join-Path $RepositoryRoot $relativePath
        [void]$builder.AppendLine("UNTRACKED $relativePath $(Get-FileSha256 -Path $fullPath)")
    }

    return Get-TextSha256 -Text $builder.ToString()
}

function Get-SourceSnapshot {
    $branch = (Invoke-GitLines -Arguments @('rev-parse', '--abbrev-ref', 'HEAD') | Select-Object -First 1)
    $fullCommit = (Invoke-GitLines -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1)
    $shortCommit = (Invoke-GitLines -Arguments @('rev-parse', '--short', 'HEAD') | Select-Object -First 1)
    $status = @(Invoke-GitLines -Arguments @('status', '--short'))
    $sourceDiffHash = Get-SourceDiffHash
    $indexDiff = @(Invoke-GitLines -Arguments @('diff', '--cached', '--binary')) -join "`n"
    $guardHash = Get-TextSha256 -Text "$branch`n$fullCommit`n$sourceDiffHash`n$indexDiff"

    return [pscustomobject]@{
        branch = [string]$branch
        fullCommit = [string]$fullCommit
        shortCommit = [string]$shortCommit
        dirty = $status.Count -gt 0
        sourceDiffHash = $sourceDiffHash
        guardHash = $guardHash
    }
}

function Assert-Equal {
    param(
        [object]$Actual,
        [object]$Expected,
        [string]$Code,
        [string]$Message
    )

    if ([string]$Actual -cne [string]$Expected) {
        Stop-Workflow -Code $Code -Message $Message
    }
}

function Test-EndpointReachable {
    param([string]$ApiBaseUrl)

    $uri = $null
    try {
        $uri = [System.Uri]$ApiBaseUrl
        $port = $uri.Port
        $client = [System.Net.Sockets.TcpClient]::new()
        try {
            $connect = $client.BeginConnect($uri.Host, $port, $null, $null)
            if (-not $connect.AsyncWaitHandle.WaitOne(3000)) {
                return $false
            }
            $client.EndConnect($connect)
            return $true
        }
        finally {
            $client.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Test-LocalBotApi {
    param(
        [string]$TestScript,
        [string]$LocalConfigPath
    )

    try {
        & $TestScript -ConfigPath $LocalConfigPath *> $null
        return $true
    }
    catch {
        return $false
    }
}

$previousTelegramConfig = $null
$hadTelegramConfigEnvironment = Test-Path Env:TELEGRAM_BUFFER_CONFIG
if ($hadTelegramConfigEnvironment) {
    $previousTelegramConfig = $env:TELEGRAM_BUFFER_CONFIG
}

try {
    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = Join-Path $PSScriptRoot '..\..'
    }

    $RepositoryRoot = Get-FullPath -Path $RepositoryRoot
    $SandboxRoot = Get-FullPath -Path $SandboxRoot
    if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
        Stop-Workflow -Code 'RepositoryNotFound' -Message 'Repository root does not exist.'
    }

    $gitRoot = Get-FullPath -Path ((Invoke-GitLines -Arguments @('rev-parse', '--show-toplevel')) | Select-Object -First 1)
    Assert-Equal `
        -Actual $gitRoot `
        -Expected $RepositoryRoot `
        -Code 'RepositoryRootMismatch' `
        -Message 'RepositoryRoot must be the Git worktree root.'

    if ([string]::IsNullOrWhiteSpace($SkillRoot)) {
        $userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
        $SkillRoot = Join-Path $userProfile '.codex\skills\publish-build-to-telegram-buffer'
    }
    $SkillRoot = Get-FullPath -Path $SkillRoot

    if (-not [string]::IsNullOrWhiteSpace($TelegramConfigPath)) {
        $TelegramConfigPath = Get-FullPath -Path $TelegramConfigPath
        if (-not (Test-Path -LiteralPath $TelegramConfigPath -PathType Leaf)) {
            Stop-Workflow -Code 'TelegramConfigNotFound' -Message 'Telegram config file does not exist.'
        }
        $env:TELEGRAM_BUFFER_CONFIG = $TelegramConfigPath
    }

    $buildScript = Join-Path $RepositoryRoot 'tools\build\build_android_telegram.ps1'
    $testBotScript = Join-Path $SkillRoot 'scripts\test_local_bot_api_server.ps1'
    $startBotScript = Join-Path $SkillRoot 'scripts\start_local_bot_api_server.ps1'
    $publishScript = Join-Path $SkillRoot 'scripts\publish_latest_apk_to_telegram_buffer.ps1'
    $localConfigScript = Join-Path $SkillRoot 'scripts\local_bot_api_config.ps1'
    $telegramConfigScript = Join-Path $SkillRoot 'scripts\telegram_config.ps1'
    foreach ($requiredScript in @(
        $buildScript,
        $testBotScript,
        $startBotScript,
        $publishScript,
        $localConfigScript,
        $telegramConfigScript
    )) {
        if (-not (Test-Path -LiteralPath $requiredScript -PathType Leaf)) {
            Stop-Workflow -Code 'RequiredScriptNotFound' -Message 'Required build or Telegram helper is missing.'
        }
    }

    . $localConfigScript
    . $telegramConfigScript
    $localBotApiConfig = Read-LocalBotApiConfig
    $telegramBufferConfig = Read-TelegramBufferConfig
    $localBotApiEndpoint = ([string]$localBotApiConfig.ApiBaseUrl).TrimEnd('/')
    $telegramEndpoint = ([string]$telegramBufferConfig.ApiBaseUrl).TrimEnd('/')
    if (-not $localBotApiEndpoint.Equals(
        $telegramEndpoint,
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        Stop-Workflow `
            -Code 'BotApiEndpointMismatch' `
            -Message 'Telegram publish endpoint must match the configured local Bot API endpoint.'
    }

    $localEndpointUri = [System.Uri]$localBotApiEndpoint
    if (-not $localEndpointUri.IsLoopback) {
        Stop-Workflow `
            -Code 'BotApiEndpointNotLocal' `
            -Message 'The configured Telegram Bot API endpoint must be local to this build stand.'
    }

    $snapshotBefore = Get-SourceSnapshot
    $result.sourceBranch = $snapshotBefore.branch
    $result.sourceCommit = $snapshotBefore.shortCommit
    $result.sourceDirty = $snapshotBefore.dirty
    $result.sourceDiffHash = $snapshotBefore.sourceDiffHash

    Assert-Equal `
        -Actual $snapshotBefore.branch `
        -Expected $expectedBranch `
        -Code 'UnexpectedBranch' `
        -Message "Checked-out branch must be '$expectedBranch'."

    try {
        $buildOutput = @(
            & $buildScript `
                -SourceWorktree $RepositoryRoot `
                -SandboxRoot $SandboxRoot `
                -BuildLabel $BuildLabel `
                -Development `
                -ShowDevelopmentConsole:$ShowDevelopmentConsole.IsPresent `
                -Json `
                2>$null 3>$null 4>$null 5>$null 6>$null
        )
    }
    catch {
        Stop-Workflow -Code 'BuildFailed' -Message 'Android build failed. See build summary and Unity logs.'
    }

    try {
        $build = ($buildOutput -join "`n") | ConvertFrom-Json
    }
    catch {
        Stop-Workflow -Code 'BuildResultInvalid' -Message 'Build entrypoint returned invalid JSON.'
    }

    foreach ($requiredProperty in @(
        'buildId',
        'sourceBranch',
        'sourceCommit',
        'sourceDirty',
        'sourceDiffHash',
        'apk',
        'apkSizeBytes',
        'skillSummaryPath'
    )) {
        if ($null -eq $build.PSObject.Properties[$requiredProperty]) {
            Stop-Workflow -Code 'BuildResultIncomplete' -Message 'Build result is missing required metadata.'
        }
    }

    Assert-Equal -Actual $build.sourceBranch -Expected $expectedBranch `
        -Code 'BuildBranchMismatch' -Message 'Stable build snapshot is not from the expected branch.'
    if ([string]::IsNullOrWhiteSpace([string]$build.sourceCommit) -or
        [string]::IsNullOrWhiteSpace([string]$build.sourceDiffHash)) {
        Stop-Workflow `
            -Code 'BuildSnapshotIncomplete' `
            -Message 'Build result does not identify its stable source snapshot.'
    }

    # The build script captures and verifies a stable snapshot around sandbox
    # synchronization. Edits made after that snapshot belong to the next build.
    $result.sourceBranch = [string]$build.sourceBranch
    $result.sourceCommit = [string]$build.sourceCommit
    $result.sourceDirty = [bool]$build.sourceDirty
    $result.sourceDiffHash = [string]$build.sourceDiffHash

    $apkPath = Get-FullPath -Path ([string]$build.apk)
    $allowedApkRoot = Join-Path $RepositoryRoot 'Builds\telegram-buffer'
    if (-not (Test-Path -LiteralPath $apkPath -PathType Leaf) -or
        -not (Test-PathInside -Child $apkPath -Parent $allowedApkRoot)) {
        Stop-Workflow -Code 'BuildApkInvalid' -Message 'Built APK is missing or outside Builds/telegram-buffer.'
    }

    $summaryPath = Get-FullPath -Path ([string]$build.skillSummaryPath)
    if (-not (Test-Path -LiteralPath $summaryPath -PathType Leaf) -or
        -not (Test-PathInside -Child $summaryPath -Parent $allowedApkRoot)) {
        Stop-Workflow -Code 'BuildSummaryInvalid' -Message 'Build summary is missing or outside Builds/telegram-buffer.'
    }

    try {
        $summary = Get-Content -LiteralPath $summaryPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        Stop-Workflow -Code 'BuildSummaryInvalid' -Message 'Build summary is not valid JSON.'
    }

    Assert-Equal -Actual $summary.buildId -Expected $build.buildId `
        -Code 'BuildSummaryMismatch' -Message 'Build summary buildId does not match build result.'
    Assert-Equal -Actual $summary.sourceBranch -Expected $build.sourceBranch `
        -Code 'BuildSummaryMismatch' -Message 'Build summary branch does not match build result.'
    Assert-Equal -Actual $summary.sourceCommit -Expected $build.sourceCommit `
        -Code 'BuildSummaryMismatch' -Message 'Build summary commit does not match build result.'
    Assert-Equal -Actual ([bool]$summary.sourceDirty) -Expected ([bool]$build.sourceDirty) `
        -Code 'BuildSummaryMismatch' -Message 'Build summary dirty state does not match build result.'
    Assert-Equal -Actual $summary.sourceDiffHash -Expected $build.sourceDiffHash `
        -Code 'BuildSummaryMismatch' -Message 'Build summary diff hash does not match build result.'
    Assert-Equal -Actual (Get-FullPath -Path ([string]$summary.apk)) -Expected $apkPath `
        -Code 'BuildSummaryMismatch' -Message 'Build summary APK does not match build result.'

    $result.buildId = [string]$build.buildId
    $result.apkPath = $apkPath
    $result.apkSizeBytes = [long](Get-Item -LiteralPath $apkPath).Length
    $result.buildSummaryPath = $summaryPath

    $safeBuildId = ([string]$build.buildId -replace '[^A-Za-z0-9_.-]', '_').Trim('_')
    if ([string]::IsNullOrWhiteSpace($safeBuildId)) {
        Stop-Workflow -Code 'TelegramArtifactNameInvalid' -Message 'BuildId cannot produce a safe Telegram artifact name.'
    }

    # Telegram receives a unique filename for every build. Reusing the constant
    # LostCyberHamster.apk name makes old client-side downloads indistinguishable
    # from a fresh APK, especially while size and Android version stay unchanged.
    $publishApkPath = Join-Path `
        (Split-Path -Parent $apkPath) `
        "LostCyberHamster_$safeBuildId.apk"
    if (Test-Path -LiteralPath $publishApkPath) {
        Stop-Workflow -Code 'TelegramArtifactAlreadyExists' -Message 'Unique Telegram APK path already exists.'
    }

    try {
        New-Item -ItemType HardLink -Path $publishApkPath -Target $apkPath -ErrorAction Stop | Out-Null
    }
    catch {
        try {
            Copy-Item -LiteralPath $apkPath -Destination $publishApkPath -ErrorAction Stop
        }
        catch {
            Stop-Workflow -Code 'TelegramArtifactPreparationFailed' -Message 'Could not prepare the uniquely named Telegram APK.'
        }
    }

    $apkSha256 = Get-FileSha256 -Path $apkPath
    if ((Get-Item -LiteralPath $publishApkPath).Length -ne $result.apkSizeBytes -or
        (Get-FileSha256 -Path $publishApkPath) -cne $apkSha256) {
        Stop-Workflow -Code 'TelegramArtifactMismatch' -Message 'Uniquely named Telegram APK does not match the built artifact.'
    }

    $botApiReachable = Test-EndpointReachable -ApiBaseUrl $localBotApiEndpoint
    if (-not $botApiReachable) {
        try {
            & $startBotScript `
                -ConfigPath $localBotApiConfig.ConfigPath `
                -NoUpdateTelegramBufferConfig `
                *> $null
        }
        catch {
            Stop-Workflow -Code 'BotApiStartFailed' -Message 'Local Telegram Bot API could not be started.'
        }

        if (-not (Test-LocalBotApi `
            -TestScript $testBotScript `
            -LocalConfigPath $localBotApiConfig.ConfigPath
        )) {
            Stop-Workflow -Code 'BotApiUnavailable' -Message 'Local Telegram Bot API is unavailable after one start attempt.'
        }
    }
    elseif (-not (Test-LocalBotApi `
        -TestScript $testBotScript `
        -LocalConfigPath $localBotApiConfig.ConfigPath
    )) {
        Stop-Workflow `
            -Code 'BotApiValidationFailed' `
            -Message 'Local Telegram Bot API is reachable, but bot or channel validation failed.'
    }

    $caption = @(
        'LostCyberHamster APK'
        "BuildId: $($build.buildId)"
        "Branch: $($build.sourceBranch)"
        "Commit: $($build.sourceCommit)"
        "Dirty tree: $($build.sourceDirty)"
        "Diff hash: $($build.sourceDiffHash)"
        "Development console: $($build.developmentConsoleVisible)"
        "Artifact: $(Split-Path -Leaf $publishApkPath)"
        "APK SHA-256: $apkSha256"
    ) -join "`n"

    try {
        $publishArguments = @{
            RepositoryRoot = $RepositoryRoot
            ApkPath = $publishApkPath
            Caption = $caption
        }
        if (-not [string]::IsNullOrWhiteSpace($TelegramConfigPath)) {
            $publishArguments.ConfigPath = $TelegramConfigPath
        }

        $publishOutput = @(& $publishScript @publishArguments 2>$null 3>$null 4>$null 5>$null 6>$null)
    }
    catch {
        Stop-Workflow -Code 'TelegramPublishFailed' -Message 'Telegram Bot API upload failed.'
    }

    try {
        $telegram = ($publishOutput -join "`n") | ConvertFrom-Json
    }
    catch {
        Stop-Workflow -Code 'TelegramResultInvalid' -Message 'Telegram publisher returned invalid JSON.'
    }

    if ($telegram.ok -ne $true -or
        $null -eq $telegram.result -or
        $null -eq $telegram.result.message_id) {
        Stop-Workflow -Code 'TelegramPublishUnconfirmed' -Message 'Telegram Bot API did not confirm the upload.'
    }

    $result.telegramMessageId = [string]$telegram.result.message_id
    $result.ok = $true
    $result.status = 'published'
}
catch {
    $result.ok = $false
    $result.status = 'failed'
    if ($_.Exception.Data.Contains('WorkflowErrorCode')) {
        $result.errorCode = [string]$_.Exception.Data['WorkflowErrorCode']
        $result.errorMessage = $_.Exception.Message
    }
    else {
        $result.errorCode = 'UnexpectedFailure'
        $result.errorMessage = 'Unexpected build-and-publish workflow failure.'
    }
}
finally {
    if ($hadTelegramConfigEnvironment) {
        $env:TELEGRAM_BUFFER_CONFIG = $previousTelegramConfig
    }
    else {
        Remove-Item Env:TELEGRAM_BUFFER_CONFIG -ErrorAction SilentlyContinue
    }
}

$result | ConvertTo-Json -Depth 8 -Compress
if ($result.ok) {
    exit 0
}
exit 1
