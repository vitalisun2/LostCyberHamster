param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [int]$Port = 8765,
    [string]$NgrokDomain = 'ladle-substance-spray.ngrok-free.dev',
    [switch]$Json
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$serverPath = Join-Path $PSScriptRoot 'server.js'
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

$collectorProcesses = Get-CommandLineProcesses -Name 'node.exe' -Predicate {
    $_.CommandLine -and $_.CommandLine.Replace('/', '\') -match '\\device-log-collector\\server\.js'
}

$ngrokProcesses = Get-CommandLineProcesses -Name 'ngrok.exe' -Predicate {
    $_.CommandLine -and
    $_.CommandLine -match '\bhttp\b' -and
    ($_.CommandLine -match [regex]::Escape([string]$Port) -or $_.CommandLine -match [regex]::Escape($NgrokDomain))
}

$supervisorProcesses = Get-CommandLineProcesses -Name 'powershell.exe' -Predicate {
    $_.CommandLine -and $_.CommandLine -match [regex]::Escape('start_device_log_stack.ps1')
}

$result = [pscustomobject]@{
    repositoryRoot = $RepositoryRoot
    localHealthUrl = $localHealthUrl
    publicHealthUrl = $publicHealthUrl
    localHealth = Test-HttpHealth -Url $localHealthUrl
    publicHealth = Test-HttpHealth -Url $publicHealthUrl -Headers @{ 'ngrok-skip-browser-warning' = 'true' } -TimeoutSeconds 10
    supervisorProcesses = $supervisorProcesses
    collectorProcesses = $collectorProcesses
    ngrokProcesses = $ngrokProcesses
    supervisorLog = Join-Path $PSScriptRoot 'device-log-stack.supervisor.log'
}

if ($Json.IsPresent) {
    $result | ConvertTo-Json -Depth 6
}
else {
    $result | Format-List
}
