param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [int]$Port = 8765
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$serverPath = Join-Path $PSScriptRoot 'server.js'
$configPath = Join-Path $PSScriptRoot 'device-log-collector.config.json'
$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$lanIp = Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object {
        $_.IPAddress -notlike '127.*' `
            -and $_.IPAddress -notlike '169.254.*' `
            -and $_.InterfaceAlias -notlike '*WSL*' `
            -and $_.InterfaceAlias -notlike '*Loopback*'
    } |
    Sort-Object `
        @{ Expression = { if ($_.PrefixOrigin -eq 'Dhcp') { 0 } else { 1 } } }, `
        @{ Expression = { if ($_.InterfaceAlias -match 'Wi-Fi|Ethernet') { 0 } else { 1 } } }, `
        InterfaceMetric |
    Select-Object -ExpandProperty IPAddress -First 1

Write-Host "Device log collector"
Write-Host "Repository: $RepositoryRoot"
Write-Host "Local URL:  http://localhost:$Port/upload"
if ($lanIp) {
    Write-Host "LAN URL:    http://$lanIp`:$Port/upload"
}
Write-Host "Output:     $(Join-Path $RepositoryRoot $config.outputRoot)"
Write-Host ""

Push-Location $RepositoryRoot
try {
    node $serverPath --port $Port
}
finally {
    Pop-Location
}
