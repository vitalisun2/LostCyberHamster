param(
    [string]$RepositoryRoot = '',
    [string]$ExpectedBranch = 'integration/unity-live',
    [string]$CodexExecutable = 'codex',
    [string]$Model = 'gpt-5.6-terra',
    [ValidateSet('', 'minimal', 'low', 'medium', 'high', 'xhigh')]
    [string]$ReasoningEffort = 'medium',
    [ValidateRange(1, 86400)]
    [int]$TimeoutSeconds = 7200,
    [string]$TelegramConfigPath = '',
    [string]$BotApiConfigPath = '',
    [string]$StateRoot = '',
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$contractVersion = 1
$fixedPrompt = @'
Run exactly one controlled command from the current repository root:

powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ".\tools\telegram-build-worker\run_build_and_publish.ps1" -RepositoryRoot "." -Json

Set the command tool timeout to at least 6900000 milliseconds. The command is a long-running Unity build plus APK upload and may be silent for several minutes.

Hard boundaries:
- Do not read files, call any other tool, construct replacement PowerShell, or retry the command.
- Never run git write commands or edit the source worktree.
- Never print tokens, config contents, environment secrets, chat ids, or private Telegram data.
- If the command exits nonzero, still read its single JSON stdout result.
- Return the helper JSON unchanged as the final structured result. Do not add commentary.
'@

$outputSchema = @'
{
  "type": "object",
  "additionalProperties": false,
  "required": [
    "contractVersion",
    "ok",
    "status",
    "errorCode",
    "errorMessage",
    "buildId",
    "apkPath",
    "apkSizeBytes",
    "buildSummaryPath",
    "sourceBranch",
    "sourceCommit",
    "sourceDirty",
    "sourceDiffHash",
    "telegramMessageId"
  ],
  "properties": {
    "contractVersion": { "type": "integer" },
    "ok": { "type": "boolean" },
    "status": {
      "type": "string",
      "enum": ["published", "failed"]
    },
    "errorCode": { "type": "string" },
    "errorMessage": { "type": "string" },
    "buildId": { "type": "string" },
    "apkPath": { "type": "string" },
    "apkSizeBytes": { "type": "integer" },
    "buildSummaryPath": { "type": "string" },
    "sourceBranch": { "type": "string" },
    "sourceCommit": { "type": "string" },
    "sourceDirty": { "type": "boolean" },
    "sourceDiffHash": { "type": "string" },
    "telegramMessageId": { "type": "string" }
  }
}
'@

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

function Invoke-GitText {
    param([string[]]$Arguments)

    $text = @(& git -C $RepositoryRoot -c core.safecrlf=false @Arguments 2>$null)
    if ($LASTEXITCODE -ne 0) {
        Stop-Workflow -Code 'GitPreflightFailed' -Message "git failed during repository preflight."
    }

    return ($text -join "`n").Trim()
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

function Get-FileSha256 {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return 'missing'
    }

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Text
    )

    [System.IO.File]::WriteAllText(
        $Path,
        $Text,
        [System.Text.UTF8Encoding]::new($false)
    )
}

function Get-RepositorySnapshot {
    $branch = Invoke-GitText -Arguments @('rev-parse', '--abbrev-ref', 'HEAD')
    $commit = Invoke-GitText -Arguments @('rev-parse', 'HEAD')
    $status = Invoke-GitText -Arguments @('status', '--porcelain=v1', '--untracked-files=all')
    $worktreeDiff = Invoke-GitText -Arguments @('diff', '--binary')
    $indexDiff = Invoke-GitText -Arguments @('diff', '--cached', '--binary')
    $untracked = Invoke-GitText -Arguments @('ls-files', '--others', '--exclude-standard')
    $stateMaterial = "STATUS`n$status`nWORKTREE_DIFF`n$worktreeDiff`nINDEX_DIFF`n$indexDiff"
    foreach ($relativePath in @($untracked -split "`n" | Where-Object { $_ })) {
        $stateMaterial += "`nUNTRACKED $relativePath $(Get-FileSha256 -Path (Join-Path $RepositoryRoot $relativePath))"
    }

    return [pscustomobject]@{
        branch = $branch
        commit = $commit
        dirty = -not [string]::IsNullOrWhiteSpace($status)
        statusHash = Get-TextSha256 -Text $stateMaterial
    }
}

