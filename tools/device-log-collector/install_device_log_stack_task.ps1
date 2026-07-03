param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$TaskName = 'LostCyberHamsterDeviceLogStack',
    [int]$Port = 8765,
    [string]$NgrokDomain = 'ladle-substance-spray.ngrok-free.dev',
    [switch]$StartNow
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'ensure_device_log_docker_stack.ps1'
if (-not (Test-Path -LiteralPath $scriptPath)) {
    throw "Docker ensure script not found: $scriptPath"
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
powershell.exe $argument
"@
    Set-Content -LiteralPath $launcherPath -Value $content -Encoding ASCII
    return $launcherPath
}

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
    $shortcut.Description = 'LostCyberHamster Android device log Docker stack ensure.'
    $shortcut.Save()

    return $shortcutPath
}

$registeredWithPowerShell = $false
$registeredWithStartupShortcut = $false
$launcherPath = New-Launcher
$shortcutPath = $null

try {
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argument
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    $settings = New-ScheduledTaskSettingsSet `
        -MultipleInstances IgnoreNew `
        -RestartCount 3 `
        -RestartInterval (New-TimeSpan -Minutes 2) `
        -ExecutionTimeLimit (New-TimeSpan -Minutes 10) `
        -AllowStartIfOnBatteries `
        -DontStopIfGoingOnBatteries

    Register-ScheduledTask `
        -TaskName $TaskName `
        -Action $action `
        -Trigger $trigger `
        -Settings $settings `
        -Description 'LostCyberHamster Android device log Docker stack ensure.' `
        -Force | Out-Null

    $registeredWithPowerShell = $true
}
catch {
    Write-Warning "Register-ScheduledTask failed, falling back to schtasks.exe launcher: $($_.Exception.Message)"
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
            Write-Warning "schtasks.exe /Run failed with exit code $LASTEXITCODE; running launcher directly."
            Start-Process -FilePath $launcherPath -WorkingDirectory (Split-Path -Parent $launcherPath) -WindowStyle Hidden
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
elseif ($registeredWithPowerShell) {
    Get-ScheduledTask -TaskName $TaskName | Select-Object TaskName, TaskPath, State
}
else {
    [pscustomobject]@{
        TaskName = $TaskName
        Mode = 'Schtasks'
        State = 'Installed'
        LauncherPath = $launcherPath
    }
}
