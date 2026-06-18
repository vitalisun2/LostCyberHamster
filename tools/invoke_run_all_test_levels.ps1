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
$runStamp = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$runLogDirectory = Join-Path $repoRoot "Temp\all_test_levels_$runStamp"
New-Item -ItemType Directory -Path $automationPath -Force | Out-Null
New-Item -ItemType Directory -Path $runLogDirectory -Force | Out-Null

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
Write-Host "Per-level logs: $runLogDirectory"

$PatternCatalog = $null

function Get-PatternCatalog {
    if ($null -ne $script:PatternCatalog) {
        return $script:PatternCatalog
    }

    $patternsPath = Join-Path $projectPath 'Assets\Content\locations\level_design_templates\levels\PatternsCollection.json'
    if (-not (Test-Path $patternsPath)) {
        throw "Patterns collection not found: $patternsPath"
    }

    $patternsJson = Get-Content -Path $patternsPath -Raw | ConvertFrom-Json
    $catalog = @{}
    foreach ($pattern in $patternsJson.patterns) {
        $description = $pattern.description
        if ([string]::IsNullOrWhiteSpace($description) -and $pattern.PSObject.Properties.Name -contains 'desсription') {
            $description = $pattern.'desсription'
        }

        $catalog[$pattern.name] = [PSCustomObject]@{
            Name = $pattern.name
            Description = $description
        }
    }

    $script:PatternCatalog = $catalog
    return $script:PatternCatalog
}

function Resolve-ExpectedAction {
    param(
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Description)) {
        return $null
    }

    $normalized = $Description.Trim().ToLowerInvariant()
    if (-not $normalized.StartsWith('should ')) {
        return $null
    }

    $isForbidden = $normalized.StartsWith('should not ')
    $actionPhrase = if ($isForbidden) {
        $normalized.Substring('should not '.Length).Trim()
    }
    else {
        $normalized.Substring('should '.Length).Trim()
    }

    $actionMappings = @(
        @{ Phrase = 'super jump from roof to roof'; Kind = 'SuperJumpFromRoofOnRoof' },
        @{ Phrase = 'jump from roof to roof'; Kind = 'JumpFromRoofOnRoof' },
        @{ Phrase = 'super jump on from roof'; Kind = 'SuperJumpOnFromRoof' },
        @{ Phrase = 'jump on from roof'; Kind = 'JumpOnFromRoof' },
        @{ Phrase = 'super jump from roof'; Kind = 'SuperJumpFromRoof' },
        @{ Phrase = 'jump from roof'; Kind = 'JumpFromRoof' },
        @{ Phrase = 'roof super jump over'; Kind = 'SuperRoofJumpOver' },
        @{ Phrase = 'roof jump over'; Kind = 'RoofJumpOver' },
        @{ Phrase = 'super jump on roof'; Kind = 'SuperJumpOnRoof' },
        @{ Phrase = 'jump on roof'; Kind = 'JumpOnRoof' },
        @{ Phrase = 'super jump over'; Kind = 'SuperJumpOver' },
        @{ Phrase = 'jump over'; Kind = 'JumpOver' },
        @{ Phrase = 'super jump on'; Kind = 'SuperJumpOn' },
        @{ Phrase = 'jump on'; Kind = 'JumpOn' },
        @{ Phrase = 'switch lane'; Kind = 'SwitchLane' }
    )

    foreach ($mapping in $actionMappings) {
        if ($actionPhrase.StartsWith($mapping.Phrase)) {
            return [PSCustomObject]@{
                Kind = $mapping.Kind
                Forbidden = $isForbidden
            }
        }
    }

    return [PSCustomObject]@{
        Kind = "UNKNOWN:$actionPhrase"
        Forbidden = $isForbidden
    }
}

