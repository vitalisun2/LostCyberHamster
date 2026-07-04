param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$ComposeFile = (Join-Path $PSScriptRoot 'docker-compose.yml'),
    [string]$EnvFile = (Join-Path $PSScriptRoot '.env.local'),
    [int]$Port = 8765,
    [string]$NgrokDomain = 'ladle-substance-spray.ngrok-free.dev',
    [string]$DeviceLogOutputRootHost = '',
    [int]$DockerStartupTimeoutSeconds = 120,
    [int]$HealthTimeoutSeconds = 120,
    [switch]$Rebuild,
    [switch]$SkipLegacyStop,
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$composeProjectName = 'lostcyberhamster-device-logs'
$collectorImage = 'lostcyberhamster/device-log-collector:local'
$ngrokImage = 'lostcyberhamster/device-log-ngrok:local'
$localHealthUrl = "http://127.0.0.1:$Port/health"
$publicHealthUrl = "https://$NgrokDomain/health"
$dropboxProjectRoot = 'crystal_wave'
$dropboxOutputRelativePath = 'LostCyberHamster_DeviceLogs\android'
$logRoot = $null

function Write-Step {
    param([string]$Message)
    if (-not $Json.IsPresent) {
        Write-Host "[device-log-docker] $Message"
    }
}

