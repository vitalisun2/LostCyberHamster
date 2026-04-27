Set-StrictMode -Off
$ErrorActionPreference = "Stop"

Push-Location $PSScriptRoot

try {
    # Переключаемся на integration/unity-live если нужно
    $branch = git branch --show-current
    if ($branch -ne "integration/unity-live") {
        Write-Host "Текущая ветка: '$branch'. Переключаюсь на integration/unity-live..."
        git checkout integration/unity-live
    }

    # Коммитим только staged-изменения (git add -A не делаем)
    $staged = git diff --cached --name-only
    if ($staged) {
        Write-Host "Staged изменения:"
        git diff --cached --stat

        # Генерируем сообщение коммита через GitHub Models (gpt-4.1-mini)
        # Читаем diff через temp-файл чтобы сохранить UTF-8 (pipe ломает кодировку кириллицы)
        $tmpFile = [System.IO.Path]::GetTempFileName()
        git diff --cached | Out-File -FilePath $tmpFile -Encoding utf8
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

        Write-Host "Commit message: $commitMsg"
        git commit -m $commitMsg
        Write-Host "Commit: $(git log -1 --oneline)"
    } else {
        Write-Host "Нет staged-изменений. Используй 'git add' чтобы выбрать файлы для коммита."
        Write-Host "Продолжаю с merge..."
    }

    # Merge в main
    git checkout main
    git merge integration/unity-live --no-ff -m "Merge integration/unity-live into main"
    git push origin main
    if ($LASTEXITCODE -ne 0) { Write-Warning "push origin main завершился с ошибкой. Продолжаю..." }
    # Синхронизируем integration/unity-live с main
    git checkout integration/unity-live
    git merge main --ff-only
    git push origin integration/unity-live
    if ($LASTEXITCODE -ne 0) { Write-Warning "push origin integration/unity-live завершился с ошибкой." }

    # Проверка синхронизации
    $mainHash    = git rev-parse main
    $liveHash    = git rev-parse integration/unity-live
    if ($mainHash -eq $liveHash) {
        Write-Host "$(git log -1 --oneline) — main и unity-live синхронизированы ✓"
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