function Get-LevelExpectedPatternActions {
    param(
        [Parameter(Mandatory)] [string]$LevelAddress
    )

    $parts = $LevelAddress -split '/'
    if ($parts.Length -lt 3) {
        throw "Unexpected level address format: $LevelAddress"
    }

    $levelKey = $parts[$parts.Length - 1]
    $levelJsonPath = Join-Path $projectPath "Assets\Content\locations\$($parts[0])\levels\$($parts[1])\$levelKey\$levelKey.json"
    if (-not (Test-Path $levelJsonPath)) {
        throw "Level json not found for $LevelAddress`: $levelJsonPath"
    }

    $catalog = Get-PatternCatalog
    $levelJson = Get-Content -Path $levelJsonPath -Raw | ConvertFrom-Json
    $expected = [System.Collections.Generic.List[PSCustomObject]]::new()
    $scenarioIndex = 0
    $sequenceIndex = 0

    foreach ($entry in $levelJson.patternSequence) {
        $sequenceIndex++
        $patternName = $entry.ref
        if ([string]::IsNullOrWhiteSpace($patternName)) {
            continue
        }

        if (-not $catalog.ContainsKey($patternName)) {
            continue
        }

        $description = $catalog[$patternName].Description
        $expectation = Resolve-ExpectedAction -Description $description
        if ($null -eq $expectation) {
            continue
        }

        $scenarioIndex++
        $expected.Add([PSCustomObject]@{
            ScenarioIndex = $scenarioIndex
            SequenceIndex = $sequenceIndex
            Pattern = $patternName
            Description = $description
            Kind = $expectation.Kind
            Forbidden = $expectation.Forbidden
        })
    }

    return $expected.ToArray()
}

function Get-LogPatternSpawns {
    param(
        [string[]]$LogLines
    )

    $spawns = [System.Collections.Generic.List[PSCustomObject]]::new()
    for ($lineIndex = 0; $lineIndex -lt $LogLines.Count; $lineIndex++) {
        $line = $LogLines[$lineIndex]
        if ($line -match '\[Bot PATTERN\] SPAWN patternIndex=([0-9]+) pattern=([^\s]+)') {
            $spawns.Add([PSCustomObject]@{
                LineIndex = $lineIndex
                PatternIndex = [int]$Matches[1]
                Pattern = $Matches[2]
            })
        }
    }

    return $spawns.ToArray()
}

function Get-ActionCountsInLogRange {
    param(
        [string[]]$LogLines,
        [int]$StartLineIndex,
        [int]$EndLineIndex
    )

    $actionCounts = @{}
    if ($StartLineIndex -lt 0) {
        $StartLineIndex = 0
    }
    if ($EndLineIndex -gt $LogLines.Count) {
        $EndLineIndex = $LogLines.Count
    }

    for ($lineIndex = $StartLineIndex; $lineIndex -lt $EndLineIndex; $lineIndex++) {
        $line = $LogLines[$lineIndex]
        if ($line -match '\[Bot EXEC\] FIRE kind=([A-Za-z]+)') {
            $kind = $Matches[1]
            if (-not $actionCounts.ContainsKey($kind)) {
                $actionCounts[$kind] = 0
            }

            $actionCounts[$kind]++
        }
    }

    return $actionCounts
}

