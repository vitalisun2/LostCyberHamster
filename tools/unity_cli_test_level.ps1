Set-StrictMode -Version Latest

function Get-UnityCliProperty {
    param(
        [AllowNull()]
        [object]$InputObject,

        [Parameter(Mandatory)]
        [string]$Name
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Format-UnityCliErrors {
    param(
        [AllowNull()]
        [object]$Envelope
    )

    $messages = @(
        @(Get-UnityCliProperty -InputObject $Envelope -Name 'errors') |
            Where-Object { $null -ne $_ } |
            ForEach-Object {
                $code = Get-UnityCliProperty -InputObject $_ -Name 'code'
                $message = Get-UnityCliProperty -InputObject $_ -Name 'message'
                if (-not [string]::IsNullOrWhiteSpace([string]$code) -and
                    -not [string]::IsNullOrWhiteSpace([string]$message)) {
                    return "${code}: $message"
                }

                if (-not [string]::IsNullOrWhiteSpace([string]$message)) {
                    return [string]$message
                }

                return ($_ | ConvertTo-Json -Compress -Depth 8)
            }
    )

    if ($messages.Count -eq 0) {
        return 'Unity CLI returned no error details.'
    }

    return ($messages -join '; ')
}

function Invoke-UnityCliJson {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $unityCommand = Get-Command 'unity' -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $unityCommand) {
        throw 'Unity CLI executable was not found in PATH.'
    }

    $stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) ("lch_unity_cli_{0}.stderr" -f [Guid]::NewGuid().ToString('N'))
    try {
        $stdoutLines = @(& $unityCommand.Source @Arguments 2> $stderrPath)
        $exitCode = $LASTEXITCODE
        $stdout = $stdoutLines -join [Environment]::NewLine
        $stderr = if (Test-Path -LiteralPath $stderrPath) {
            Get-Content -Raw -LiteralPath $stderrPath
        }
        else {
            ''
        }

        if ([string]::IsNullOrWhiteSpace($stdout)) {
            $detail = if ([string]::IsNullOrWhiteSpace($stderr)) { 'empty stdout' } else { $stderr.Trim() }
            throw "Unity CLI returned no JSON (exit $exitCode): $detail"
        }

        try {
            $envelope = $stdout | ConvertFrom-Json
        }
        catch {
            throw "Unity CLI returned invalid JSON (exit $exitCode): $($_.Exception.Message)"
        }

        $success = Get-UnityCliProperty -InputObject $envelope -Name 'success'
        if ($exitCode -ne 0 -or $success -ne $true) {
            $details = Format-UnityCliErrors -Envelope $envelope
            throw "Unity CLI command failed (exit $exitCode): $details"
        }

        return $envelope
    }
    finally {
        if (Test-Path -LiteralPath $stderrPath) {
            Remove-Item -LiteralPath $stderrPath -Force
        }
    }
}

function Invoke-UnityCliCommandResult {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [Parameter(Mandatory)]
        [string]$Command,

        [string[]]$CommandArguments = @(),

        [int]$RequestTimeoutSeconds = 10
    )

    $arguments = @(
        'command',
        $Command,
        '--project-path',
        $ProjectPath,
        '--timeout',
        ([string]$RequestTimeoutSeconds),
        '--format',
        'json',
        '--no-banner',
        '--non-interactive'
    ) + $CommandArguments

    $envelope = Invoke-UnityCliJson -Arguments $arguments
    $data = Get-UnityCliProperty -InputObject $envelope -Name 'data'
    $result = Get-UnityCliProperty -InputObject $data -Name 'result'

    if ($result -is [string] -and $result.TrimStart().StartsWith('{')) {
        try {
            return ($result | ConvertFrom-Json)
        }
        catch {
            throw "Unity CLI command '$Command' returned invalid result JSON: $($_.Exception.Message)"
        }
    }

    return $result
}