function Resolve-CodexCommand {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        Stop-Workflow -Code 'CodexExecutableMissing' -Message 'CodexExecutable is empty.'
    }

    if ([System.IO.Path]::IsPathRooted($Value) -or $Value.Contains('\') -or $Value.Contains('/')) {
        $path = Get-FullPath -Path $Value
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            Stop-Workflow -Code 'CodexExecutableNotFound' -Message "Codex executable not found: $path"
        }

        return $path
    }

    $names = if ($Value -ieq 'codex') { @('codex.cmd', 'codex.exe', 'codex') } else { @($Value) }
    foreach ($name in $names) {
        $command = Get-Command -Name $name -CommandType Application,ExternalScript -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($null -ne $command) {
            if (-not [string]::IsNullOrWhiteSpace($command.Source)) {
                return $command.Source
            }

            return $command.Definition
        }
    }

    Stop-Workflow -Code 'CodexExecutableNotFound' -Message "Codex executable not found: $Value"
}

function Convert-ToProcessArgument {
    param([string]$Value)

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }

    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Convert-ToProcessArgumentLine {
    param([string[]]$Arguments)

    return (($Arguments | ForEach-Object { Convert-ToProcessArgument -Value $_ }) -join ' ')
}

function Get-WorkflowErrorCode {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    if ($ErrorRecord.Exception.Data.Contains('WorkflowErrorCode')) {
        return [string]$ErrorRecord.Exception.Data['WorkflowErrorCode']
    }

    if ($ErrorRecord.Exception -is [System.UnauthorizedAccessException] -or
        $ErrorRecord.Exception.Message -match '(?i)access is denied') {
        return 'CodexExecutableAccessDenied'
    }

    return 'UnhandledError'
}

$startedAt = (Get-Date).ToUniversalTime()
$runId = '{0}_{1}' -f (Get-Date -Format 'yyyyMMdd_HHmmss'), ([guid]::NewGuid().ToString('N').Substring(0, 8))
$runDirectory = ''
$resultPath = ''
$lockStream = $null
$process = $null
$exitStatus = 1
$snapshotBefore = $null
$resolvedCodexExecutable = ''
$plannedArguments = @()
$hadTelegramConfigEnvironment = Test-Path Env:TELEGRAM_BUFFER_CONFIG
$previousTelegramConfigEnvironment = if ($hadTelegramConfigEnvironment) { $env:TELEGRAM_BUFFER_CONFIG } else { $null }
$hadBotApiConfigEnvironment = Test-Path Env:TELEGRAM_LOCAL_BOT_API_CONFIG
$previousBotApiConfigEnvironment = if ($hadBotApiConfigEnvironment) { $env:TELEGRAM_LOCAL_BOT_API_CONFIG } else { $null }

$result = [ordered]@{
    contractVersion = $contractVersion
    ok = $false
    status = 'failed'
    errorCode = ''
    errorMessage = ''
    runId = $runId
    repositoryRoot = ''
    expectedBranch = $ExpectedBranch
    sourceBranch = ''
    sourceCommit = ''
    sourceDirty = $false
    sourceStatusHash = ''
    codexExecutable = ''
    codexExitCode = $null
    timedOut = $false
    startedAtUtc = $startedAt.ToString('o')
    completedAtUtc = ''
    durationSeconds = 0
    runDirectory = ''
    stdoutLogPath = ''
    stderrLogPath = ''
    finalOutputPath = ''
    resultPath = ''
    promptSha256 = Get-TextSha256 -Text $fixedPrompt
    model = $Model
    reasoningEffort = $ReasoningEffort
    plannedArguments = @()
    agentResult = $null
}

