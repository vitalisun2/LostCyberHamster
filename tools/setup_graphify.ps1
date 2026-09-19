[CmdletBinding()]
param(
    [string]$GraphifyVersion = '0.9.61'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Invoke-Uv {
    param([Parameter(Mandatory)][string[]]$Arguments)
    if (Get-Command uv -ErrorAction SilentlyContinue) {
        & uv @Arguments
    } else {
        & python -m uv @Arguments
    }
    if ($LASTEXITCODE -ne 0) { throw "uv command failed: $($Arguments -join ' ')" }
}

if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
    throw 'Python 3.10+ is required.'
}

$pythonVersion = & python -c "import sys; print(f'{sys.version_info.major}.{sys.version_info.minor}')"
$pythonParts = $pythonVersion -split '\.'
if ([int]$pythonParts[0] -lt 3 -or ([int]$pythonParts[0] -eq 3 -and [int]$pythonParts[1] -lt 10)) {
    throw "Python 3.10+ is required; found $pythonVersion."
}

if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
    & python -m pip install --user uv
    if ($LASTEXITCODE -ne 0) { throw 'Could not install uv.' }
}

$uvToolBin = Join-Path $env:USERPROFILE '.local\bin'
if (Test-Path $uvToolBin) { $env:PATH = "$uvToolBin;$env:PATH" }

Invoke-Uv @('tool', 'install', '--force', "graphifyy==$GraphifyVersion")
Invoke-Uv @('tool', 'update-shell')

$graphify = Get-Command graphify -ErrorAction SilentlyContinue
if (-not $graphify) {
    throw 'Graphify executable was not found after installation. Add %USERPROFILE%\.local\bin to PATH.'
}

& graphify --version
if ($LASTEXITCODE -ne 0) { throw 'Graphify version check failed.' }

& graphify install --project --platform codex
if ($LASTEXITCODE -ne 0) { throw 'Codex project integration failed.' }

& graphify hook install
if ($LASTEXITCODE -ne 0) { throw 'Git hook installation failed.' }

$hooksPath = (& git rev-parse --git-path hooks).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($hooksPath)) {
    throw 'Could not resolve Git hooks path.'
}
$projectHook = Join-Path $repoRoot '.githooks\post-commit'
$installedHook = Join-Path $hooksPath 'post-commit'
Copy-Item -LiteralPath $projectHook -Destination $installedHook -Force

$projectHookHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $projectHook).Hash
$installedHookHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $installedHook).Hash
if ($projectHookHash -ne $installedHookHash) {
    throw 'Installed post-commit hook does not match .githooks/post-commit.'
}

$hookStatus = (& graphify hook status | Out-String).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Git hook status check failed.' }
Write-Host $hookStatus
if ($hookStatus -notmatch '(?m)^post-commit: installed\r?$') {
    throw 'Project post-commit wrapper was not recognized by Graphify.'
}

Write-Host "Graphify $GraphifyVersion setup complete."
