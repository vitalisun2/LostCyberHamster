[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [ValidatePattern('^[^\\/:*?"<>|]+$')]
    [string]$TaskName = 'LostCyberHamster Telegram Build Worker'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$task = Get-ScheduledTask -TaskPath '\' -ErrorAction SilentlyContinue |
    Where-Object {
        [System.StringComparer]::OrdinalIgnoreCase.Equals($_.TaskName, $TaskName) -and
        $_.TaskPath -eq '\'
    } |
    Select-Object -First 1

if ($null -eq $task) {
    Write-Host "Scheduled task not found: $TaskName"
    return
}

if ($PSCmdlet.ShouldProcess($TaskName, 'Stop and unregister exact scheduled task')) {
    Stop-ScheduledTask -InputObject $task -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -InputObject $task -Confirm:$false
    Write-Host "Removed scheduled task: $TaskName"
}

# State, logs, Telegram configs, and secrets are intentionally retained.
