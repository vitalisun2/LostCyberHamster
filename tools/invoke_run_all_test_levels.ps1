<#
.SYNOPSIS
    Runs ALL bot test levels in sequence and prints a pass/fail summary.
.DESCRIPTION
    Discovers all test levels under Assets/Content/locations, recompiles scripts once,
    then launches each level via the automation bridge. Use this instead of
    invoke_open_unity_test_level.ps1 for the standard workflow validation step.
.PARAMETER TimeoutSeconds
    Timeout per level in seconds. Default: 120.
.PARAMETER PollMilliseconds
    Polling interval in milliseconds. Default: 250.
.PARAMETER TimeScale
    Explicit Time.timeScale override forwarded to Unity. Default: 1.
#>
param(
    [int]$TimeoutSeconds = 120,
    [int]$PollMilliseconds = 250,
    [float]$TimeScale = 1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot       = Split-Path -Parent $PSScriptRoot
$projectPath    = Join-Path $repoRoot 'LostCyberHamster'
$automationPath = Join-Path $projectPath 'EditorLogs\automation'
$requestPath    = Join-Path $automationPath 'test_level_request.json'
$responsePath   = Join-Path $automationPath 'test_level_response.json'
New-Item -ItemType Directory -Path $automationPath -Force | Out-Null

# ── Test levels — Locations folder is the source of truth ───────────────────
function Get-TestLevelAddresses {
    $locationsPath = Join-Path $projectPath 'Assets\Content\locations'
    if (-not (Test-Path $locationsPath)) {
        throw "Locations folder not found: $locationsPath"
    }

    $discovered = Get-ChildItem -Path $locationsPath -Filter 'test*.json' -File -Recurse |
        ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($locationsPath, $_.FullName)
            $parts = $relativePath -split '[\\/]'
            if ($parts.Length -lt 5 -or $parts[1] -ne 'levels') {
                return
            }

            "$($parts[0])/$($parts[2])/$($parts[3])"
        } |
        Sort-Object -Unique

    $preferredOrder = @(
        '01_New_York/Morning/test_switch_lane',
        '01_New_York/Morning/test_jump_over',
        '01_New_York/Morning/test_superjump_over',
        '01_New_York/Morning/test_jump_on_roof',
        '01_New_York/Morning/test_super_jump_on_roof'
    )

    $ordered = [System.Collections.Generic.List[string]]::new()
    foreach ($levelAddress in $preferredOrder) {
        if ($discovered -contains $levelAddress) {
            $ordered.Add($levelAddress)
        }
    }

    foreach ($levelAddress in $discovered) {
        if (-not $ordered.Contains($levelAddress)) {
            $ordered.Add($levelAddress)
        }
    }

    if ($ordered.Count -eq 0) {
        throw "No test levels found under $locationsPath"
    }

    return $ordered.ToArray()
}

$TestLevels = @(Get-TestLevelAddresses)
Write-Host "Discovered $($TestLevels.Count) test levels."

$ExpectedActionKindsByLevel = @{
    '01_New_York/Morning/test_switch_lane'        = @('SwitchLane')
    '01_New_York/Morning/test_jump_over'          = @('JumpOver')
    '01_New_York/Morning/test_superjump_over'     = @('SuperJumpOver')
    '01_New_York/Morning/test_jump_on_roof'       = @('JumpOnRoof')
    '01_New_York/Morning/test_super_jump_on_roof' = @('SuperJumpOnRoof')
}

function Get-LevelSemanticSummary {
    param(
        [Parameter(Mandatory)] [string]$LevelAddress,
        [string]$DiagnosticLogPath
    )

    $actionCounts = @{}
    $damageCount = 0
    if (-not [string]::IsNullOrWhiteSpace($DiagnosticLogPath) -and (Test-Path $DiagnosticLogPath)) {
        $logLines = Get-Content -Path $DiagnosticLogPath
        foreach ($line in $logLines) {
            if ($line -match '\[Bot DAMAGE\]|\[TEST RESULT\] FAIL') {
                $damageCount++
            }

            if ($line -match '\[Bot EXEC\] FIRE kind=([A-Za-z]+)') {
                $kind = $Matches[1]
                if (-not $actionCounts.ContainsKey($kind)) {
                    $actionCounts[$kind] = 0
                }

                $actionCounts[$kind]++
            }
        }
    }

    $actionSummary = 'none'
    if ($actionCounts.Count -gt 0) {
        $actionSummary = ($actionCounts.GetEnumerator() |
            Sort-Object Name |
            ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ', '
    }

    $missingExpectedActions = @()
    if ($ExpectedActionKindsByLevel.ContainsKey($LevelAddress)) {
        foreach ($expectedActionKind in $ExpectedActionKindsByLevel[$LevelAddress]) {
            if (-not $actionCounts.ContainsKey($expectedActionKind)) {
                $missingExpectedActions += $expectedActionKind
            }
        }
    }

    $semanticResult = if ($damageCount -eq 0 -and $missingExpectedActions.Count -eq 0) { 'OK' } else { 'FAIL' }
    return [PSCustomObject]@{
        Result = $semanticResult
        Actions = $actionSummary
        DamageMarkers = $damageCount
        MissingExpectedActions = $missingExpectedActions
    }
}

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

# ── Regenerate project files for IDE/MSBuild after .cs add/delete ────────────
Write-Host ''
Write-Host '=== Regenerating project files ==='
try {
    [void](Invoke-UnityCommand -Command 'regenerate_project_files' -RunningMessage 'project files regeneration')
}
catch {
    Write-Host "[warn] regenerate_project_files failed: $($_.Exception.Message)"
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
        $semanticSummary = Get-LevelSemanticSummary -LevelAddress $levelAddress -DiagnosticLogPath $response.diagnosticLogPath
        Write-Host "Result: $testResult"
        Write-Host "Actions: $($semanticSummary.Actions)"
        Write-Host "Damage markers: $($semanticSummary.DamageMarkers)"
        if ($semanticSummary.MissingExpectedActions.Count -gt 0) {
            Write-Host "Missing expected actions: $($semanticSummary.MissingExpectedActions -join ', ')"
        }
        Write-Host "Diagnostic log: $($response.diagnosticLogPath)"
        $summary.Add([PSCustomObject]@{
            Level = $levelAddress
            Result = $testResult
            Semantic = $semanticSummary.Result
            Actions = $semanticSummary.Actions
        })
    }
    catch {
        Write-Host "ERROR: $($_.Exception.Message)"
        $summary.Add([PSCustomObject]@{ Level = $levelAddress; Result = 'ERROR'; Semantic = 'ERROR'; Actions = 'none' })
    }
}

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '========== SUMMARY =========='
$failCount = 0
foreach ($item in $summary) {
    $tag = switch ($item.Result) {
        'WIN'   { if ($item.Semantic -eq 'OK') { 'WIN ' } else { 'SEMF'; $failCount++ } }
        'FAIL'  { 'FAIL'; $failCount++ }
        default { 'ERR '; $failCount++ }
    }
    Write-Host "[$tag] $($item.Level) actions=[$($item.Actions)]"
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
