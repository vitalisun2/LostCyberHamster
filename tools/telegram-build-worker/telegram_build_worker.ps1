[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $env:USERPROFILE ".codex\telegram-buffer.local.json"),
    [string]$BotApiConfigPath = (Join-Path $env:USERPROFILE ".codex\telegram-bot-api.local.json"),
    [string]$StateDirectory = (Join-Path $env:LOCALAPPDATA "LostCyberHamster\TelegramBuildWorker"),
    [string]$StatePath,
    [string]$DispatchPath,
    [string]$RepositoryRoot,
    [ValidateRange(0, 50)]
    [int]$PollTimeoutSeconds = 45,
    [ValidateRange(10, 300)]
    [int]$ProgressIntervalSeconds = 30,
    [switch]$Once
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-NormalizedPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return [System.IO.Path]::GetFullPath($Path)
}

function Get-Sha256Text {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        $hash = $sha256.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash)).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Test-PathInside {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Child,
        [Parameter(Mandatory = $true)]
        [string]$Parent
    )

    $childPath = Get-NormalizedPath -Path $Child
    $parentPath = (Get-NormalizedPath -Path $Parent).TrimEnd('\', '/')
    return $childPath.Equals($parentPath, [System.StringComparison]::OrdinalIgnoreCase) -or
        $childPath.StartsWith(
            $parentPath + [System.IO.Path]::DirectorySeparatorChar,
            [System.StringComparison]::OrdinalIgnoreCase
        )
}

function Read-WorkerState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    try {
        $state = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        $initializedProperty = $state.PSObject.Properties["initialized"]
        $nextOffsetProperty = $state.PSObject.Properties["nextOffset"]

        if ($null -eq $initializedProperty) {
            throw "Missing initialized field."
        }

        if ([bool]$initializedProperty.Value -and $null -eq $nextOffsetProperty) {
            throw "Missing nextOffset field."
        }

        return $state
    }
    catch {
        throw [System.InvalidOperationException]::new(
            "Worker state is invalid. Remove or repair the state file before restarting."
        )
    }
}

function Write-WorkerState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [long]$NextOffset
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        $null = New-Item -ItemType Directory -Path $directory -Force
    }

    $state = [ordered]@{
        initialized  = $true
        nextOffset   = $NextOffset
        updatedAtUtc = [DateTime]::UtcNow.ToString("o")
    }

    $temporaryPath = "$Path.$PID.tmp"
    try {
        $state | ConvertTo-Json | Set-Content -LiteralPath $temporaryPath -Encoding UTF8
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Read-TelegramConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw [System.IO.FileNotFoundException]::new("Telegram config file is missing.")
    }

    try {
        $config = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        throw [System.InvalidOperationException]::new("Telegram config is invalid JSON.")
    }

    $botTokenProperty = $config.PSObject.Properties["botToken"]
    $chatIdProperty = $config.PSObject.Properties["chatId"]

    if ($null -eq $botTokenProperty -or [string]::IsNullOrWhiteSpace([string]$botTokenProperty.Value)) {
        throw [System.InvalidOperationException]::new("Telegram config has no botToken.")
    }

    if ($null -eq $chatIdProperty -or [string]::IsNullOrWhiteSpace([string]$chatIdProperty.Value)) {
        throw [System.InvalidOperationException]::new("Telegram config has no chatId.")
    }

    return $config
}