function Format-ActionCounts {
    param(
        [hashtable]$ActionCounts
    )

    if ($null -eq $ActionCounts -or $ActionCounts.Count -eq 0) {
        return 'none'
    }

    return ($ActionCounts.GetEnumerator() |
        Sort-Object Name |
        ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ', '
}

function Get-LevelSemanticSummary {
    param(
        [Parameter(Mandatory)] [string]$LevelAddress,
        [string]$DiagnosticLogPath
    )

    $actionCounts = @{}
    $damageCount = 0
    $logLines = @()
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

    $actionSummary = Format-ActionCounts -ActionCounts $actionCounts

    $expectedPatterns = @(Get-LevelExpectedPatternActions -LevelAddress $LevelAddress)
    $spawns = @(Get-LogPatternSpawns -LogLines $logLines)
    $passedPatterns = [System.Collections.Generic.List[string]]::new()
    $failedPatterns = [System.Collections.Generic.List[string]]::new()
    $matchedPatterns = [System.Collections.Generic.List[PSCustomObject]]::new()
    $previousSpawnSearchStart = 0

    foreach ($expected in $expectedPatterns) {
        $spawnIndex = -1
        for ($candidateIndex = $previousSpawnSearchStart; $candidateIndex -lt $spawns.Count; $candidateIndex++) {
            if ($spawns[$candidateIndex].Pattern -eq $expected.Pattern) {
                $spawnIndex = $candidateIndex
                break
            }
        }

        $label = "#$($expected.ScenarioIndex) $($expected.Pattern) [$($expected.Description)]"
        if ($spawnIndex -lt 0) {
            $failedPatterns.Add("${label}: pattern was not spawned")
            continue
        }

        $previousSpawnSearchStart = $spawnIndex + 1
        $matchedPatterns.Add([PSCustomObject]@{
            Expected = $expected
            SpawnIndex = $spawnIndex
            Label = $label
        })
    }

    for ($matchedIndex = 0; $matchedIndex -lt $matchedPatterns.Count; $matchedIndex++) {
        $matchedPattern = $matchedPatterns[$matchedIndex]
        $expected = $matchedPattern.Expected
        $spawnIndex = $matchedPattern.SpawnIndex
        $label = $matchedPattern.Label
        $startLineIndex = $spawns[$spawnIndex].LineIndex
        $endLineIndex = if ($matchedIndex + 1 -lt $matchedPatterns.Count) {
            $nextSpawnIndex = $matchedPatterns[$matchedIndex + 1].SpawnIndex
            $spawns[$nextSpawnIndex].LineIndex
        }
        else {
            $logLines.Count
        }
        $patternActionCounts = Get-ActionCountsInLogRange -LogLines $logLines -StartLineIndex $startLineIndex -EndLineIndex $endLineIndex
        $actualCount = if ($patternActionCounts.ContainsKey($expected.Kind)) { $patternActionCounts[$expected.Kind] } else { 0 }

        if ($expected.Kind.StartsWith('UNKNOWN:')) {
            $failedPatterns.Add("${label}: unsupported expectation '$($expected.Kind)'")
            continue
        }

        if ($expected.Forbidden) {
            if ($actualCount -gt 0) {
                $failedPatterns.Add("${label}: forbidden $($expected.Kind) fired $actualCount time(s); actions=$(Format-ActionCounts -ActionCounts $patternActionCounts)")
            }
            else {
                $passedPatterns.Add("${label}: did not fire $($expected.Kind)")
            }
        }
        else {
            if ($actualCount -gt 0) {
                $passedPatterns.Add("${label}: fired $($expected.Kind) $actualCount time(s)")
            }
            else {
                $failedPatterns.Add("${label}: expected $($expected.Kind) was not fired; actions=$(Format-ActionCounts -ActionCounts $patternActionCounts)")
            }
        }
    }

    $semanticResult = if ($damageCount -eq 0 -and $failedPatterns.Count -eq 0) { 'OK' } else { 'FAIL' }
    return [PSCustomObject]@{
        Result = $semanticResult
        Actions = $actionSummary
        DamageMarkers = $damageCount
        CheckedPatterns = $expectedPatterns.Count
        PassedPatterns = $passedPatterns.ToArray()
        FailedPatterns = $failedPatterns.ToArray()
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
        if (-not [string]::IsNullOrWhiteSpace($response.diagnosticLogPath) -and (Test-Path $response.diagnosticLogPath)) {
            $logFileName = ($levelAddress -replace '[\\/:*?"<>|]', '_') + '.txt'
            Copy-Item -Path $response.diagnosticLogPath -Destination (Join-Path $runLogDirectory $logFileName) -Force
        }

        Write-Host "Result: $testResult"
        Write-Host "Actions: $($semanticSummary.Actions)"
        Write-Host "Damage markers: $($semanticSummary.DamageMarkers)"
        Write-Host "Checked patterns: $($semanticSummary.CheckedPatterns)"
        if ($semanticSummary.FailedPatterns.Count -gt 0) {
            Write-Host 'Pattern mismatches:'
            foreach ($failure in $semanticSummary.FailedPatterns) {
                Write-Host "  - $failure"
            }
        }
        Write-Host "Diagnostic log: $($response.diagnosticLogPath)"
        $summary.Add([PSCustomObject]@{
            Level = $levelAddress
            Result = $testResult
            Semantic = $semanticSummary.Result
            Actions = $semanticSummary.Actions
            CheckedPatterns = $semanticSummary.CheckedPatterns
            FailedPatterns = $semanticSummary.FailedPatterns
        })
    }
    catch {
        Write-Host "ERROR: $($_.Exception.Message)"
        $summary.Add([PSCustomObject]@{
            Level = $levelAddress
            Result = 'ERROR'
            Semantic = 'ERROR'
            Actions = 'none'
            CheckedPatterns = 0
            FailedPatterns = @($_.Exception.Message)
        })
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
    Write-Host "[$tag] $($item.Level) checked=$($item.CheckedPatterns) actions=[$($item.Actions)]"
    if ($item.FailedPatterns.Count -gt 0) {
        foreach ($failure in $item.FailedPatterns) {
            Write-Host "      - $failure"
        }
    }
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
