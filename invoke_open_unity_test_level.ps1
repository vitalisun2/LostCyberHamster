param(
    [int]$TimeoutSeconds = 120,
    [int]$PollMilliseconds = 250,
    [string]$LevelAddress = '01_New_York/Morning/test_threat_small_notalive_road_switchlane',
    [float]$TimeScale = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'LostCyberHamster'
$automationPath = Join-Path $projectPath 'EditorLogs\automation'
$requestPath = Join-Path $automationPath 'test_level_request.json'
$responsePath = Join-Path $automationPath 'test_level_response.json'
New-Item -ItemType Directory -Path $automationPath -Force | Out-Null

function Initialize-WindowActivationHelper {
    if ('WindowActivationHelper' -as [type]) {
        return
    }

    $typeDefinition = @'
using System;
using System.Runtime.InteropServices;

public static class WindowActivationHelper
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

function TryWakeUnityEditorWindow {
    Initialize-WindowActivationHelper

    $candidateProcesses = Get-Process | Where-Object {
        $_.MainWindowHandle -ne 0 -and (
            $_.ProcessName -like 'Unity*' -or
            $_.MainWindowTitle -like '*Unity*' -or
            $_.MainWindowTitle -like '*LostCyberHamster*'
        )
    } | Sort-Object {
        if ($_.MainWindowTitle -like '*LostCyberHamster*') { 0 } else { 1 }
    }

    foreach ($process in $candidateProcesses) {
        try {
            if ([WindowActivationHelper]::IsIconic($process.MainWindowHandle)) {
                [WindowActivationHelper]::ShowWindowAsync($process.MainWindowHandle, 9) | Out-Null
            }

            [WindowActivationHelper]::BringWindowToTop($process.MainWindowHandle) | Out-Null
            [WindowActivationHelper]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
            Write-Host "[info] Woke Unity window: $($process.MainWindowTitle)"
            return $true
        }
        catch {
            Write-Host "[warn] Failed to wake Unity window '$($process.MainWindowTitle)': $($_.Exception.Message)"
        }
    }

    Write-Host '[warn] Unity window wake-up fallback could not find an active Unity editor window.'
    return $false
}

function Initialize-RunningObjectTableHelper {
    if ('VisualStudioRunningObjectTableHelper' -as [type]) {
        return
    }

    $typeDefinition = @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

public static class VisualStudioRunningObjectTableHelper
{
    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable pprot);

    public static List<string> GetMonikerNames()
    {
        var result = new List<string>();
        GetRunningObjectTable(0, out var rot);
        rot.EnumRunning(out var enumMoniker);
        var monikers = new IMoniker[1];
        while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
        {
            CreateBindCtx(0, out var ctx);
            monikers[0].GetDisplayName(ctx, null, out var name);
            result.Add(name);
        }

        return result;
    }

    public static object GetObject(string displayName)
    {
        GetRunningObjectTable(0, out var rot);
        rot.EnumRunning(out var enumMoniker);
        var monikers = new IMoniker[1];
        while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
        {
            CreateBindCtx(0, out var ctx);
            monikers[0].GetDisplayName(ctx, null, out var name);
            if (!string.Equals(name, displayName, StringComparison.OrdinalIgnoreCase))
                continue;

            rot.GetObject(monikers[0], out var comObject);
            return comObject;
        }

        return null;
    }
}
'@

    Add-Type -TypeDefinition $typeDefinition
}

function Reload-RunningVisualStudioSolution {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SolutionPath
    )

    Initialize-RunningObjectTableHelper

    $expectedSolutionPath = [System.IO.Path]::GetFullPath($SolutionPath)
    $monikers = [VisualStudioRunningObjectTableHelper]::GetMonikerNames() | Where-Object { $_ -like '!VisualStudio.DTE.*' }

    foreach ($moniker in $monikers) {
        $dte = $null
        try {
            $dte = [VisualStudioRunningObjectTableHelper]::GetObject($moniker)
            if ($null -eq $dte) {
                continue
            }

            $openSolutionRaw = $dte.Solution.FullName
            if ([string]::IsNullOrWhiteSpace($openSolutionRaw)) {
                continue
            }

            $openSolutionPath = [System.IO.Path]::GetFullPath($openSolutionRaw)
            if (-not [string]::Equals($openSolutionPath, $expectedSolutionPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $dte.Solution.Close($false)
            Start-Sleep -Milliseconds 500
            $dte.Solution.Open($expectedSolutionPath)
            Write-Host "[completed] Reloaded open Visual Studio solution."
            return
        }
        catch {
            Write-Host "[warn] Visual Studio reload failed for moniker '$moniker': $($_.Exception.Message)"
        }
    }

    Write-Host '[info] No running Visual Studio instance was found for this solution.'
}

function Invoke-UnityAutomationCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $true)]
        [string]$RunningMessage,

        [string]$LevelAddress,

        [float]$TimeScale = 0
    )

    $requestId = [Guid]::NewGuid().ToString('N')
    $request = [ordered]@{
        requestId = $requestId
        command = $Command
        createdAtUtc = [DateTime]::UtcNow.ToString('o')
    }

    if (-not [string]::IsNullOrWhiteSpace($LevelAddress)) {
        $request.levelAddress = $LevelAddress
    }

    if ($TimeScale -gt 0) {
        $request.timeScale = $TimeScale
    }

    $request | ConvertTo-Json | Set-Content -Path $requestPath -Encoding UTF8

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = $null
    $wakeAttempted = $false
    $wakeAt = (Get-Date).AddMilliseconds([Math]::Max($PollMilliseconds * 4, 1000))

    while ((Get-Date) -lt $deadline) {
        if (Test-Path $responsePath) {
            try {
                $response = Get-Content -Path $responsePath -Raw | ConvertFrom-Json
            }
            catch {
                Start-Sleep -Milliseconds $PollMilliseconds
                continue
            }

            if ($null -eq $response -or $response.requestId -ne $requestId) {
                Start-Sleep -Milliseconds $PollMilliseconds
                continue
            }

            if ($response.state -ne $lastState) {
                $lastState = $response.state
                Write-Host "[$($response.state)] $($response.message)"
            }

            if ($response.state -eq 'completed') {
                return $response
            }

            if ($response.state -in @('failed', 'busy')) {
                throw "$($response.state): $($response.message)"
            }
        }

        if (-not $wakeAttempted -and (Get-Date) -ge $wakeAt) {
            # Some Unity Editor sessions do not process the file-queue until the window is activated.
            # Wake the editor once so the automation bridge can pick up the request without a manual click.
            [void](TryWakeUnityEditorWindow)
            $wakeAttempted = $true
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    }

    if (Test-Path $requestPath) {
        try {
            $pendingRequest = Get-Content -Path $requestPath -Raw | ConvertFrom-Json
            if ($null -ne $pendingRequest -and $pendingRequest.requestId -eq $requestId) {
                Remove-Item -Path $requestPath -Force
            }
        }
        catch {
        }
    }

    throw "Timeout waiting for Unity automation response during '$RunningMessage'. Ensure the project is open in Unity and scripts compiled successfully."
}

