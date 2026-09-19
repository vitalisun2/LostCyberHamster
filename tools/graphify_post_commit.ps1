[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Resolve-GraphifyExecutable {
    $command = Get-Command graphify -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $uvExecutable = Join-Path $env:USERPROFILE '.local\bin\graphify.exe'
    if (Test-Path -LiteralPath $uvExecutable) {
        return $uvExecutable
    }

    throw 'Graphify executable was not found. Run tools/setup_graphify.ps1.'
}

try {
    Write-Host '[graphify hook] rebuilding tracked project map...'
    $graphifyExecutable = Resolve-GraphifyExecutable
    & $graphifyExecutable update .
    if ($LASTEXITCODE -ne 0) {
        throw "graphify update failed with exit code $LASTEXITCODE."
    }

    $mapChanges = @(git status --porcelain -- graphify-out)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect graphify-out status.'
    }

    if ($mapChanges.Count -eq 0) {
        Write-Host '[graphify hook] map unchanged.'
        exit 0
    }

    Invoke-Git @('add', '--', 'graphify-out')
    $stagedMapChanges = @(git diff --cached --quiet -- graphify-out)
    if ($LASTEXITCODE -eq 0) {
        Write-Host '[graphify hook] map changes disappeared before commit.'
        exit 0
    }

    Write-Host '[graphify hook] committing refreshed project map...'
    $env:GRAPHIFY_SKIP_HOOK = '1'
    Invoke-Git @('commit', '--only', 'graphify-out', '-m', 'chore: refresh Graphify project map')
    Write-Host '[graphify hook] map commit complete.'
}
catch {
    Write-Warning "[graphify hook] $($_.Exception.Message)"
    exit 0
}
