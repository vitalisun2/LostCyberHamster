param(
    [int]$TimeoutSeconds = 120,
    [int]$PollMilliseconds = 250,
    [string]$LevelAddress = '01_New_York/Morning/test_level',
    [float]$TimeScale = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectPath = Join-Path $PSScriptRoot 'LostCyberHamster'
$automationPath = Join-Path $projectPath 'EditorLogs\automation'
$requestPath = Join-Path $automationPath 'test_level_request.json'
$responsePath = Join-Path $automationPath 'test_level_response.json'
New-Item -ItemType Directory -Path $automationPath -Force | Out-Null

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

# Request recompilation; the bridge calls AssetDatabase.Refresh() internally,
# so no window focus or SendKeys needed.
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
    Write-Host '[warn] Explicit recompilation command is still unavailable; continuing after focus-based refresh fallback.'
}

# Regenerate .csproj/.sln so that Visual Studio Solution Explorer stays in sync
# with the actual file structure (handles added/deleted .cs files).
try {
    [void](Invoke-UnityAutomationCommand -Command 'regenerate_project_files' -RunningMessage 'project files regeneration')
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