# Request recompilation through the bridge. If Unity does not pick up the request
# while unfocused, the launcher wakes the editor window once as a fallback.
$recompileCompleted = $false
for ($attempt = 1; $attempt -le 5 -and -not $recompileCompleted; $attempt++) {
    Start-Sleep -Seconds 2

    try {
        [void](Invoke-UnityAutomationCommand -Command 'recompile_scripts' -RunningMessage 'script recompilation')
        $recompileCompleted = $true
    }
    catch {
        if ($_.Exception.Message -notlike 'failed: Unsupported command: recompile_scripts') {
            throw
        }

        Write-Host "[retry] Unity editor still uses old bridge assembly; AssetDatabase.Refresh may need more time (attempt $attempt/5)."
    }
}

if (-not $recompileCompleted) {
    Write-Host '[warn] Explicit recompilation command is still unavailable after fallback wake-up attempts.'
}

# Regenerate .csproj/.sln so that Visual Studio Solution Explorer stays in sync
# with the actual file structure (handles added/deleted .cs files).
try {
    [void](Invoke-UnityAutomationCommand -Command 'regenerate_project_files' -RunningMessage 'project files regeneration')
    Reload-RunningVisualStudioSolution -SolutionPath (Join-Path $projectPath 'LostCyberHamster.sln')
}
catch {
    Write-Host "[warn] regenerate_project_files failed: $($_.Exception.Message)"
}

$launchResponse = Invoke-UnityAutomationCommand `
    -Command 'launch_test_level' `
    -RunningMessage 'test level launch' `
    -LevelAddress $LevelAddress `
    -TimeScale $TimeScale

Write-Host "Result: $($launchResponse.testResult)"
Write-Host "Diagnostic log: $($launchResponse.diagnosticLogPath)"
exit 0