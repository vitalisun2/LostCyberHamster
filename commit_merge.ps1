Set-StrictMode -Off
$ErrorActionPreference = "Stop"

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        $details = ($output | Out-String).Trim()
        if ([string]::IsNullOrWhiteSpace($details)) {
            $details = "git exited with code $LASTEXITCODE"
        }

        throw "git $($Arguments -join ' ') failed.`n$details"
    }

    return @($output)
}

function Get-GitText {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return ((Invoke-Git -Arguments $Arguments) -join "`n").Trim()
}

Push-Location $PSScriptRoot

try {
    # Переключаемся на integration/unity-live если нужно
    $branch = Get-GitText -Arguments @('branch', '--show-current')
    if ($branch -ne "integration/unity-live") {
        Write-Host "Текущая ветка: '$branch'. Переключаюсь на integration/unity-live..."
        Invoke-Git -Arguments @('checkout', 'integration/unity-live') | Out-Null
    }

    # Коммитим только staged-изменения (git add -A не делаем)
    $staged = @(Invoke-Git -Arguments @('diff', '--cached', '--name-only'))
    if ($staged) {
        Write-Host "Staged изменения:"
        Invoke-Git -Arguments @('diff', '--cached', '--stat') | ForEach-Object { Write-Host $_ }

        # Генерируем сообщение коммита через GitHub Models (gpt-4.1-mini)
        # Читаем diff через temp-файл чтобы сохранить UTF-8 (pipe ломает кодировку кириллицы)
        $tmpFile = [System.IO.Path]::GetTempFileName()
        Invoke-Git -Arguments @('diff', '--cached') | Out-File -FilePath $tmpFile -Encoding utf8
        $diff = Get-Content $tmpFile -Encoding utf8 -Raw
        Remove-Item $tmpFile -Force
        $maxLen = 12000
        if ($diff.Length -gt $maxLen) { $diff = $diff.Substring(0, $maxLen) + "`n...[truncated]" }

        $prompt = "Write a short git commit message in English, one line up to 72 characters, imperative mood, no period at the end. Be specific about what changed — not just which file, but what was added, removed or modified. Output only the message, nothing else.`nDiff:`n$diff"
        $commitMsg = ($prompt | gh models run openai/gpt-4.1-mini 2>$null | Where-Object { $_ -match '\S' -and $_ -notmatch '^та' } | Select-Object -Last 1)
        $commitMsg = "$commitMsg".Trim().Trim('"').Trim()
        if ([string]::IsNullOrWhiteSpace($commitMsg)) {
            Write-Warning "Не удалось сгенерировать сообщение через gh models, использую fallback."
            $commitMsg = "Update: " + ($staged -join ", ")
            if ($commitMsg.Length -gt 72) { $commitMsg = $commitMsg.Substring(0, 69) + "..." }
        }

        Write-Host "Commit message: $commitMsg" -ForegroundColor Cyan
        Invoke-Git -Arguments @('commit', '-m', $commitMsg) | ForEach-Object { Write-Host $_ }
        Write-Host "Commit: $(Get-GitText -Arguments @('log', '-1', '--oneline'))"
    } else {
        Write-Host "Нет staged-изменений. Используй 'git add' чтобы выбрать файлы для коммита."
        Write-Host "Продолжаю с merge..."
    }

    # Обновляем main через fast-forward без checkout, чтобы не трогать unstaged в unity-live.
    & git merge-base --is-ancestor main integration/unity-live
    if ($LASTEXITCODE -eq 1) {
        $mainHash = Get-GitText -Arguments @('rev-parse', 'main')
        $liveHash = Get-GitText -Arguments @('rev-parse', 'integration/unity-live')
        throw "Ветки действительно разошлись: main=$mainHash, unity-live=$liveHash. Нужен ручной merge, fast-forward невозможен."
    }

    if ($LASTEXITCODE -ne 0) {
        throw "git merge-base --is-ancestor main integration/unity-live failed with exit code $LASTEXITCODE"
    }

    $mainHash = Get-GitText -Arguments @('rev-parse', 'main')
    $liveHash = Get-GitText -Arguments @('rev-parse', 'integration/unity-live')
    if ($mainHash -ne $liveHash) {
        Write-Host "Fast-forward main -> integration/unity-live ($liveHash)..."
        Invoke-Git -Arguments @('update-ref', 'refs/heads/main', $liveHash, $mainHash) | Out-Null
    } else {
        Write-Host "main уже синхронизирован с integration/unity-live."
    }

    Invoke-Git -Arguments @('push', 'origin', 'main') | ForEach-Object { Write-Host $_ }
    Invoke-Git -Arguments @('push', 'origin', 'integration/unity-live') | ForEach-Object { Write-Host $_ }

    # Проверка синхронизации
    $mainHash    = Get-GitText -Arguments @('rev-parse', 'main')
    $liveHash    = Get-GitText -Arguments @('rev-parse', 'integration/unity-live')
    if ($mainHash -eq $liveHash) {
        Write-Host "$(Get-GitText -Arguments @('log', '-1', '--oneline', 'integration/unity-live')) — main и unity-live синхронизированы ✓"
    } else {
        Write-Error "Ветки расходятся! main=$mainHash, unity-live=$liveHash"
        exit 1
    }
} catch {
    Write-Error $_
    exit 1
} finally {
    Pop-Location
}