function Test-UnityCliTestLevelTransport {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [ref]$FailureReason
    )

    try {
        $statusEnvelope = Invoke-UnityCliJson -Arguments @(
            'status',
            '--project-path',
            $ProjectPath,
            '--format',
            'json',
            '--no-banner',
            '--non-interactive'
        )

        $statusData = Get-UnityCliProperty -InputObject $statusEnvelope -Name 'data'
        $instances = @(Get-UnityCliProperty -InputObject $statusData -Name 'instances')
        $expectedPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
        $ready = $false
        foreach ($instance in $instances) {
            $instanceProject = [string](Get-UnityCliProperty -InputObject $instance -Name 'project')
            $instanceState = [string](Get-UnityCliProperty -InputObject $instance -Name 'state')
            if ([string]::IsNullOrWhiteSpace($instanceProject)) {
                continue
            }

            $instancePath = [System.IO.Path]::GetFullPath($instanceProject).TrimEnd('\', '/')
            if ([string]::Equals($instancePath, $expectedPath, [System.StringComparison]::OrdinalIgnoreCase) -and
                $instanceState -eq 'ready') {
                $ready = $true
                break
            }
        }

        if (-not $ready) {
            throw "Unity CLI did not report a ready Editor for '$expectedPath'."
        }

        $catalogEnvelope = Invoke-UnityCliJson -Arguments @(
            'command',
            '--project-path',
            $ProjectPath,
            '--query',
            'lch_test_level',
            '--detail',
            'compact',
            '--format',
            'json',
            '--no-banner',
            '--non-interactive'
        )

        $catalogData = Get-UnityCliProperty -InputObject $catalogEnvelope -Name 'data'
        $commandNames = @(
            @(Get-UnityCliProperty -InputObject $catalogData -Name 'commands') |
                ForEach-Object { [string](Get-UnityCliProperty -InputObject $_ -Name 'name') }
        )

        foreach ($requiredCommand in @('lch_test_level_launch', 'lch_test_level_status')) {
            if ($commandNames -notcontains $requiredCommand) {
                throw "Unity Editor does not expose '$requiredCommand'."
            }
        }

        $FailureReason.Value = $null
        return $true
    }
    catch {
        $FailureReason.Value = $_.Exception.Message
        return $false
    }
}

function Resolve-UnityTestLevelTransport {
    param(
        [Parameter(Mandatory)]
        [ValidateSet('Auto', 'Cli', 'Bridge')]
        [string]$Transport,

        [Parameter(Mandatory)]
        [string]$ProjectPath
    )

    if ($Transport -eq 'Bridge') {
        Write-Host '[transport] File automation bridge (explicit).'
        return 'Bridge'
    }

    $failureReason = $null
    if (Test-UnityCliTestLevelTransport -ProjectPath $ProjectPath -FailureReason ([ref]$failureReason)) {
        Write-Host '[transport] Unity CLI.'
        return 'Cli'
    }

    if ($Transport -eq 'Cli') {
        throw "Unity CLI transport is unavailable: $failureReason"
    }

    Write-Host "[fallback] Unity CLI unavailable: $failureReason"
    Write-Host '[transport] File automation bridge.'
    return 'Bridge'
}

