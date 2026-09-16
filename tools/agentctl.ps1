param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

# Agent Orchestrator lives in its own repository next to this one:
# C:\Personal\crystal-wave\repos\agent-orchestrator
$orchestratorRoot = Join-Path (Split-Path (Split-Path $PSScriptRoot)) 'agent-orchestrator'
$project = Join-Path $orchestratorRoot 'AgentOrchestrator.csproj'
if (-not (Test-Path $project)) {
    Write-Error "Agent Orchestrator project not found: $project"
    exit 1
}
dotnet run --project $project -- agentctl @Arguments
exit $LASTEXITCODE