function Invoke-TelegramMethod {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [hashtable]$Body
    )

    $apiBaseUrlProperty = $script:TelegramConfig.PSObject.Properties["apiBaseUrl"]
    $apiBaseUrl = if ($null -eq $apiBaseUrlProperty) { "" } else { [string]$apiBaseUrlProperty.Value }
    if ([string]::IsNullOrWhiteSpace($apiBaseUrl)) {
        $apiBaseUrl = "https://api.telegram.org"
    }

    $apiBaseUrl = $apiBaseUrl.TrimEnd("/")
    $requestUri = "$apiBaseUrl/bot$($script:TelegramConfig.botToken)/$Method"
    $requestTimeout = [Math]::Max(15, $PollTimeoutSeconds + 10)

    try {
        $response = Invoke-RestMethod `
            -Method Post `
            -Uri $requestUri `
            -Body $Body `
            -ContentType "application/x-www-form-urlencoded" `
            -TimeoutSec $requestTimeout
    }
    catch {
        throw [System.InvalidOperationException]::new("Telegram request failed.")
    }

    $okProperty = if ($null -eq $response) { $null } else { $response.PSObject.Properties["ok"] }
    if ($null -eq $okProperty -or -not [bool]$okProperty.Value) {
        throw [System.InvalidOperationException]::new("Telegram API returned an unsuccessful response.")
    }

    return $response
}

function Get-TelegramUpdates {
    param(
        [Parameter(Mandatory = $true)]
        [long]$Offset,
        [Parameter(Mandatory = $true)]
        [int]$Timeout,
        [Parameter(Mandatory = $true)]
        [int]$Limit
    )

    $response = Invoke-TelegramMethod -Method "getUpdates" -Body @{
        offset          = $Offset
        timeout         = $Timeout
        limit           = $Limit
        allowed_updates = '["channel_post"]'
    }

    $resultProperty = $response.PSObject.Properties["result"]
    if ($null -eq $resultProperty) {
        throw [System.InvalidOperationException]::new("Telegram API response has no result.")
    }

    return @($resultProperty.Value)
}

function Send-TelegramStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    $response = Invoke-TelegramMethod -Method "sendMessage" -Body @{
        chat_id              = [string]$script:TelegramConfig.chatId
        text                 = $Text
        disable_notification = "false"
    }

    $resultProperty = $response.PSObject.Properties["result"]
    if ($null -eq $resultProperty -or $null -eq $resultProperty.Value) {
        throw [System.InvalidOperationException]::new("Telegram sendMessage response has no result.")
    }

    $messageIdProperty = $resultProperty.Value.PSObject.Properties["message_id"]
    if ($null -eq $messageIdProperty) {
        throw [System.InvalidOperationException]::new("Telegram sendMessage response has no message_id.")
    }

    return [long]$messageIdProperty.Value
}

function Send-TelegramStatusSafe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    try {
        [void](Send-TelegramStatus -Text $Text)
    }
    catch {
        Write-Warning "Telegram status message could not be sent."
    }
}

function Convert-ToProcessArgument {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Value
    )

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }

    $escaped = [regex]::Replace($Value, '(\\*)"', '$1$1\"')
    $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
    return '"' + $escaped + '"'
}

function Convert-ToProcessArgumentLine {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return (($Arguments | ForEach-Object { Convert-ToProcessArgument -Value $_ }) -join " ")
}

function Start-BuildDispatch {
    $startedAtUtc = [DateTime]::UtcNow
    $stdoutLogPath = Join-Path $StateDirectory "last-dispatch.log"
    $stderrLogPath = Join-Path $StateDirectory "last-dispatch.stderr.log"
    $powerShellPath = Join-Path $PSHOME "powershell.exe"
    if (-not (Test-Path -LiteralPath $powerShellPath -PathType Leaf)) {
        $powerShellPath = "powershell.exe"
    }

    [System.IO.File]::WriteAllText($stdoutLogPath, "")
    [System.IO.File]::WriteAllText($stderrLogPath, "")

    $arguments = @(
        "-NoProfile",
        "-NonInteractive",
        "-ExecutionPolicy", "Bypass",
        "-File", $DispatchPath,
        "-RepositoryRoot", $RepositoryRoot,
        "-TelegramConfigPath", $ConfigPath,
        "-BotApiConfigPath", $BotApiConfigPath,
        "-StateRoot", $StateDirectory
    )
    $argumentLine = Convert-ToProcessArgumentLine -Arguments $arguments

    $process = Start-Process `
        -FilePath $powerShellPath `
        -ArgumentList $argumentLine `
        -WorkingDirectory $RepositoryRoot `
        -RedirectStandardOutput $stdoutLogPath `
        -RedirectStandardError $stderrLogPath `
        -WindowStyle Hidden `
        -PassThru
    [void]$process.Handle

    return [pscustomobject]@{
        Process       = $process
        StartedAtUtc  = $startedAtUtc
        StdoutLogPath = $stdoutLogPath
        StderrLogPath = $stderrLogPath
    }
}