function Invoke-UnityCliRecompile {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [int]$TimeoutSeconds = 120,

        [int]$PollMilliseconds = 250
    )

    [void](Invoke-UnityCliCommandResult `
        -ProjectPath $ProjectPath `
        -Command 'recompile' `
        -CommandArguments @('--focus', 'false'))

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = $null
    $lastConnectionError = $null

    while ((Get-Date) -lt $deadline) {
        try {
            $result = Invoke-UnityCliCommandResult -ProjectPath $ProjectPath -Command 'recompile_status'
            $status = [string](Get-UnityCliProperty -InputObject $result -Name 'status')
            $failed = Get-UnityCliProperty -InputObject $result -Name 'failed'

            if ($status -ne $lastStatus) {
                Write-Host "[$status] Unity script recompilation."
                $lastStatus = $status
            }

            if ($failed -eq $true) {
                $errors = @(Get-UnityCliProperty -InputObject $result -Name 'errors') -join '; '
                throw "compile_failed: Unity script recompilation failed: $errors"
            }

            if ($status -in @('completed', 'up_to_date')) {
                return $result
            }

            $lastConnectionError = $null
        }
        catch {
            if ($_.Exception.Message -like 'compile_failed:*') {
                throw ($_.Exception.Message -replace '^compile_failed:\s*', '')
            }

            # Domain reload temporarily removes the Pipeline endpoint. Retry until deadline.
            $lastConnectionError = $_.Exception.Message
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    }

    $suffix = if ([string]::IsNullOrWhiteSpace($lastConnectionError)) { '' } else { " Last error: $lastConnectionError" }
    throw "Timeout waiting for Unity script recompilation.$suffix"
}

function Invoke-UnityCliRegenerateProjectFiles {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath
    )

    return Invoke-UnityCliCommandResult -ProjectPath $ProjectPath -Command 'lch_project_regenerate_files'
}

function Invoke-UnityCliTestLevel {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [Parameter(Mandatory)]
        [string]$LevelAddress,

        [float]$TimeScale = 1,

        [int]$TimeoutSeconds = 120,

        [int]$PollMilliseconds = 250
    )

    $effectiveTimeScale = if ($TimeScale -gt 0) { $TimeScale } else { 0 }
    $timeScaleInvariant = $effectiveTimeScale.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $launchResponse = Invoke-UnityCliCommandResult `
        -ProjectPath $ProjectPath `
        -Command 'lch_test_level_launch' `
        -CommandArguments @('--level_address', $LevelAddress, '--time_scale', $timeScaleInvariant)

    if ($null -eq $launchResponse) {
        throw 'lch_test_level_launch returned an empty result.'
    }

    $requestId = [string](Get-UnityCliProperty -InputObject $launchResponse -Name 'requestId')
    if ([string]::IsNullOrWhiteSpace($requestId)) {
        throw 'lch_test_level_launch returned no requestId.'
    }

    $launchState = [string](Get-UnityCliProperty -InputObject $launchResponse -Name 'state')
    $launchMessage = [string](Get-UnityCliProperty -InputObject $launchResponse -Name 'message')
    Write-Host "[$launchState] $launchMessage"
    if ($launchState -in @('failed', 'busy')) {
        throw "${launchState}: $launchMessage"
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastState = $launchState
    $lastConnectionError = $null

    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-UnityCliCommandResult -ProjectPath $ProjectPath -Command 'lch_test_level_status'
            if ($null -eq $response) {
                throw 'lch_test_level_status returned an empty result.'
            }

            $responseRequestId = [string](Get-UnityCliProperty -InputObject $response -Name 'requestId')
            if ($responseRequestId -ne $requestId) {
                Start-Sleep -Milliseconds $PollMilliseconds
                continue
            }

            $state = [string](Get-UnityCliProperty -InputObject $response -Name 'state')
            $message = [string](Get-UnityCliProperty -InputObject $response -Name 'message')
            if ($state -ne $lastState) {
                Write-Host "[$state] $message"
                $lastState = $state
            }

            if ($state -eq 'completed') {
                return $response
            }

            if ($state -in @('failed', 'busy')) {
                throw "${state}: $message"
            }

            $lastConnectionError = $null
        }
        catch {
            if ($_.Exception.Message -match '^(failed|busy):') {
                throw
            }

            # Entering/exiting Play Mode can briefly remove the Pipeline endpoint.
            $lastConnectionError = $_.Exception.Message
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    }

    $suffix = if ([string]::IsNullOrWhiteSpace($lastConnectionError)) { '' } else { " Last error: $lastConnectionError" }
    throw "Timeout waiting for lch_test_level_status for request '$requestId'.$suffix"
}
