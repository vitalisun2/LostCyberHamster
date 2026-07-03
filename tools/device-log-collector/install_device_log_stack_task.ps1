param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$TaskName = 'LostCyberHamsterDeviceLogStack',
    [int]$Port = 8765,
    [string]$NgrokDomain = 'ladle-substance-spray.ngrok-free.dev',
    [switch]$StartNow
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'start_device_log_stack.ps1'
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Supervisor script not found: $scriptPath"
}

$argument = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', "`"$scriptPath`"",
    '-RepositoryRoot', "`"$RepositoryRoot`"",
    '-Port', $Port,
    '-NgrokDomain', $NgrokDomain
) -join ' '

function New-Launcher {
    $launcherDir = Join-Path $env:LOCALAPPDATA 'LostCyberHamster\device-log-stack'
    New-Item -ItemType Directory -Force -Path $launcherDir | Out-Null

    $launcherPath = Join-Path $launcherDir 'start_device_log_stack.cmd'
$content = @"
@echo off
:restart
powershell.exe $argument
timeout /t 10 /nobreak >nul
goto restart
"@
    Set-Content -LiteralPath $launcherPath -Value $content -Encoding ASCII
    return $launcherPath
}

$registeredWithPowerShell = $false
$registeredWithStartupShortcut = $false

function Install-StartupShortcut {
    param([string]$LauncherPath)

    $startupDir = [Environment]::GetFolderPath('Startup')
    if ([string]::IsNullOrWhiteSpace($startupDir)) {
        throw 'Windows Startup folder was not resolved.'
    }

    $shortcutPath = Join-Path $startupDir "$TaskName.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $LauncherPath
    $shortcut.WorkingDirectory = Split-Path -Parent $LauncherPath
    $shortcut.WindowStyle = 7
    $shortcut.Description = 'LostCyberHamster Android device log collector + ngrok supervisor.'
    $shortcut.Save()

    return $shortcutPath
}

try {
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argument
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    $settings = New-ScheduledTaskSettingsSet `
        -MultipleInstances IgnoreNew `
        -RestartCount 999 `
        -RestartInterval (New-TimeSpan -Minutes 1) `
        -ExecutionTimeLimit (New-TimeSpan -Days 365) `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries

    Register-ScheduledTask `
        -TaskName $TaskName `
        -Action $action `
        -Trigger $trigger `
        -Settings $settings `
        -Description 'LostCyberHamster Android device log collector + ngrok supervisor.' `
        -Force | Out-Null

    $registeredWithPowerShell = $true
}
catch {
    Write-Warning "Register-ScheduledTask failed, falling back to schtasks.exe launcher: $($_.Exception.Message)"
    $launcherPath = New-Launcher
    $taskRun = "`"$launcherPath`""
    & schtasks.exe /Create /SC ONLOGON /TN $TaskName /TR $taskRun /RL LIMITED /F
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "schtasks.exe failed with exit code $LASTEXITCODE, installing user Startup shortcut fallback."
        $shortcutPath = Install-StartupShortcut -LauncherPath $launcherPath
        $registeredWithStartupShortcut = $true
    }
}

if ($StartNow.IsPresent) {
    if ($registeredWithPowerShell) {
        Start-ScheduledTask -TaskName $TaskName
    }
    elseif ($registeredWithStartupShortcut) {
        Start-Process -FilePath $launcherPath -WorkingDirectory (Split-Path -Parent $launcherPath) -WindowStyle Hidden
    }
    else {
        & schtasks.exe /Run /TN $TaskName
        if ($LASTEXITCODE -ne 0) {
            throw "schtasks.exe /Run failed with exit code $LASTEXITCODE"
        }
    }
}

if ($registeredWithStartupShortcut) {
    [pscustomobject]@{
        TaskName = $TaskName
        Mode = 'StartupShortcut'
        State = 'Installed'
        ShortcutPath = $shortcutPath
        LauncherPath = $launcherPath
    }
}
else {
    try {
    Get-ScheduledTask -TaskName $TaskName | Select-Object TaskName, TaskPath, State
    }
    catch {
        & schtasks.exe /Query /TN $TaskName
    }
}