function Format-Elapsed {
    param(
        [Parameter(Mandatory = $true)]
        [TimeSpan]$Elapsed
    )

    $hours = [Math]::Floor($Elapsed.TotalHours)
    return "{0:00}:{1:00}:{2:00}" -f $hours, $Elapsed.Minutes, $Elapsed.Seconds
}

function Get-BucketPhase {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Bucket
    )

    switch ($Bucket) {
        0 { return "command accepted" }
        10 { return "Codex started" }
        20 { return "preparing sandbox and Unity" }
        30 { return "Unity BuildPlayer" }
        40 { return "building Unity player" }
        50 { return "Bee, IL2CPP and clang" }
        60 { return "native build" }
        70 { return "Gradle and postprocess" }
        80 { return "packaging Android APK" }
        90 { return "APK ready, uploading to Telegram" }
        100 { return "APK uploaded" }
        default { return "build in progress" }
    }
}

function Format-ProgressStatus {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Percent,
        [Parameter(Mandatory = $true)]
        [string]$Phase,
        [Parameter(Mandatory = $true)]
        [TimeSpan]$Elapsed,
        [switch]$Heartbeat
    )

    $suffix = if ($Heartbeat.IsPresent) { " | still running" } else { "" }
    return "$Percent% | $Phase | $(Format-Elapsed -Elapsed $Elapsed)$suffix"
}

