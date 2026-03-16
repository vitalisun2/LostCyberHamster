param(
    [ValidateSet("STAB", "BOT", "ECO", "ALL")]
    [string]$Channel = "ALL",

    [string]$Event = "",

    [int]$Tail = 200,

    [string]$ProjectRoot = "",

    [switch]$SummaryOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Get-Location).Path
}

$editorLogs = Join-Path $ProjectRoot "LostCyberHamster/EditorLogs"
$diagPath = Join-Path $editorLogs "diagnostic_log.txt"

function Get-TaggedLogLines {
    param(
        [string]$Path,
        [string]$SelectedChannel,
        [string]$SelectedEvent
    )

    if (-not (Test-Path $Path)) { return @() }

    $lines = Get-Content -Path $Path -ErrorAction Stop
    if ($SelectedChannel -ne "ALL") {
        $needlePattern = [regex]::Escape("[CH=$SelectedChannel]")
        $lines = $lines | Where-Object { $_ -match $needlePattern }
    }

    if (-not [string]::IsNullOrWhiteSpace($SelectedEvent)) {
        $lines = $lines | Where-Object { $_ -match $SelectedEvent }
    }

    return @($lines)
}

$mode = "tagged"
$lines = @()

$lines = @(Get-TaggedLogLines -Path $diagPath -SelectedChannel $Channel -SelectedEvent $Event)

$lines = @($lines)

if ($SummaryOnly) {
    $channelCounts = [ordered]@{
        STAB = 0
        BOT  = 0
        ECO  = 0
        OTHER = 0
    }

    foreach ($line in $lines) {
        if ($line -match [regex]::Escape("[CH=STAB]")) { $channelCounts.STAB++; continue }
        if ($line -match [regex]::Escape("[CH=BOT]"))  { $channelCounts.BOT++; continue }
        if ($line -match [regex]::Escape("[CH=ECO]"))  { $channelCounts.ECO++; continue }

        if ($mode -eq "split") {
            $channelCounts.OTHER++
        } else {
            $channelCounts.OTHER++
        }
    }

    Write-Output ("mode={0}" -f $mode)
    Write-Output ("total={0}" -f $lines.Count)
    Write-Output ("stab={0}" -f $channelCounts.STAB)
    Write-Output ("bot={0}" -f $channelCounts.BOT)
    Write-Output ("eco={0}" -f $channelCounts.ECO)
    Write-Output ("other={0}" -f $channelCounts.OTHER)
    exit 0
}

if ($lines.Count -eq 0) {
    Write-Output ("mode={0}" -f $mode)
    Write-Output "No log lines matched filter."
    exit 0
}

$tailCount = [Math]::Max(1, $Tail)
$tailLines = $lines | Select-Object -Last $tailCount

Write-Output ("mode={0}" -f $mode)
$tailLines
