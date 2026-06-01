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

    $allLines = @(Get-Content -Path $Path -ErrorAction Stop)
    $result = New-Object System.Collections.Generic.List[string]

    $currentChannel = ""
    $includeCurrentBlock = $false

    foreach ($line in $allLines) {
        if ($line -match "\[CH=(?<ch>STAB|BOT|ECO)\]") {
            $currentChannel = $Matches.ch

            $channelMatch = ($SelectedChannel -eq "ALL" -or $currentChannel -eq $SelectedChannel)
            $eventMatch = ([string]::IsNullOrWhiteSpace($SelectedEvent) -or ($line -match $SelectedEvent))
            $includeCurrentBlock = $channelMatch -and $eventMatch

            if ($includeCurrentBlock) {
                $result.Add($line)
            }
            continue
        }

        if ($includeCurrentBlock) {
            $result.Add($line)
        }
    }

    return @($result)
}

$mode = "tagged"
$lines = @()

$lines = @(Get-TaggedLogLines -Path $diagPath -SelectedChannel $Channel -SelectedEvent $Event)

$lines = @($lines)

if ($SummaryOnly) {
    $summaryLines = $lines
    if ($mode -eq "tagged" -and (Test-Path $diagPath)) {
        $summaryLines = @(Get-Content -Path $diagPath -ErrorAction Stop)
    }

    $channelCounts = [ordered]@{
        STAB = 0
        BOT  = 0
        ECO  = 0
        OTHER = 0
    }

    $lastTaggedChannel = ""

    foreach ($line in $summaryLines) {
        if ($line -match [regex]::Escape("[CH=STAB]")) { $channelCounts.STAB++; $lastTaggedChannel = "STAB"; continue }
        if ($line -match [regex]::Escape("[CH=BOT]"))  { $channelCounts.BOT++;  $lastTaggedChannel = "BOT";  continue }
        if ($line -match [regex]::Escape("[CH=ECO]"))  { $channelCounts.ECO++;  $lastTaggedChannel = "ECO";  continue }

        if ($mode -eq "tagged") {
            switch ($lastTaggedChannel) {
                "STAB" { $channelCounts.STAB++; continue }
                "BOT"  { $channelCounts.BOT++;  continue }
                "ECO"  { $channelCounts.ECO++;  continue }
            }
        }

        $channelCounts.OTHER++
    }

    Write-Output ("mode={0}" -f $mode)
    Write-Output ("total={0}" -f $summaryLines.Count)
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