function Get-FileTailText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [int]$MaximumBytes = 1048576
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return ""
    }

    $stream = $null
    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
        )
        $bytesToRead = [int][Math]::Min([long]$MaximumBytes, $stream.Length)
        if ($bytesToRead -eq 0) {
            return ""
        }

        $null = $stream.Seek(-$bytesToRead, [System.IO.SeekOrigin]::End)
        $buffer = New-Object byte[] $bytesToRead
        $bytesRead = $stream.Read($buffer, 0, $bytesToRead)
        return [System.Text.Encoding]::UTF8.GetString($buffer, 0, $bytesRead)
    }
    catch {
        return ""
    }
    finally {
        if ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Get-BuildProgress {
    param(
        [Parameter(Mandatory = $true)]
        [DateTime]$CommandStartedAtUtc,
        [Parameter(Mandatory = $true)]
        [int]$CurrentProgress,
        [AllowEmptyString()]
        [string]$BuildDirectory
    )

    $progress = [Math]::Max(10, $CurrentProgress)
    $phase = "Codex started"

    if ([string]::IsNullOrWhiteSpace($BuildDirectory)) {
        $outputRoot = Join-Path $RepositoryRoot "Builds\telegram-buffer"
        if (Test-Path -LiteralPath $outputRoot -PathType Container) {
            try {
                $newestDirectory = Get-ChildItem -LiteralPath $outputRoot -Directory -ErrorAction Stop |
                    Where-Object { $_.CreationTimeUtc -gt $CommandStartedAtUtc } |
                    Sort-Object CreationTimeUtc -Descending |
                    Select-Object -First 1
                if ($null -ne $newestDirectory) {
                    $BuildDirectory = $newestDirectory.FullName
                }
            }
            catch {
                # Progress detection is best effort and must never stop a build.
            }
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($BuildDirectory)) {
        $progress = [Math]::Max($progress, 20)
        $phase = "preparing sandbox and Unity"

        $unityLogPath = Join-Path $BuildDirectory "unity-android.log"
        $unityLogTail = Get-FileTailText -Path $unityLogPath

        if ($unityLogTail -match "BuildPlayer") {
            $progress = [Math]::Max($progress, 35)
            $phase = "Unity BuildPlayer"
        }
        if ($unityLogTail -match "\b(Bee|IL2CPP|clang)\b") {
            $progress = [Math]::Max($progress, 55)
            $phase = "Bee, IL2CPP and clang"
        }
        if ($unityLogTail -match '(DisplayProgressbar: Building Gradle project|Android PostProcess task "Building Gradle project"|org\.gradle\.launcher\.GradleMain.*assemble)') {
            $progress = [Math]::Max($progress, 75)
            $phase = "Gradle and postprocess"
        }

        $apkPath = Join-Path $BuildDirectory "LostCyberHamster.apk"
        if ($unityLogTail -match "(Build Successful|Build succeeded|Build Finished, Result: Success|Build completed with a result of 'Succeeded')" -or
            (Test-Path -LiteralPath $apkPath -PathType Leaf)) {
            $progress = [Math]::Max($progress, 90)
            $phase = "APK ready"
        }

        $summaryPath = Join-Path $BuildDirectory "build-summary.codex.json"
        if (Test-Path -LiteralPath $summaryPath -PathType Leaf) {
            $progress = [Math]::Max($progress, 95)
            $phase = "uploading to Telegram"
        }
    }

    return [pscustomobject]@{
        Progress       = [Math]::Min(95, $progress)
        Phase          = $phase
        BuildDirectory = $BuildDirectory
    }
}

function Convert-ToSecretFreeText {
    param(
        [AllowEmptyString()]
        [string]$Text
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return ""
    }

    $safeText = $Text -replace "[\r\n]+", " "
    foreach ($secret in @(
        [string]$script:TelegramConfig.botToken,
        [string]$script:TelegramConfig.chatId
    )) {
        if (-not [string]::IsNullOrWhiteSpace($secret)) {
            $safeText = $safeText.Replace($secret, "[REDACTED]")
        }
    }
    $safeText = [regex]::Replace(
        $safeText,
        "(?<![A-Za-z0-9_])\d{5,}:[A-Za-z0-9_-]{20,}",
        "[REDACTED]"
    )

    $safeText = $safeText.Trim()
    if ($safeText.Length -gt 1000) {
        $safeText = $safeText.Substring(0, 997) + "..."
    }

    return $safeText
}

function Read-DispatchFailure {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
        return $null
    }

    try {
        $rawText = Get-Content -LiteralPath $LogPath -Raw -Encoding UTF8
        if ([string]::IsNullOrWhiteSpace($rawText)) {
            return $null
        }

        try {
            $result = $rawText | ConvertFrom-Json
        }
        catch {
            $jsonLine = @($rawText -split "\r?\n" | Where-Object { $_.TrimStart().StartsWith("{") }) |
                Select-Object -Last 1
            if ([string]::IsNullOrWhiteSpace($jsonLine)) {
                return $null
            }
            $result = $jsonLine | ConvertFrom-Json
        }

        $errorCodeProperty = $result.PSObject.Properties["errorCode"]
        $errorMessageProperty = $result.PSObject.Properties["errorMessage"]
        return [pscustomobject]@{
            ErrorCode = Convert-ToSecretFreeText -Text (
                if ($null -eq $errorCodeProperty) { "" } else { [string]$errorCodeProperty.Value }
            )
            ErrorMessage = Convert-ToSecretFreeText -Text (
                if ($null -eq $errorMessageProperty) { "" } else { [string]$errorMessageProperty.Value }
            )
        }
    }
    catch {
        return $null
    }
}

function Format-BuildFailureStatus {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Percent,
        [Parameter(Mandatory = $true)]
        [string]$Phase,
        [Parameter(Mandatory = $true)]
        [TimeSpan]$Elapsed,
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    $prefix = "FAILED | $Percent% | $Phase | $(Format-Elapsed -Elapsed $Elapsed)"
    $failure = Read-DispatchFailure -LogPath $LogPath
    if ($null -eq $failure) {
        return "$prefix | Log: $LogPath"
    }

    $details = @($failure.ErrorCode, $failure.ErrorMessage) |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    if ($details.Count -eq 0) {
        return "$prefix | Log: $LogPath"
    }

    return "$prefix | $($details -join ': ')"
}

function Wait-BuildDispatch {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Dispatch
    )

    $process = $Dispatch.Process
    $currentBucket = 0
    $currentPhase = Get-BucketPhase -Bucket 0
    $buildDirectory = ""
    $startedAt = Get-Date
    $lastStatusAt = $startedAt
    $exitCode = 1

    try {
        $currentBucket = 10
        $currentPhase = Get-BucketPhase -Bucket $currentBucket
        Send-TelegramStatusSafe -Text (
            Format-ProgressStatus `
                -Percent $currentBucket `
                -Phase $currentPhase `
                -Elapsed ((Get-Date) - $startedAt)
        )
        $lastStatusAt = Get-Date

        while ($true) {
            $secondsUntilHeartbeat = [Math]::Max(
                1,
                120 - ((Get-Date) - $lastStatusAt).TotalSeconds
            )
            $waitSeconds = [Math]::Min(
                $ProgressIntervalSeconds,
                [Math]::Ceiling($secondsUntilHeartbeat)
            )
            if ($process.WaitForExit([int]($waitSeconds * 1000))) {
                break
            }

            $progress = Get-BuildProgress `
                -CommandStartedAtUtc $Dispatch.StartedAtUtc `
                -CurrentProgress $currentBucket `
                -BuildDirectory $buildDirectory
            $buildDirectory = $progress.BuildDirectory
            $targetBucket = [int]([Math]::Floor([double]$progress.Progress / 10) * 10)
            $targetBucket = [Math]::Min(90, $targetBucket)
            $elapsed = (Get-Date) - $startedAt

            while ($currentBucket + 10 -le $targetBucket) {
                $currentBucket += 10
                $currentPhase = Get-BucketPhase -Bucket $currentBucket
                Send-TelegramStatusSafe -Text (
                    Format-ProgressStatus `
                        -Percent $currentBucket `
                        -Phase $currentPhase `
                        -Elapsed $elapsed
                )
                $lastStatusAt = Get-Date
            }

            if (((Get-Date) - $lastStatusAt).TotalSeconds -ge 120) {
                Send-TelegramStatusSafe -Text (
                    Format-ProgressStatus `
                        -Percent $currentBucket `
                        -Phase $progress.Phase `
                        -Elapsed $elapsed `
                        -Heartbeat
                )
                $lastStatusAt = Get-Date
            }
        }

        $process.WaitForExit()
        $exitCode = $process.ExitCode
    }
    catch {
        Write-Warning "Dispatch progress monitoring failed; waiting for the existing build process."

        # The dispatcher has its own two-hour timeout. This fallback stays bounded
        # and prevents a second build from starting while the first child is alive.
        $fallbackDeadlineUtc = $Dispatch.StartedAtUtc.AddSeconds(7500)
        while ([DateTime]::UtcNow -lt $fallbackDeadlineUtc) {
            $hasExited = $false
            try {
                $hasExited = $process.HasExited
            }
            catch {
                $hasExited = $true
            }

            if ($hasExited) {
                break
            }

            if (((Get-Date) - $lastStatusAt).TotalSeconds -ge 120) {
                Send-TelegramStatusSafe -Text (
                    Format-ProgressStatus `
                        -Percent $currentBucket `
                        -Phase "progress monitor unavailable; build still running" `
                        -Elapsed ((Get-Date) - $startedAt) `
                        -Heartbeat
                )
                $lastStatusAt = Get-Date
            }
            Start-Sleep -Seconds 5
        }

        try {
            if (-not $process.HasExited) {
                # Kill the full Codex -> PowerShell -> Unity process tree. Do not
                # release the worker lock until tree termination is confirmed.
                do {
                    & taskkill.exe /PID $process.Id /T /F *> $null
                    $terminated = $process.WaitForExit(15000)
                    if (-not $terminated -and
                        ((Get-Date) - $lastStatusAt).TotalSeconds -ge 120) {
                        Send-TelegramStatusSafe -Text (
                            Format-ProgressStatus `
                                -Percent $currentBucket `
                                -Phase "terminating timed-out build process tree" `
                                -Elapsed ((Get-Date) - $startedAt) `
                                -Heartbeat
                        )
                        $lastStatusAt = Get-Date
                    }
                } while (-not $terminated)
            }
            else {
                $process.WaitForExit()
            }
            $exitCode = $process.ExitCode
        }
        catch {
            $exitCode = 1
        }
    }
    finally {
        $process.Dispose()
    }

    # Capture APK and summary files created between the last polling interval
    # and process exit so the final status reflects the completed build phase.
    try {
        $progress = Get-BuildProgress `
            -CommandStartedAtUtc $Dispatch.StartedAtUtc `
            -CurrentProgress $currentBucket `
            -BuildDirectory $buildDirectory
        $buildDirectory = $progress.BuildDirectory
        $targetBucket = [int]([Math]::Floor([double]$progress.Progress / 10) * 10)
        $targetBucket = [Math]::Min(90, $targetBucket)
        $progressElapsed = (Get-Date) - $startedAt

        while ($currentBucket + 10 -le $targetBucket) {
            $currentBucket += 10
            $currentPhase = Get-BucketPhase -Bucket $currentBucket
            Send-TelegramStatusSafe -Text (
                Format-ProgressStatus `
                    -Percent $currentBucket `
                    -Phase $currentPhase `
                    -Elapsed $progressElapsed
            )
        }
    }
    catch {
        Write-Warning "Final dispatch progress refresh failed."
    }

    $elapsed = (Get-Date) - $startedAt
    if ($exitCode -eq 0) {
        while ($currentBucket -lt 100) {
            $currentBucket += 10
            Send-TelegramStatusSafe -Text (
                Format-ProgressStatus `
                    -Percent $currentBucket `
                    -Phase (Get-BucketPhase -Bucket $currentBucket) `
                    -Elapsed $elapsed
            )
        }
    }
    else {
        Send-TelegramStatusSafe -Text (
            Format-BuildFailureStatus `
                -Percent $currentBucket `
                -Phase $currentPhase `
                -Elapsed $elapsed `
                -LogPath $Dispatch.StdoutLogPath
        )
    }

    return $exitCode
}

function Test-IsAllowedBuildCommand {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Update
    )

    $channelPostProperty = $Update.PSObject.Properties["channel_post"]
    if ($null -eq $channelPostProperty -or $null -eq $channelPostProperty.Value) {
        return $false
    }

    $post = $channelPostProperty.Value
    $textProperty = $post.PSObject.Properties["text"]
    $chatProperty = $post.PSObject.Properties["chat"]
    if ($null -eq $textProperty -or [string]$textProperty.Value -cne "build") {
        return $false
    }

    if ($null -eq $chatProperty -or $null -eq $chatProperty.Value) {
        return $false
    }

    $chatIdProperty = $chatProperty.Value.PSObject.Properties["id"]
    if ($null -eq $chatIdProperty) {
        return $false
    }

    return ([string]$chatIdProperty.Value -ceq [string]$script:TelegramConfig.chatId)
}

function Initialize-UpdateOffset {
    $updates = @(Get-TelegramUpdates -Offset -1 -Timeout 0 -Limit 1)
    $nextOffset = [long]0

    if ($updates.Count -gt 0) {
        $latestUpdate = $updates | Sort-Object { [long]$_.update_id } | Select-Object -Last 1
        $nextOffset = [long]$latestUpdate.update_id + 1
    }

    Write-WorkerState -Path $StatePath -NextOffset $nextOffset
    Write-Host "Telegram build worker initialized."
    return $nextOffset
}

function Invoke-PollCycle {
    param(
        [Parameter(Mandatory = $true)]
        [long]$Offset
    )

    $nextOffset = $Offset
    $updates = @(Get-TelegramUpdates -Offset $Offset -Timeout $PollTimeoutSeconds -Limit 100)

    foreach ($update in ($updates | Sort-Object { [long]$_.update_id })) {
        $nextOffset = [long]$update.update_id + 1

        # Persist before dispatch: restart must never rebuild an already accepted update.
        Write-WorkerState -Path $StatePath -NextOffset $nextOffset

        if (-not (Test-IsAllowedBuildCommand -Update $update)) {
            continue
        }

        Send-TelegramStatusSafe -Text (
            Format-ProgressStatus `
                -Percent 0 `
                -Phase (Get-BucketPhase -Bucket 0) `
                -Elapsed ([TimeSpan]::Zero)
        )

        $dispatchStartedAt = Get-Date
        $dispatch = $null
        try {
            $dispatch = Start-BuildDispatch
        }
        catch {
            Send-TelegramStatusSafe -Text (
                Format-BuildFailureStatus `
                    -Percent 0 `
                    -Phase "pipeline did not start" `
                    -Elapsed ((Get-Date) - $dispatchStartedAt) `
                    -LogPath (Join-Path $StateDirectory "last-dispatch.stderr.log")
            )
            continue
        }

        [void](Wait-BuildDispatch -Dispatch $dispatch)

        # Wait-BuildDispatch already sent final success or failure status.
    }

    return $nextOffset
}

$workerMutex = $null
$mutexAcquired = $false
$exitCode = 0

try {
    if ([string]::IsNullOrWhiteSpace($StatePath)) {
        $StatePath = Join-Path $StateDirectory "state.json"
    }

    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = Join-Path $PSScriptRoot "..\.."
    }

    if ([string]::IsNullOrWhiteSpace($DispatchPath)) {
        $DispatchPath = Join-Path $PSScriptRoot "invoke_codex_build.ps1"
    }

    $ConfigPath = Get-NormalizedPath -Path $ConfigPath
    $BotApiConfigPath = Get-NormalizedPath -Path $BotApiConfigPath
    $StateDirectory = Get-NormalizedPath -Path $StateDirectory
    $StatePath = Get-NormalizedPath -Path $StatePath
    $DispatchPath = Get-NormalizedPath -Path $DispatchPath
    $RepositoryRoot = Get-NormalizedPath -Path $RepositoryRoot

    if (-not (Test-Path -LiteralPath $DispatchPath -PathType Leaf)) {
        throw [System.IO.FileNotFoundException]::new("Build dispatch script is missing.")
    }

    if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
        throw [System.IO.DirectoryNotFoundException]::new("Repository root is missing.")
    }

    if (-not (Test-Path -LiteralPath $BotApiConfigPath -PathType Leaf)) {
        throw [System.IO.FileNotFoundException]::new("Local Bot API config file is missing.")
    }

    if (Test-PathInside -Child $StateDirectory -Parent $RepositoryRoot) {
        throw [System.InvalidOperationException]::new("StateDirectory must be outside the repository.")
    }

    $null = New-Item -ItemType Directory -Path $StateDirectory -Force

    $mutexHash = Get-Sha256Text -Text $StatePath.ToLowerInvariant()
    $mutexName = "Local\LostCyberHamster.TelegramBuildWorker.$($mutexHash.Substring(0, 24))"
    $workerMutex = [System.Threading.Mutex]::new($false, $mutexName)

    try {
        $mutexAcquired = $workerMutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        $mutexAcquired = $true
    }

    if (-not $mutexAcquired) {
        [Console]::Error.WriteLine("Telegram build worker is already running.")
        $exitCode = 2
    }
    else {
        $script:TelegramConfig = Read-TelegramConfig -Path $ConfigPath
        $state = Read-WorkerState -Path $StatePath

        if ($null -eq $state -or -not [bool]$state.initialized) {
            $nextOffset = Initialize-UpdateOffset
            if ($Once) {
                $exitCode = 0
            }
        }
        else {
            $nextOffset = [long]$state.nextOffset
        }

        if (-not ($Once -and ($null -eq $state -or -not [bool]$state.initialized))) {
            do {
                try {
                    $nextOffset = Invoke-PollCycle -Offset $nextOffset
                }
                catch {
                    if ($Once) {
                        throw [System.InvalidOperationException]::new("Telegram poll failed.")
                    }

                    Write-Warning "Telegram poll failed. Retrying."
                    Start-Sleep -Seconds 5
                }
            } while (-not $Once)
        }
    }
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    $exitCode = 1
}
finally {
    if ($mutexAcquired -and $null -ne $workerMutex) {
        $workerMutex.ReleaseMutex()
    }

    if ($null -ne $workerMutex) {
        $workerMutex.Dispose()
    }
}

exit $exitCode
