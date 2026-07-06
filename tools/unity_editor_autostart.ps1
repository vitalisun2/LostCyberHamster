Set-StrictMode -Version Latest

function Initialize-UnityAutomationWindowHelper {
    if ('UnityAutomationWindowHelper' -as [type]) {
        return
    }

    $typeDefinition = @'
using System;
using System.Runtime.InteropServices;

public static class UnityAutomationWindowHelper
{
    [DllImport("user32.dll")]
    public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);
}
'@

    Add-Type -TypeDefinition $typeDefinition
}

function ConvertTo-UnityAutomationComparablePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    try {
        return [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/').ToLowerInvariant()
    }
    catch {
        return $Path.TrimEnd('\', '/').ToLowerInvariant()
    }
}

function Get-UnityEditorProjectProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $projectFullPath = ConvertTo-UnityAutomationComparablePath -Path $ProjectPath
    $projectName = Split-Path -Leaf $ProjectPath
    $unityProcesses = @(Get-Process Unity -ErrorAction SilentlyContinue)
    $unityProcessById = @{}

    foreach ($process in $unityProcesses) {
        $unityProcessById[[int]$process.Id] = $process
    }

    $unityCimProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue)
    foreach ($process in $unityCimProcesses) {
        $commandLine = [string]$process.CommandLine
        if ([string]::IsNullOrWhiteSpace($commandLine)) {
            continue
        }

        if (-not $commandLine.ToLowerInvariant().Contains($projectFullPath)) {
            continue
        }

        $processId = [int]$process.ProcessId
        if ($unityProcessById.ContainsKey($processId)) {
            return $unityProcessById[$processId]
        }
    }

    foreach ($process in $unityProcesses) {
        $title = [string]$process.MainWindowTitle
        if ([string]::IsNullOrWhiteSpace($title)) {
            continue
        }

        if ($title -like "$projectName -*" -or ($title -like "*$projectName*" -and $title -like '*Unity*')) {
            return $process
        }
    }

    return $null
}

function Invoke-UnityEditorWindowWake {
    param(
        [Parameter(Mandatory = $true)]
        $Process
    )

    Initialize-UnityAutomationWindowHelper

    try {
        if ($Process.MainWindowHandle -eq 0) {
            return
        }

        if ([UnityAutomationWindowHelper]::IsIconic($Process.MainWindowHandle)) {
            [UnityAutomationWindowHelper]::ShowWindowAsync($Process.MainWindowHandle, 9) | Out-Null
        }

        [UnityAutomationWindowHelper]::BringWindowToTop($Process.MainWindowHandle) | Out-Null
        [UnityAutomationWindowHelper]::SetForegroundWindow($Process.MainWindowHandle) | Out-Null
    }
    catch {
        Write-Host "[warn] Failed to wake Unity window '$($Process.MainWindowTitle)': $($_.Exception.Message)"
    }
}

function Ensure-UnityEditorForProject {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$UnityExePath,

        [int]$TimeoutSeconds = 600
    )

    $resolvedProjectPath = (Resolve-Path $ProjectPath).Path
    $existingProcess = Get-UnityEditorProjectProcess -ProjectPath $resolvedProjectPath
    if ($null -ne $existingProcess) {
        Write-Host "[info] Unity project editor already running: pid=$($existingProcess.Id) title='$($existingProcess.MainWindowTitle)'"
        Invoke-UnityEditorWindowWake -Process $existingProcess
        return
    }

    if (-not (Test-Path $UnityExePath)) {
        throw "Unity executable not found: $UnityExePath"
    }

    Write-Host "[info] Unity project editor is not running. Starting Unity: $resolvedProjectPath"
    Start-Process -FilePath $UnityExePath -ArgumentList @('-projectPath', $resolvedProjectPath)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 5

        $process = Get-UnityEditorProjectProcess -ProjectPath $resolvedProjectPath
        if ($null -eq $process) {
            continue
        }

        $title = [string]$process.MainWindowTitle
        if ([string]::IsNullOrWhiteSpace($title) -or $title -like 'Opening project*') {
            Write-Host "[info] Waiting for Unity project window... pid=$($process.Id) title='$title'"
            continue
        }

        Write-Host "[completed] Unity project editor is ready: pid=$($process.Id) title='$title'"
        Invoke-UnityEditorWindowWake -Process $process
        return
    }

    throw "Timed out waiting for Unity editor to open project '$resolvedProjectPath'."
}
