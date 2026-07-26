[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [ValidatePattern('^[^\\/:*?"<>|]+$')]
    [string]$TaskName = 'LostCyberHamster Telegram Build Worker',

    [string]$RepositoryRoot,

    [string]$TelegramConfigPath,

    [string]$BotApiConfigPath,

    [string]$StateDirectory,

    [ValidateRange(1, 50)]
    [int]$RestartCount = 5,

    [ValidateRange(1, 60)]
    [int]$RestartIntervalMinutes = 1,

    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-RequiredFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description not found: $Path"
    }

    $item = Get-Item -LiteralPath $Path
    if ($item.Length -eq 0) {
        throw "$Description is empty: $Path"
    }

    return $item.FullName
}

function Quote-TaskArgument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ($Value.Contains('"')) {
        throw 'Task argument must not contain a quote character.'
    }

    return '"' + $Value + '"'
}

if ([string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
    throw 'USERPROFILE is not available.'
}

if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    throw 'LOCALAPPDATA is not available.'
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path $PSScriptRoot '..\..'
}
if ([string]::IsNullOrWhiteSpace($TelegramConfigPath)) {
    $TelegramConfigPath = Join-Path $env:USERPROFILE '.codex\telegram-buffer.local.json'
}
if ([string]::IsNullOrWhiteSpace($BotApiConfigPath)) {
    $BotApiConfigPath = Join-Path $env:USERPROFILE '.codex\telegram-bot-api.local.json'
}
if ([string]::IsNullOrWhiteSpace($StateDirectory)) {
    $StateDirectory = Join-Path $env:LOCALAPPDATA 'LostCyberHamster\TelegramBuildWorker'
}

$workerPath = Resolve-RequiredFile `
    -Path (Join-Path $PSScriptRoot 'telegram_build_worker.ps1') `
    -Description 'Worker script'
$hiddenLauncherPath = Resolve-RequiredFile `
    -Path (Join-Path $PSScriptRoot 'launch_worker_hidden.vbs') `
    -Description 'Hidden worker launcher'
$dispatchPath = Resolve-RequiredFile `
    -Path (Join-Path $PSScriptRoot 'invoke_codex_build.ps1') `
    -Description 'Codex dispatch script'
$resolvedTelegramConfigPath = Resolve-RequiredFile `
    -Path $TelegramConfigPath `
    -Description 'Telegram delivery config'
$null = Resolve-RequiredFile `
    -Path $BotApiConfigPath `
    -Description 'Local Telegram Bot API config'

if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
    throw "Repository root not found: $RepositoryRoot"
}
$resolvedRepositoryRoot = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$resolvedBotApiConfigPath = (Resolve-Path -LiteralPath $BotApiConfigPath).Path

$codexCommand = Get-Command -Name 'codex.cmd' -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $codexCommand) {
    throw 'Standalone Codex CLI is missing. Install it with: npm install --global @openai/codex'
}
if ($codexCommand.Source -match '(?i)\\WindowsApps\\OpenAI\.Codex_') {
    throw 'Codex Desktop App alias cannot run headlessly. Install standalone Codex CLI with: npm install --global @openai/codex'
}

$powershellPath = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
if (-not (Test-Path -LiteralPath $powershellPath -PathType Leaf)) {
    throw "Windows PowerShell not found: $powershellPath"
}
$wscriptPath = Join-Path $env:WINDIR 'System32\wscript.exe'
if (-not (Test-Path -LiteralPath $wscriptPath -PathType Leaf)) {
    throw "Windows Script Host not found: $wscriptPath"
}

$resolvedStateDirectory = [System.IO.Path]::GetFullPath($StateDirectory)
$statePath = Join-Path $resolvedStateDirectory 'state.json'
$repositoryPrefix = $resolvedRepositoryRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
if ($resolvedStateDirectory.Equals($resolvedRepositoryRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $resolvedStateDirectory.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'StateDirectory must be outside the repository.'
}

if (-not (Test-Path -LiteralPath $resolvedStateDirectory -PathType Container)) {
    if ($PSCmdlet.ShouldProcess($resolvedStateDirectory, 'Create worker state directory')) {
        $null = New-Item -ItemType Directory -Path $resolvedStateDirectory -Force
    }
}

$workerArgumentValues = @(
    '-NoLogo'
    '-NoProfile'
    '-NonInteractive'
    '-ExecutionPolicy'
    'Bypass'
    '-File'
    $workerPath
    '-ConfigPath'
    $resolvedTelegramConfigPath
    '-BotApiConfigPath'
    $resolvedBotApiConfigPath
    '-StatePath'
    $statePath
    '-DispatchPath'
    $dispatchPath
    '-RepositoryRoot'
    $resolvedRepositoryRoot
    '-StateDirectory'
    $resolvedStateDirectory
)
$launcherArguments = @(
    '//B'
    '//NoLogo'
    (Quote-TaskArgument -Value $hiddenLauncherPath)
    (Quote-TaskArgument -Value $powershellPath)
)
$launcherArguments += @(
    $workerArgumentValues | ForEach-Object {
        Quote-TaskArgument -Value $_
    }
)
$launcherArguments = $launcherArguments -join ' '

$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$action = New-ScheduledTaskAction `
    -Execute $wscriptPath `
    -Argument $launcherArguments `
    -WorkingDirectory $PSScriptRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser
$principal = New-ScheduledTaskPrincipal `
    -UserId $currentUser `
    -LogonType Interactive `
    -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet `
    -Hidden `
    -StartWhenAvailable `
    -RestartCount $RestartCount `
    -RestartInterval (New-TimeSpan -Minutes $RestartIntervalMinutes) `
    -ExecutionTimeLimit (New-TimeSpan -Seconds 0) `
    -MultipleInstances IgnoreNew `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries

if ($PSCmdlet.ShouldProcess($TaskName, "Register AtLogOn task for $currentUser")) {
    $null = Register-ScheduledTask `
        -TaskName $TaskName `
        -TaskPath '\' `
        -Action $action `
        -Trigger $trigger `
        -Principal $principal `
        -Settings $settings `
        -Description 'Listens for Telegram build commands and dispatches local Codex builds.' `
        -Force

    Write-Host "Installed scheduled task: $TaskName"
}

if (-not $NoStart) {
    if ($PSCmdlet.ShouldProcess($TaskName, 'Start scheduled task')) {
        Start-ScheduledTask -TaskName $TaskName -TaskPath '\'
        Write-Host "Started scheduled task: $TaskName"
    }
}
