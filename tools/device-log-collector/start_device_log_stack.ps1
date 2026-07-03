param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [int]$Port = 8765,
    [string]$NgrokDomain = 'ladle-substance-spray.ngrok-free.dev',
    [int]$HealthCheckIntervalSeconds = 15,
    [int]$RestartDelaySeconds = 3,
    [switch]$Once
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$collectorDir = $PSScriptRoot
$serverPath = Join-Path $collectorDir 'server.js'
$supervisorLogPath = Join-Path $collectorDir 'device-log-stack.supervisor.log'
$collectorStdoutPath = Join-Path $collectorDir 'collector.supervised.stdout.log'
$collectorStderrPath = Join-Path $collectorDir 'collector.supervised.stderr.log'
$ngrokStdoutPath = Join-Path $collectorDir 'ngrok.supervised.stdout.log'
$ngrokStderrPath = Join-Path $collectorDir 'ngrok.supervised.stderr.log'
$localHealthUrl = "http://127.0.0.1:$Port/health"
$publicHealthUrl = "https://$NgrokDomain/health"

function Write-StackLog {
    param([string]$Message)

    $line = "[{0}] {1}" -f (Get-Date).ToString('o'), $Message
    Add-Content -LiteralPath $supervisorLogPath -Value $line -Encoding UTF8
    Write-Host $line
}

function Resolve-ExecutablePath {
    param(
        [string]$CommandName,
        [string[]]$FallbackPaths
    )

    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    if ($command -and $command.Source) {
        return $command.Source
    }

    foreach ($path in $FallbackPaths) {
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }

    throw "Executable not found: $CommandName"
}

function Get-CollectorProcesses {
    Get-CimInstance Win32_Process -Filter "Name = 'node.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -and $_.CommandLine.Replace('/', '\') -match '\\device-log-collector\\server\.js'
        }
}

function Get-NgrokProcesses {
    Get-CimInstance Win32_Process -Filter "Name = 'ngrok.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -and
            $_.CommandLine -match '\bhttp\b' -and
            ($_.CommandLine -match [regex]::Escape([string]$Port) -or $_.CommandLine -match [regex]::Escape($NgrokDomain))
        }
}

function Stop-CimProcesses {
    param(
        [object[]]$Processes,
        [string]$Reason
    )

    foreach ($process in $Processes) {
        try {
            Write-StackLog "Stopping PID $($process.ProcessId) ($Reason)."
            Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
        }
        catch {
            Write-StackLog "Failed to stop PID $($process.ProcessId): $($_.Exception.Message)"
        }
    }
}

function Test-HttpHealth {
    param(
        [string]$Url,
        [hashtable]$Headers = @{},
        [int]$TimeoutSeconds = 5
    )

    try {
        $response = Invoke-RestMethod -Method Get -Uri $Url -Headers $Headers -TimeoutSec $TimeoutSeconds
        return $response -and $response.ok -eq $true
    }
    catch {
        return $false
    }
}

function Ensure-Collector {
    if (Test-HttpHealth -Url $localHealthUrl) {
        return
    }

    Stop-CimProcesses -Processes @(Get-CollectorProcesses) -Reason 'collector health failed'
    Start-Sleep -Seconds $RestartDelaySeconds

    $nodePath = Resolve-ExecutablePath `
        -CommandName 'node' `
        -FallbackPaths @(
            (Join-Path $env:ProgramFiles 'nodejs\node.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\nodejs\node.exe')
        )

    Write-StackLog "Starting collector on port $Port."
    Start-Process `
        -FilePath $nodePath `
        -ArgumentList @($serverPath, '--port', $Port) `
        -WorkingDirectory $RepositoryRoot `
        -RedirectStandardOutput $collectorStdoutPath `
        -RedirectStandardError $collectorStderrPath `
        -WindowStyle Hidden | Out-Null

    $deadline = (Get-Date).AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 500
        if (Test-HttpHealth -Url $localHealthUrl) {
            Write-StackLog "Collector health OK: $localHealthUrl"
            return
        }
    } while ((Get-Date) -lt $deadline)

    throw "Collector failed health check: $localHealthUrl"
}

function Ensure-Ngrok {
    $headers = @{ 'ngrok-skip-browser-warning' = 'true' }
    if (Test-HttpHealth -Url $publicHealthUrl -Headers $headers -TimeoutSeconds 10) {
        return
    }

    Stop-CimProcesses -Processes @(Get-NgrokProcesses) -Reason 'ngrok health failed'
    Start-Sleep -Seconds $RestartDelaySeconds

    $ngrokPath = Resolve-ExecutablePath `
        -CommandName 'ngrok' `
        -FallbackPaths @(
            (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages\Ngrok.Ngrok_Microsoft.Winget.Source_8wekyb3d8bbwe\ngrok.exe')
        )

    Write-StackLog "Starting ngrok tunnel https://$NgrokDomain -> localhost:$Port."
    Start-Process `
        -FilePath $ngrokPath `
        -ArgumentList @('http', "--domain=$NgrokDomain", $Port, '--log=stdout', '--log-format=json') `
        -WorkingDirectory $RepositoryRoot `
        -RedirectStandardOutput $ngrokStdoutPath `
        -RedirectStandardError $ngrokStderrPath `
        -WindowStyle Hidden | Out-Null

    $deadline = (Get-Date).AddSeconds(30)
    do {
        Start-Sleep -Seconds 1
        if (Test-HttpHealth -Url $publicHealthUrl -Headers $headers -TimeoutSeconds 10) {
            Write-StackLog "ngrok public health OK: $publicHealthUrl"
            return
        }
    } while ((Get-Date) -lt $deadline)

    throw "ngrok failed health check: $publicHealthUrl"
}

if (-not (Test-Path -LiteralPath $serverPath)) {
    throw "Collector server not found: $serverPath"
}

Write-StackLog "Device log stack supervisor started. RepositoryRoot=$RepositoryRoot Port=$Port NgrokDomain=$NgrokDomain Once=$($Once.IsPresent)"

do {
    try {
        Ensure-Collector
        Ensure-Ngrok
    }
    catch {
        Write-StackLog "Health cycle failed: $($_.Exception.Message)"
    }

    if ($Once.IsPresent) {
        break
    }

    Start-Sleep -Seconds $HealthCheckIntervalSeconds
} while ($true)