function Invoke-Docker {
    param([string[]]$Arguments)

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & docker @Arguments
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($LASTEXITCODE -ne 0) {
        throw "docker $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

function Invoke-DockerCompose {
    param([string[]]$Arguments)

    Invoke-Docker -Arguments (@(
        'compose',
        '--env-file', $EnvFile,
        '-f', $ComposeFile
    ) + $Arguments)
}

function Test-DockerReady {
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & docker info 1>$null 2>$null
        return $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Test-DockerImageExists {
    param([string]$Image)

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & docker image inspect $Image 1>$null 2>$null
        return $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Start-DockerDesktopIfNeeded {
    if (Test-DockerReady) {
        return
    }

    $dockerDesktopCandidates = @(
        (Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe')
    )

    if (${env:ProgramFiles(x86)}) {
        $dockerDesktopCandidates += Join-Path ${env:ProgramFiles(x86)} 'Docker\Docker\Docker Desktop.exe'
    }

    $dockerDesktopPath = $dockerDesktopCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ($dockerDesktopPath) {
        Write-Step "Docker daemon is not ready; starting Docker Desktop."
        Start-Process -FilePath $dockerDesktopPath -WindowStyle Hidden
    }

    $deadline = (Get-Date).AddSeconds($DockerStartupTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-DockerReady) {
            return
        }

        Start-Sleep -Seconds 3
    }

    throw "Docker daemon is not ready after $DockerStartupTimeoutSeconds seconds."
}

function Read-EnvValue {
    param(
        [string]$Path,
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    foreach ($line in Get-Content -LiteralPath $Path -Encoding UTF8) {
        if ($line -match "^\s*$([regex]::Escape($Name))\s*=\s*(.+?)\s*$") {
            return $matches[1].Trim('"')
        }
    }

    return $null
}

function ConvertTo-HostPath {
    param([string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
}

function ConvertTo-ComposePath {
    param([string]$Path)

    return $Path.Replace('\', '/')
}

function Get-DropboxOutputRootCandidates {
    $candidates = @(
        (Join-Path 'C:\Dropbox\exchange' (Join-Path $dropboxProjectRoot $dropboxOutputRelativePath))
    )

    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $candidates += Join-Path (Join-Path $env:USERPROFILE 'Dropbox\exchange') (Join-Path $dropboxProjectRoot $dropboxOutputRelativePath)
    }

    $candidates | Select-Object -Unique
}

function Test-DropboxCandidateAnchorExists {
    param([string]$Candidate)

    $deviceLogRoot = Split-Path -Parent $Candidate
    $projectRoot = Split-Path -Parent $deviceLogRoot
    $exchangeRoot = Split-Path -Parent $projectRoot

    return (
        (Test-Path -LiteralPath $Candidate) -or
        (Test-Path -LiteralPath $projectRoot) -or
        (Test-Path -LiteralPath $exchangeRoot)
    )
}

function Resolve-DeviceLogOutputRootHost {
    if (-not [string]::IsNullOrWhiteSpace($DeviceLogOutputRootHost)) {
        return ConvertTo-HostPath -Path $DeviceLogOutputRootHost.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($env:DEVICE_LOG_OUTPUT_ROOT_HOST)) {
        return ConvertTo-HostPath -Path $env:DEVICE_LOG_OUTPUT_ROOT_HOST.Trim()
    }

    foreach ($candidate in Get-DropboxOutputRootCandidates) {
        if (Test-DropboxCandidateAnchorExists -Candidate $candidate) {
            return ConvertTo-HostPath -Path $candidate
        }
    }

    $envFileOutputRoot = Read-EnvValue -Path $EnvFile -Name 'DEVICE_LOG_OUTPUT_ROOT_HOST'
    if (-not [string]::IsNullOrWhiteSpace($envFileOutputRoot)) {
        $hostPath = ConvertTo-HostPath -Path $envFileOutputRoot.Trim()
        if (Test-Path -LiteralPath $hostPath) {
            return $hostPath
        }
    }

    throw "Dropbox Exchange folder was not found. Install/start Dropbox or pass -DeviceLogOutputRootHost '<path>' explicitly. Expected default: C:\Dropbox\exchange\$dropboxProjectRoot\$dropboxOutputRelativePath"
}

function Get-NgrokAuthtoken {
    if (-not [string]::IsNullOrWhiteSpace($env:NGROK_AUTHTOKEN)) {
        return $env:NGROK_AUTHTOKEN.Trim()
    }

    $envFileToken = Read-EnvValue -Path $EnvFile -Name 'NGROK_AUTHTOKEN'
    if (-not [string]::IsNullOrWhiteSpace($envFileToken)) {
        return $envFileToken.Trim()
    }

    $ngrokConfigPath = Join-Path $env:LOCALAPPDATA 'ngrok\ngrok.yml'
    if (Test-Path -LiteralPath $ngrokConfigPath) {
        $rawConfig = Get-Content -LiteralPath $ngrokConfigPath -Raw -Encoding UTF8
        if ($rawConfig -match '(?m)^\s*authtoken\s*:\s*(\S+)\s*$') {
            return $matches[1].Trim()
        }
    }

    throw "NGROK_AUTHTOKEN is missing. Set it in the environment or run 'ngrok config add-authtoken <token>' once."
}

function Ensure-LocalEnvFile {
    param([string]$OutputRootHost)

    $token = Get-NgrokAuthtoken
    $envDirectory = Split-Path -Parent $EnvFile
    New-Item -ItemType Directory -Force -Path $envDirectory | Out-Null

    $content = @(
        "NGROK_AUTHTOKEN=$token",
        "NGROK_DOMAIN=$NgrokDomain",
        "DEVICE_LOG_OUTPUT_ROOT_HOST=$(ConvertTo-ComposePath -Path $OutputRootHost)"
    )

    Set-Content -LiteralPath $EnvFile -Value $content -Encoding ASCII
}

function Get-CommandLineProcesses {
    param(
        [string]$Name,
        [scriptblock]$Predicate
    )

    @(Get-CimInstance Win32_Process -Filter "Name = '$Name'" -ErrorAction SilentlyContinue |
        Where-Object $Predicate)
}

function Stop-MatchedProcesses {
    param(
        [string]$Label,
        [object[]]$Processes
    )

    foreach ($process in $Processes) {
        if ($process.ProcessId -eq $PID) {
            continue
        }

        Write-Step "Stopping legacy $Label process pid=$($process.ProcessId)."
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Stop-LegacyLocalStack {
    $legacySupervisors = @()
    foreach ($processName in @('powershell.exe', 'pwsh.exe')) {
        $legacySupervisors += Get-CommandLineProcesses -Name $processName -Predicate {
            $_.CommandLine -and $_.CommandLine -match [regex]::Escape('start_device_log_stack.ps1')
        }
    }

    $legacyLaunchers = Get-CommandLineProcesses -Name 'cmd.exe' -Predicate {
        $_.CommandLine -and $_.CommandLine -match [regex]::Escape('LostCyberHamster\device-log-stack\start_device_log_stack.cmd')
    }

    $legacyCollectors = Get-CommandLineProcesses -Name 'node.exe' -Predicate {
        $_.CommandLine -and $_.CommandLine.Replace('/', '\') -match '\\device-log-collector\\server\.js'
    }

    $legacyNgrok = Get-CommandLineProcesses -Name 'ngrok.exe' -Predicate {
        $_.CommandLine -and
            $_.CommandLine -match '\bhttp\b' -and
            ($_.CommandLine -match [regex]::Escape([string]$Port) -or $_.CommandLine -match [regex]::Escape($NgrokDomain))
    }

    Stop-MatchedProcesses -Label 'supervisor' -Processes $legacySupervisors
    Stop-MatchedProcesses -Label 'startup launcher' -Processes $legacyLaunchers
    Stop-MatchedProcesses -Label 'collector' -Processes $legacyCollectors
    Stop-MatchedProcesses -Label 'ngrok' -Processes $legacyNgrok
}

function Test-HttpHealth {
    param(
        [string]$Url,
        [hashtable]$Headers = @{},
        [int]$TimeoutSeconds = 5
    )

    try {
        $response = Invoke-RestMethod -Method Get -Uri $Url -Headers $Headers -TimeoutSec $TimeoutSeconds
        if ($response -and $response.ok -eq $true) {
            return [pscustomobject]@{
                ok = $true
                error = $null
            }
        }

        return [pscustomobject]@{
            ok = $false
            error = 'Health endpoint returned a non-ok response.'
        }
    }
    catch {
        return [pscustomobject]@{
            ok = $false
            error = $_.Exception.Message
        }
    }
}

function Wait-HttpHealth {
    param(
        [string]$Name,
        [string]$Url,
        [hashtable]$Headers = @{},
        [int]$TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastResult = $null

    while ((Get-Date) -lt $deadline) {
        $lastResult = Test-HttpHealth -Url $Url -Headers $Headers
        if ($lastResult.ok) {
            return $lastResult
        }

        Start-Sleep -Seconds 2
    }

    throw "$Name health check failed at $Url. Last error: $($lastResult.error)"
}

function Get-ComposePsJson {
    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = & docker compose --env-file $EnvFile -f $ComposeFile ps --format json 2>$null
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($output)) {
            return @()
        }

        return $output | ConvertFrom-Json
    }
    catch {
        return @()
    }
}

function Get-ComposeServiceLogTail {
    param(
        [string]$Service,
        [int]$Tail = 40
    )

    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = & docker compose --env-file $EnvFile -f $ComposeFile logs --no-color --tail $Tail $Service 2>$null
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($output)) {
            return ''
        }

        return ($output -join "`n")
    }
    catch {
        return ''
    }
}

function Wait-ComposeServiceHealthy {
    param(
        [string]$Service,
        [int]$TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = 'not_found'

    while ((Get-Date) -lt $deadline) {
        $container = @(Get-ComposePsJson) | Where-Object { $_.Service -eq $Service } | Select-Object -First 1
        if ($container) {
            $lastStatus = "state=$($container.State), health=$($container.Health), status=$($container.Status)"
            if ($container.State -eq 'running' -and $container.Health -eq 'healthy') {
                return $container
            }
        }

        Start-Sleep -Seconds 2
    }

    $logTail = Get-ComposeServiceLogTail -Service $Service
    if ([string]::IsNullOrWhiteSpace($logTail)) {
        throw "Docker Compose service '$Service' did not become healthy after $TimeoutSeconds seconds. Last status: $lastStatus"
    }

    throw "Docker Compose service '$Service' did not become healthy after $TimeoutSeconds seconds. Last status: $lastStatus. Recent logs:`n$logTail"
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker CLI was not found in PATH. Install Docker Desktop before using the Android device log Docker stack.'
}

if (-not (Test-Path -LiteralPath $ComposeFile)) {
    throw "Compose file not found: $ComposeFile"
}

$logRoot = Resolve-DeviceLogOutputRootHost
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
Write-Step "Using device log output root: $logRoot"
Ensure-LocalEnvFile -OutputRootHost $logRoot
Start-DockerDesktopIfNeeded

if (-not $SkipLegacyStop.IsPresent) {
    Stop-LegacyLocalStack
}

Write-Step "Starting Docker Compose stack '$composeProjectName'."
$upArguments = @('up', '-d', '--quiet-pull', '--remove-orphans')
if (
    $Rebuild.IsPresent -or
    -not (Test-DockerImageExists -Image $collectorImage) -or
    -not (Test-DockerImageExists -Image $ngrokImage)
) {
    $upArguments = @('up', '-d', '--build', '--quiet-pull', '--remove-orphans')
}

Invoke-DockerCompose -Arguments $upArguments

Write-Step "Waiting for local collector health."
$localHealth = Wait-HttpHealth -Name 'Local collector' -Url $localHealthUrl -TimeoutSeconds $HealthTimeoutSeconds

Write-Step "Waiting for ngrok container health."
$ngrokContainer = Wait-ComposeServiceHealthy -Service 'ngrok' -TimeoutSeconds $HealthTimeoutSeconds

Write-Step "Verifying public ngrok route health."
$publicHealth = Wait-HttpHealth `
    -Name 'Public ngrok route' `
    -Url $publicHealthUrl `
    -Headers @{ 'ngrok-skip-browser-warning' = 'true' } `
    -TimeoutSeconds $HealthTimeoutSeconds

$result = [pscustomobject]@{
    mode = 'DockerCompose'
    project = $composeProjectName
    repositoryRoot = $RepositoryRoot
    composeFile = $ComposeFile
    envFile = $EnvFile
    logRoot = $logRoot
    localHealthUrl = $localHealthUrl
    publicHealthUrl = $publicHealthUrl
    localHealth = $localHealth
    publicHealth = $publicHealth
    ngrokContainer = $ngrokContainer
    containers = Get-ComposePsJson
}

if ($Json.IsPresent) {
    $result | ConvertTo-Json -Depth 8
}
else {
    $result | Format-List
}
