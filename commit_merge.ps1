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

    # Смотрим что изменилось
    $status = git status --short
    if ($status) {
        Write-Host "Изменения:"
        git diff --stat HEAD

        git add -A

        # Генерируем сообщение коммита через GitHub Models (gpt-4.1) на основе staged diff
        $diff = git diff --cached
        $maxLen = 12000
        if ($diff.Length -gt $maxLen) { $diff = $diff.Substring(0, $maxLen) + "`n...[truncated]" }

        $sysPrompt = "Ты пишешь короткие осмысленные сообщения git commit на русском языке. Формат: одна строка, до 72 символов, без префиксов типа feat:/fix:, без точки в конце, повелительное наклонение. Опиши суть изменений по diff."
        $userPrompt = "Diff:`n$diff"

        $commitMsg = $userPrompt | gh models run --system-prompt $sysPrompt openai/gpt-4.1 2>$null
        $commitMsg = ($commitMsg -join " ").Trim().Trim('"').Trim()
        if ([string]::IsNullOrWhiteSpace($commitMsg)) {
            Write-Warning "Не удалось сгенерировать сообщение через gh models, использую fallback."
            $files = git diff --cached --name-only
            $commitMsg = "Update: " + ($files -join ", ")
            if ($commitMsg.Length -gt 72) { $commitMsg = $commitMsg.Substring(0, 69) + "..." }
        }

        Write-Host "Commit message: $commitMsg"
        git commit -m $commitMsg
        Write-Host "Commit: $(git log -1 --oneline)"
    } else {
        Write-Host "Нечего коммитить, переходим к merge."
    }

    # Merge в main
    git checkout main
    git merge integration/unity-live --no-ff -m "Merge integration/unity-live into main"
    git push origin main
    git push origin integration/unity-live
    git checkout integration/unity-live

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
