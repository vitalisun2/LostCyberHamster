param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$ComposeFile = (Join-Path $PSScriptRoot 'docker-compose.yml'),
    [string]$EnvFile = (Join-Path $PSScriptRoot '.env.local'),
    [int]$Port = 8765,
    [string]$NgrokDomain = 'ladle-substance-spray.ngrok-free.dev',
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$localHealthUrl = "http://127.0.0.1:$Port/health"
$publicHealthUrl = "https://$NgrokDomain/health"

function Get-CommandLineProcesses {
    param(
        [string]$Name,
        [scriptblock]$Predicate
    )

    @(Get-CimInstance Win32_Process -Filter "Name = '$Name'" -ErrorAction SilentlyContinue |
        Where-Object $Predicate |
        ForEach-Object {
            [pscustomobject]@{
                processId = $_.ProcessId
                commandLine = $_.CommandLine
            }
        })
}

function Test-HttpHealth {
    param(
        [string]$Url,
        [hashtable]$Headers = @{},
        [int]$TimeoutSeconds = 5
    )

    try {
        $response = Invoke-RestMethod -Method Get -Uri $Url -Headers $Headers -TimeoutSec $TimeoutSeconds
        return [pscustomobject]@{
            ok = $response -and $response.ok -eq $true
            error = $null
        }
    }
    catch {
        return [pscustomobject]@{
            ok = $false
            error = $_.Exception.Message
        }
    }
}

function Get-ComposeContainers {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        return [pscustomobject]@{
            ok = $false
            error = 'Docker CLI was not found in PATH.'
            containers = @()
        }
    }

    if (-not (Test-Path -LiteralPath $ComposeFile)) {
        return [pscustomobject]@{
            ok = $false
            error = "Compose file not found: $ComposeFile"
            containers = @()
        }
    }

    if (-not (Test-Path -LiteralPath $EnvFile)) {
        return [pscustomobject]@{
            ok = $false
            error = "Local env file not found: $EnvFile. Run ensure_device_log_docker_stack.ps1 first."
            containers = @()
        }
    }

    try {
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = & docker compose --env-file $EnvFile -f $ComposeFile ps --format json 2>$null
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        if ($LASTEXITCODE -ne 0) {
            return [pscustomobject]@{
                ok = $false
                error = "docker compose ps failed with exit code $LASTEXITCODE."
                containers = @()
            }
        }

        if ([string]::IsNullOrWhiteSpace($output)) {
            $containers = @()
        }
        else {
            $containers = @($output | ConvertFrom-Json)
        }

        return [pscustomobject]@{
            ok = $true
            error = $null
            containers = $containers
        }
    }
    catch {
        return [pscustomobject]@{
            ok = $false
            error = $_.Exception.Message
            containers = @()
        }
    }
}

$collectorProcesses = Get-CommandLineProcesses -Name 'node.exe' -Predicate {
    $_.CommandLine -and $_.CommandLine.Replace('/', '\') -match '\\device-log-collector\\server\.js'
}

$ngrokProcesses = Get-CommandLineProcesses -Name 'ngrok.exe' -Predicate {
    $_.CommandLine -and
    $_.CommandLine -match '\bhttp\b' -and
    ($_.CommandLine -match [regex]::Escape([string]$Port) -or $_.CommandLine -match [regex]::Escape($NgrokDomain))
}

$supervisorProcesses = @()
foreach ($processName in @('powershell.exe', 'pwsh.exe')) {
    $supervisorProcesses += Get-CommandLineProcesses -Name $processName -Predicate {
        $_.CommandLine -and $_.CommandLine -match [regex]::Escape('start_device_log_stack.ps1')
    }
}

$localHealth = Test-HttpHealth -Url $localHealthUrl
$publicHealth = Test-HttpHealth -Url $publicHealthUrl -Headers @{ 'ngrok-skip-browser-warning' = 'true' } -TimeoutSeconds 10
$compose = Get-ComposeContainers
$composeContainers = @($compose.containers)
$collectorContainer = $composeContainers | Where-Object {
    $_.Service -eq 'collector' -and $_.State -eq 'running' -and $_.Health -eq 'healthy'
} | Select-Object -First 1
$ngrokContainer = $composeContainers | Where-Object {
    $_.Service -eq 'ngrok' -and $_.State -eq 'running'
} | Select-Object -First 1
$dockerReady = $compose.ok -and $null -ne $collectorContainer -and $null -ne $ngrokContainer

$result = [pscustomobject]@{
    repositoryRoot = $RepositoryRoot
    mode = 'DockerCompose'
    ready = ($localHealth.ok -and $publicHealth.ok -and $dockerReady)
    dockerReady = $dockerReady
    ensureScript = Join-Path $PSScriptRoot 'ensure_device_log_docker_stack.ps1'
    composeFile = $ComposeFile
    envFile = $EnvFile
    localHealthUrl = $localHealthUrl
    publicHealthUrl = $publicHealthUrl
    localHealth = $localHealth
    publicHealth = $publicHealth
    compose = $compose
    legacySupervisorProcesses = @($supervisorProcesses)
    legacyCollectorProcesses = @($collectorProcesses)
    legacyNgrokProcesses = @($ngrokProcesses)
}

if ($Json.IsPresent) {
    $result | ConvertTo-Json -Depth 8
}
else {
    $result | Format-List
}
