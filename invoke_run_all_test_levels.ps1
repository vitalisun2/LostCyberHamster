<#
.SYNOPSIS
    Runs ALL bot test levels in sequence and prints a pass/fail summary.
.DESCRIPTION
    Recompiles scripts once, then launches each test level listed in $TestLevels via the
    automation bridge. Use this instead of invoke_open_unity_test_level.ps1 for the
    standard workflow validation step (step 4b in workflow.md).
.PARAMETER TimeoutSeconds
    Timeout per level in seconds. Default: 120.
.PARAMETER PollMilliseconds
    Polling interval in milliseconds. Default: 250.
.PARAMETER TimeScale
    Explicit Time.timeScale override forwarded to Unity. Default: 0 (no override —
    bot-enabled logic applies, which defaults to 4x when bot is on).
#>
param(
    [int]$TimeoutSeconds = 120,
    [int]$PollMilliseconds = 250,
    [float]$TimeScale = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectPath    = Join-Path $PSScriptRoot 'LostCyberHamster'
$automationPath = Join-Path $projectPath 'EditorLogs\automation'
$requestPath    = Join-Path $automationPath 'test_level_request.json'
$responsePath   = Join-Path $automationPath 'test_level_response.json'
New-Item -ItemType Directory -Path $automationPath -Force | Out-Null

# ── Test levels — keep in sync with workflow.md «Тестовые уровни бота» ──────
$TestLevels = @(
    '01_New_York/Morning/test_threat_small_notalive_road_switchlane',
    '01_New_York/Morning/test_threat_small_notalive_road_jump',
    '01_New_York/Morning/test_threat_bigalive'
)

# ── Automation bridge helper ─────────────────────────────────────────────────
function Invoke-UnityCommand {
    param(
        [Parameter(Mandatory)] [string]$Command,
        [Parameter(Mandatory)] [string]$RunningMessage,
        [string]$LevelAddress,
        [float]$CmdTimeScale = 0
    )

    $requestId = [Guid]::NewGuid().ToString('N')
    $request   = [ordered]@{
        requestId    = $requestId
        command      = $Command
        createdAtUtc = [DateTime]::UtcNow.ToString('o')
    }

    if (-not [string]::IsNullOrWhiteSpace($LevelAddress)) {
        $request.levelAddress = $LevelAddress
    }
    if ($CmdTimeScale -gt 0) {
        $request.timeScale = $CmdTimeScale
    }

    $request | ConvertTo-Json | Set-Content -Path $requestPath -Encoding UTF8

    $deadline  = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = $null

    while ((Get-Date) -lt $deadline) {
        if (Test-Path $responsePath) {
            try   { $response = Get-Content -Path $responsePath -Raw | ConvertFrom-Json }
            catch { Start-Sleep -Milliseconds $PollMilliseconds; continue }

            if ($null -eq $response -or $response.requestId -ne $requestId) {
                Start-Sleep -Milliseconds $PollMilliseconds
                continue
            }

            if ($response.state -ne $lastState) {
                $lastState = $response.state
                Write-Host "[$($response.state)] $($response.message)"
            }

            if ($response.state -eq 'completed') { return $response }
            if ($response.state -in @('failed', 'busy')) {
                throw "$($response.state): $($response.message)"
            }
        }
        Start-Sleep -Milliseconds $PollMilliseconds
    }

    if (Test-Path $requestPath) {
        try {
            $pending = Get-Content -Path $requestPath -Raw | ConvertFrom-Json
            if ($null -ne $pending -and $pending.requestId -eq $requestId) {
                Remove-Item -Path $requestPath -Force
            }
        } catch {}
    }

    throw "Timeout waiting for Unity automation response during '$RunningMessage'. Ensure the project is open in Unity and scripts are compiled."
}

# ── Recompile once before all levels ─────────────────────────────────────────
Write-Host ''
Write-Host '=== Recompiling scripts ==='
$recompileCompleted = $false
for ($attempt = 1; $attempt -le 5 -and -not $recompileCompleted; $attempt++) {
    Start-Sleep -Seconds 2
    try {
        [void](Invoke-UnityCommand -Command 'recompile_scripts' -RunningMessage 'script recompilation')
        $recompileCompleted = $true
    }
    catch {
        if ($_.Exception.Message -notlike 'failed: Unsupported command: recompile_scripts') { throw }
        Write-Host "[retry] Bridge assembly not yet updated (attempt $attempt/5)."
    }
}
if (-not $recompileCompleted) {
    Write-Host '[warn] Recompilation unavailable; continuing anyway.'
}

# ── Run each level ────────────────────────────────────────────────────────────
$summary = [System.Collections.Generic.List[PSCustomObject]]::new()

foreach ($levelAddress in $TestLevels) {
    Write-Host ''
    Write-Host "=== Level: $levelAddress ==="
    try {
        $response = Invoke-UnityCommand `
            -Command       'launch_test_level' `
            -RunningMessage 'test level launch' `
            -LevelAddress  $levelAddress `
            -CmdTimeScale  $TimeScale

        $testResult = $response.testResult
        Write-Host "Result: $testResult"
        Write-Host "Diagnostic log: $($response.diagnosticLogPath)"
        $summary.Add([PSCustomObject]@{ Level = $levelAddress; Result = $testResult })
    }
    catch {
        Write-Host "ERROR: $($_.Exception.Message)"
        $summary.Add([PSCustomObject]@{ Level = $levelAddress; Result = 'ERROR' })
    }
}

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '========== SUMMARY =========='
$failCount = 0
foreach ($item in $summary) {
    $tag = switch ($item.Result) {
        'WIN'   { 'WIN ' }
        'FAIL'  { 'FAIL'; $failCount++ }
        default { 'ERR '; $failCount++ }
    }
    Write-Host "[$tag] $($item.Level)"
}

if ($failCount -eq 0) {
    Write-Host ''
    Write-Host "All $($TestLevels.Count) levels passed."
    exit 0
}
else {
    Write-Host ''
    Write-Host "$failCount of $($TestLevels.Count) levels failed."
    exit 1
}