try {
    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = Join-Path $PSScriptRoot '..\..'
    }

    $RepositoryRoot = Get-FullPath -Path $RepositoryRoot
    $result.repositoryRoot = $RepositoryRoot
    if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
        Stop-Workflow -Code 'RepositoryNotFound' -Message "Repository root not found: $RepositoryRoot"
    }

    $gitRoot = Get-FullPath -Path (Invoke-GitText -Arguments @('rev-parse', '--show-toplevel'))
    if (-not $gitRoot.Equals($RepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        Stop-Workflow -Code 'RepositoryRootMismatch' -Message 'RepositoryRoot must be the Git worktree root.'
    }

    if ([string]::IsNullOrWhiteSpace($StateRoot)) {
        $localAppData = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
        if ([string]::IsNullOrWhiteSpace($localAppData)) {
            Stop-Workflow -Code 'StateRootUnavailable' -Message 'Local application data path is unavailable.'
        }

        $StateRoot = Join-Path $localAppData 'LostCyberHamster\TelegramBuildWorker'
    }

    $StateRoot = Get-FullPath -Path $StateRoot
    if (Test-PathInside -Child $StateRoot -Parent $RepositoryRoot) {
        Stop-Workflow -Code 'StateRootInsideRepository' -Message 'StateRoot must be outside the Git worktree.'
    }

    $runDirectory = Join-Path $StateRoot (Join-Path 'runs' $runId)
    New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
    $result.runDirectory = $runDirectory

    $stdoutLogPath = Join-Path $runDirectory 'codex.stdout.jsonl'
    $stderrLogPath = Join-Path $runDirectory 'codex.stderr.log'
    $promptPath = Join-Path $runDirectory 'prompt.txt'
    $schemaPath = Join-Path $runDirectory 'result.schema.json'
    $finalOutputPath = Join-Path $runDirectory 'codex.final.json'
    $resultPath = Join-Path $runDirectory 'invoke-result.json'
    $result.stdoutLogPath = $stdoutLogPath
    $result.stderrLogPath = $stderrLogPath
    $result.finalOutputPath = $finalOutputPath
    $result.resultPath = $resultPath

    $lockPath = Join-Path $StateRoot 'invoke_codex_build.lock'
    try {
        $lockStream = [System.IO.File]::Open(
            $lockPath,
            [System.IO.FileMode]::OpenOrCreate,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None
        )
    }
    catch [System.IO.IOException] {
        Stop-Workflow -Code 'WorkflowBusy' -Message 'Another Codex build workflow is already running.'
    }

    $snapshotBefore = Get-RepositorySnapshot
    $result.sourceBranch = $snapshotBefore.branch
    $result.sourceCommit = $snapshotBefore.commit
    $result.sourceDirty = $snapshotBefore.dirty
    $result.sourceStatusHash = $snapshotBefore.statusHash

    if ($snapshotBefore.branch -cne $ExpectedBranch) {
        Stop-Workflow `
            -Code 'UnexpectedBranch' `
            -Message "Expected branch '$ExpectedBranch', found '$($snapshotBefore.branch)'."
    }

    if ($ExpectedBranch -cne 'integration/unity-live') {
        Stop-Workflow `
            -Code 'UnsupportedBranchContract' `
            -Message "This controlled workflow only supports branch 'integration/unity-live'."
    }

    if (-not [string]::IsNullOrWhiteSpace($TelegramConfigPath)) {
        $TelegramConfigPath = Get-FullPath -Path $TelegramConfigPath
        if (-not (Test-Path -LiteralPath $TelegramConfigPath -PathType Leaf)) {
            Stop-Workflow -Code 'TelegramConfigNotFound' -Message 'Telegram config file does not exist.'
        }
        $env:TELEGRAM_BUFFER_CONFIG = $TelegramConfigPath
    }

    if (-not [string]::IsNullOrWhiteSpace($BotApiConfigPath)) {
        $BotApiConfigPath = Get-FullPath -Path $BotApiConfigPath
        if (-not (Test-Path -LiteralPath $BotApiConfigPath -PathType Leaf)) {
            Stop-Workflow -Code 'BotApiConfigNotFound' -Message 'Local Bot API config file does not exist.'
        }
        $env:TELEGRAM_LOCAL_BOT_API_CONFIG = $BotApiConfigPath
    }

    $resolvedCodexExecutable = Resolve-CodexCommand -Value $CodexExecutable
    $result.codexExecutable = $resolvedCodexExecutable

    Write-Utf8NoBom -Path $promptPath -Text $fixedPrompt
    Write-Utf8NoBom -Path $schemaPath -Text $outputSchema

    $plannedArguments = @(
        '--sandbox', 'danger-full-access',
        '--ask-for-approval', 'never',
        '--cd', $RepositoryRoot,
        'exec',
        '--ephemeral',
        '--json',
        '--ignore-user-config',
        '--output-schema', $schemaPath,
        '--output-last-message', $finalOutputPath,
        '-'
    )
    if (-not [string]::IsNullOrWhiteSpace($Model)) {
        $plannedArguments = @('--model', $Model) + $plannedArguments
    }
    if (-not [string]::IsNullOrWhiteSpace($ReasoningEffort)) {
        $plannedArguments = @('--config', "model_reasoning_effort=$ReasoningEffort") + $plannedArguments
    }
    $result.plannedArguments = $plannedArguments

    if ($DryRun.IsPresent) {
        $result.ok = $true
        $result.status = 'dry_run'
        $result.errorCode = if ($resolvedCodexExecutable -match '(?i)\\WindowsApps\\OpenAI\.Codex_') {
            'DesktopAppAliasDetected'
        }
        else {
            ''
        }
        $result.errorMessage = if ($result.errorCode) {
            'Resolved codex points to the Desktop AppX package; scheduled headless execution may fail. Install standalone Codex CLI or inject codex.cmd/codex.exe.'
        }
        else {
            ''
        }
        $exitStatus = 0
    }
    else {
        $argumentLine = Convert-ToProcessArgumentLine -Arguments $plannedArguments
        try {
            $process = Start-Process `
                -FilePath $resolvedCodexExecutable `
                -ArgumentList $argumentLine `
                -WorkingDirectory $RepositoryRoot `
                -RedirectStandardInput $promptPath `
                -RedirectStandardOutput $stdoutLogPath `
                -RedirectStandardError $stderrLogPath `
                -WindowStyle Hidden `
                -PassThru
            [void]$process.Handle
        }
        catch {
            if ($_.Exception -is [System.UnauthorizedAccessException] -or
                $_.Exception.Message -match '(?i)access is denied') {
                Stop-Workflow `
                    -Code 'CodexExecutableAccessDenied' `
                    -Message 'Codex CLI could not start: access denied. Desktop AppX alias is not a usable scheduled CLI; install standalone Codex CLI or pass its path with -CodexExecutable.'
            }

            throw
        }

        $completedInTime = $process.WaitForExit($TimeoutSeconds * 1000)
        if (-not $completedInTime) {
            $result.timedOut = $true
            if (-not $process.HasExited) {
                & taskkill.exe /PID $process.Id /T /F *> $null
                $terminated = $process.WaitForExit(15000)
                if (-not $terminated) {
                    Stop-Workflow -Code 'CodexTerminationFailed' -Message 'Timed-out Codex process could not be terminated.'
                }
            }

            Stop-Workflow -Code 'CodexTimedOut' -Message "Codex build workflow exceeded timeout of $TimeoutSeconds seconds."
        }

        $process.WaitForExit()
        $result.codexExitCode = $process.ExitCode
        if ($process.ExitCode -ne 0) {
            Stop-Workflow `
                -Code 'CodexFailed' `
                -Message "Codex build workflow exited with code $($process.ExitCode). See external logs."
        }

        if (-not (Test-Path -LiteralPath $finalOutputPath -PathType Leaf)) {
            Stop-Workflow -Code 'CodexResultMissing' -Message 'Codex completed without a structured final result.'
        }

        try {
            $agentResult = Get-Content -LiteralPath $finalOutputPath -Raw -Encoding UTF8 | ConvertFrom-Json
        }
        catch {
            Stop-Workflow -Code 'CodexResultInvalid' -Message 'Codex final result is not valid JSON.'
        }

        $result.agentResult = $agentResult
        if ($agentResult.status -ne 'published') {
            $agentErrorCode = [string]$agentResult.errorCode
            if ([string]::IsNullOrWhiteSpace($agentErrorCode)) {
                $agentErrorCode = 'BuildOrPublishFailed'
            }
            $agentErrorMessage = [string]$agentResult.errorMessage
            if ([string]::IsNullOrWhiteSpace($agentErrorMessage)) {
                $agentErrorMessage = 'Agent reported build or Telegram publication failure. See external logs.'
            }
            Stop-Workflow `
                -Code $agentErrorCode `
                -Message $agentErrorMessage
        }

        if ($agentResult.sourceBranch -cne $ExpectedBranch) {
            Stop-Workflow -Code 'AgentBranchMismatch' -Message 'Agent result contains an unexpected source branch.'
        }

        if (-not (Test-Path -LiteralPath $agentResult.apkPath -PathType Leaf)) {
            Stop-Workflow -Code 'PublishedApkMissing' -Message 'Published APK path from agent result does not exist.'
        }

        $allowedApkRoot = Join-Path $RepositoryRoot 'Builds\telegram-buffer'
        if (-not (Test-PathInside -Child $agentResult.apkPath -Parent $allowedApkRoot)) {
            Stop-Workflow -Code 'PublishedApkOutsideBuilds' -Message 'Agent result APK is outside Builds/telegram-buffer.'
        }

        if ([string]::IsNullOrWhiteSpace([string]$agentResult.telegramMessageId)) {
            Stop-Workflow -Code 'TelegramConfirmationMissing' -Message 'Telegram publication has no message id.'
        }

        $result.ok = $true
        $result.status = 'published'
        $exitStatus = 0
    }
}
catch {
    $result.ok = $false
    $result.status = 'failed'
    $result.errorCode = Get-WorkflowErrorCode -ErrorRecord $_
    $result.errorMessage = $_.Exception.Message
}
finally {
    if ($null -ne $process) {
        $process.Dispose()
    }
    if ($null -ne $lockStream) {
        $lockStream.Dispose()
    }
    if ($hadTelegramConfigEnvironment) {
        $env:TELEGRAM_BUFFER_CONFIG = $previousTelegramConfigEnvironment
    }
    else {
        Remove-Item Env:TELEGRAM_BUFFER_CONFIG -ErrorAction SilentlyContinue
    }
    if ($hadBotApiConfigEnvironment) {
        $env:TELEGRAM_LOCAL_BOT_API_CONFIG = $previousBotApiConfigEnvironment
    }
    else {
        Remove-Item Env:TELEGRAM_LOCAL_BOT_API_CONFIG -ErrorAction SilentlyContinue
    }

    $completedAt = (Get-Date).ToUniversalTime()
    $result.completedAtUtc = $completedAt.ToString('o')
    $result.durationSeconds = [math]::Round(($completedAt - $startedAt).TotalSeconds, 3)
}

$resultJson = [pscustomobject]$result | ConvertTo-Json -Depth 12 -Compress
if (-not [string]::IsNullOrWhiteSpace($resultPath)) {
    try {
        $resultJson | Set-Content -LiteralPath $resultPath -Encoding UTF8
    }
    catch {
        if ($result.ok) {
            $result.ok = $false
            $result.status = 'failed'
            $result.errorCode = 'ResultPersistenceFailed'
            $result.errorMessage = 'Could not persist machine-readable workflow result.'
            $exitStatus = 1
            $resultJson = [pscustomobject]$result | ConvertTo-Json -Depth 12 -Compress
        }
    }
}

Write-Output $resultJson
exit $exitStatus
