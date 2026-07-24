# Telegram Build Worker

Локальный worker слушает `LostCyberHamster builds`. Точное сообщение `build` собирает Android development APK из текущего локального `integration/unity-live`, включая dirty-файлы.

Git не меняется: без commit, merge и push. APK и Telegram summary содержат commit, dirty-state и diff hash.

Перед batch-build содержимое `Assets`, `Packages` и `ProjectSettings` зеркалируется в отдельный sandbox. Если исходники меняются прямо во время копирования, sync повторяется до получения стабильного snapshot. Правки после snapshot не отменяют текущий build и попадут в следующий.

## Архитектура

```text
Telegram channel_post: build
  -> telegram_build_worker.ps1
  -> invoke_codex_build.ps1
  -> standalone Codex CLI, headless
  -> run_build_and_publish.ps1
  -> tools/build/build_android_telegram.ps1
  -> local Telegram Bot API
```

- `telegram_build_worker.ps1` — polling, проверка `chatId`, offset, lock, статусы.
- `invoke_codex_build.ps1` — проверка ветки/source snapshot, запуск одного headless Codex, строгая проверка JSON-результата.
- `run_build_and_publish.ps1` — единственный deterministic build/publish entrypoint, разрешенный Codex.
- `install_worker_task.ps1` — скрытый Scheduled Task текущего пользователя, `AtLogOn`, restart policy.
- `uninstall_worker_task.ps1` — удаление только точного task.
- `worker-config.example.json` — справка по локальным путям и настройкам.

Codex не выбирает команды и не редактирует проект. Fixed prompt разрешает один вызов `run_build_and_publish.ps1`.

## Требования

- standalone `codex.cmd` доступен текущему пользователю;
- `codex.cmd login status` успешен;
- Unity и Android tooling настроены;
- Docker и локальный Telegram Bot API запущены;
- существуют:
  - `%USERPROFILE%\.codex\telegram-buffer.local.json`;
  - `%USERPROFILE%\.codex\telegram-bot-api.local.json`.

Codex Desktop App alias не подходит для надежного headless-запуска. Установка CLI:

```powershell
npm install --global @openai/codex
codex.cmd login status
```

Tokens/config contents не читаются установщиком, не попадают в Git и логи.

## Безопасная проверка

Проверка dispatch без Codex, Unity, сборки и публикации:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\telegram-build-worker\invoke_codex_build.ps1 `
  -RepositoryRoot . `
  -DryRun
```

Команда проверяет branch, source snapshot, CLI resolver, paths и печатает compact JSON-план.

Проверка установки без изменений:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\telegram-build-worker\install_worker_task.ps1 -WhatIf
```

## Установка

Из корня `integration/unity-live`:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\telegram-build-worker\install_worker_task.ps1
```

Установить без немедленного старта:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\telegram-build-worker\install_worker_task.ps1 -NoStart
```

Task запускается скрыто при входе текущего пользователя. Падение вызывает restart. Второй экземпляр и параллельный билд блокируются.

## Запуск и статус

```powershell
$task = 'LostCyberHamster Telegram Build Worker'
Start-ScheduledTask -TaskName $task
Get-ScheduledTask -TaskName $task
Get-ScheduledTaskInfo -TaskName $task
Stop-ScheduledTask -TaskName $task
```

После первого старта отправь точное, регистрозависимое сообщение `build`.

При первом запуске без `state.json` worker инициализирует offset и не выполняет старые updates. После инициализации offset сохраняется. Если task был остановлен, команда `build`, отправленная во время остановки, будет взята после следующего старта.

Принятая команда записывается в state до запуска сборки и автоматически не повторяется. После ошибки отправь новое `build`: это защищает от неявной повторной сборки и двойной загрузки APK.

## Статусы прогресса

Worker отправляет отдельный статус примерно каждые 10%: прием команды, preflight, snapshot/sandbox, Unity preparation, Addressables/player build, проверка APK, подготовка публикации, upload, завершение.

Проценты phase-based и приблизительные. Unity API не дает точного общего процента для этого pipeline. Долгая фаза не увеличивает процент искусственно: каждые 120 секунд приходит heartbeat с текущей фазой.

При ошибке Telegram получает короткий status и путь к логу. Worker продолжает polling после временной ошибки Telegram, Docker или Codex.

## State и логи

Все runtime-файлы вне Git:

```text
%LOCALAPPDATA%\LostCyberHamster\TelegramBuildWorker\
```

Основное:

- `state.json` — Telegram offset;
- `last-dispatch.log` — последний worker dispatch;
- `runs\<runId>\codex.stdout.jsonl` — события headless Codex;
- `runs\<runId>\codex.stderr.log` — stderr;
- `runs\<runId>\codex.final.json` — deterministic workflow result;
- `runs\<runId>\invoke-result.json` — итог dispatch.

Telegram token в эти файлы не пишется.

## Удаление

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\telegram-build-worker\uninstall_worker_task.ps1
```

Удаляется только `LostCyberHamster Telegram Build Worker`. State, логи и Telegram configs остаются.

## Если не работает

- `Standalone Codex CLI is missing`: установи CLI, проверь `codex.cmd login status`.
- Docker/Bot API недоступен: запусти Docker Desktop и локальный `telegram-bot-api`, затем перезапусти task.
- Task завершился: смотри `Get-ScheduledTaskInfo`, `last-dispatch.log`, свежий каталог `runs\<runId>`.
- Команда до самого первого старта пропала: это safe offset initialization. Отправь новое `build`.
- Cloud Bot API не принимает большой APK: используй локальный Bot API config.

## Доступ

`channel_post` надежно идентифицирует канал, но обычно не конкретного автора. Любой с правом публикации в разрешенном канале сможет вызвать `build`. Ограничь права публикации и `chatId`.
