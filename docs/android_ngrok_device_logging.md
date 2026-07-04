# Android Ngrok Device Logging

Документ описывает dev-инфраструктуру, через которую Android APK LostCyberHamster, уже установленный на телефоне, сам отправляет snapshots `diagnostic_log.txt` на рабочий ноутбук через публичный ngrok endpoint.

## Суть

Это push-based логирование: агент не подключается к телефону и не запрашивает у него логи. Runtime игры сам отправляет snapshots на HTTP collector при важных событиях. Collector принимает `POST /upload` и сохраняет каждый upload в общую Dropbox Exchange папку.

Текущая схема:

```text
Android APK
  -> ngrok HTTPS endpoint
  -> Docker ngrok container на основном ноутбуке
  -> Docker collector container на основном ноутбуке
  -> C:\Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android
  -> Dropbox sync
  -> второй ноутбук читает ту же папку локально
```

Текущий dev endpoint:

```text
https://ladle-substance-spray.ngrok-free.dev/upload
```

Текущий build label:

```text
android-dev-ngrok-logs
```

Важно: collector не читает ngrok и не опрашивает телефон. Телефон делает обычный `POST /upload`, ngrok tunnel переносит request на основной ноутбук, collector принимает request и пишет файлы.

## Роли ноутбуков

Основной ноутбук является receiver-узлом:

- держит Docker Compose stack `collector + ngrok`;
- принимает requests от Android устройств через `ladle-substance-spray.ngrok-free.dev`;
- пишет uploads в Dropbox Exchange output root;
- должен быть включен, иметь интернет, запущенный Docker Desktop и Dropbox.

Второй ноутбук является reader-узлом:

- не поднимает ngrok;
- не поднимает collector;
- не подключается к телефону;
- читает уже синхронизированные файлы из Dropbox Exchange;
- использует тот же относительный путь `exchange\crystal_wave\LostCyberHamster_DeviceLogs\android` внутри своего локального Dropbox.

Эта схема дает один источник правды по логам: общую Dropbox-папку. Мы не используем ngrok pooling для двух collectors, потому что pooling балансирует uploads между ноутбуками, а не дублирует каждый upload на оба.

## Output root

Канонический host-путь на основном ноутбуке:

```text
C:\Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android
```

`ensure_device_log_docker_stack.ps1` сам находит или создает эту директорию, если доступна Dropbox Exchange папка. На текущем основном ноутбуке с Dropbox web совпадает именно `C:\Dropbox\exchange`. Затем скрипт записывает host-путь в локальный файл:

```text
tools/device-log-collector/.env.local
```

Переменная для Docker Compose:

```text
DEVICE_LOG_OUTPUT_ROOT_HOST=C:/Dropbox/exchange/crystal_wave/LostCyberHamster_DeviceLogs/android
```

`.env.local` содержит секрет ngrok и локальные пути, поэтому не коммитится.

Если Dropbox Exchange не найден, receiver stack не считается готовым. Для разового нестандартного стенда можно явно передать путь:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\ensure_device_log_docker_stack.ps1 -DeviceLogOutputRootHost "D:\Some\Explicit\DeviceLogs\android" -Json
```

Для общей рабочей инфраструктуры использовать только Dropbox Exchange output root.

## Готовность receiver-стека

Основной способ поднять инфраструктуру на receiver-ноутбуке:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\ensure_device_log_docker_stack.ps1 -Json
```

Этот скрипт является entrypoint для агентов. Он:

- проверяет наличие Docker CLI и готовность Docker daemon;
- при необходимости запускает Docker Desktop и ждет готовности daemon;
- находит/создает Dropbox Exchange output root;
- создает локальный `tools/device-log-collector/.env.local` с `NGROK_AUTHTOKEN`, `NGROK_DOMAIN` и `DEVICE_LOG_OUTPUT_ROOT_HOST`;
- берет ngrok token из `$env:NGROK_AUTHTOKEN`, существующего `.env.local` или `%LOCALAPPDATA%\ngrok\ngrok.yml`;
- останавливает старый non-Docker supervisor/collector/ngrok, если они занимают тот же порт;
- выполняет `docker compose up -d --quiet-pull --remove-orphans`; на первом запуске или с `-Rebuild` дополнительно собирает локальные images;
- ждет `http://127.0.0.1:8765/health`;
- ждет `https://ladle-substance-spray.ngrok-free.dev/health` с header `ngrok-skip-browser-warning: true`;
- возвращает JSON со статусом health, compose-контейнеров и host output root.

После изменений `server.js`, `Dockerfile`, `Dockerfile.ngrok`, `docker-compose.yml` или config запускать с явным rebuild:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\ensure_device_log_docker_stack.ps1 -Rebuild -Json
```

Проверить текущее состояние без попытки поднять стек:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\check_device_log_stack.ps1 -Json
```

Ожидаемый результат:

- `ready: true`;
- `dockerReady: true`;
- `localHealth.ok: true`;
- `publicHealth.ok: true`;
- `outputRootHostExists: true`;
- `outputRootHost` указывает на Dropbox Exchange папку.

Если `publicHealth.ok: true`, но `ready: false` или `dockerReady: false`, это не считается рабочим receiver-состоянием. Частая причина: тот же ngrok domain уже держит другой ноутбук, и публичный `/health` отвечает от него. В этом случае смотреть logs `lostcyberhamster-device-log-ngrok`; для `ERR_NGROK_334 endpoint already online` нужно остановить лишний ngrok/collector на другом ноутбуке или явно выбрать, какой ноутбук сейчас receiver.

## Docker Compose

Файлы стека:

```text
tools/device-log-collector/Dockerfile
tools/device-log-collector/Dockerfile.ngrok
tools/device-log-collector/docker-compose.yml
tools/device-log-collector/ensure_device_log_docker_stack.ps1
tools/device-log-collector/check_device_log_stack.ps1
tools/device-log-collector/ngrok-watchdog.sh
```

Compose project:

```text
lostcyberhamster-device-logs
```

Контейнеры:

- `lostcyberhamster-device-log-collector` - Node.js HTTP collector, слушает `0.0.0.0:8765` внутри контейнера, опубликован на ноутбуке как `127.0.0.1:8765`, пишет в bind mount `/workspace/DeviceLogs/android`;
- `lostcyberhamster-device-log-ngrok` - локальный wrapper image на базе официального `ngrok/ngrok:3-alpine`, поднимает tunnel на `http://collector:8765` с закрепленным `--url=https://ladle-substance-spray.ngrok-free.dev`.

Оба сервиса имеют `restart: unless-stopped`. У collector есть Docker healthcheck, а ngrok стартует только после healthy collector.

Ngrok контейнер дополнительно запускается через `ngrok-watchdog.sh`: wrapper проверяет публичный `/health` с header `ngrok-skip-browser-warning: true`. Если несколько проверок подряд не проходят, wrapper завершает контейнер с ошибкой, и Docker restart policy поднимает ngrok заново.

Ручная остановка контейнера через `docker stop` или `docker kill` считается намеренной остановкой. Такой контейнер поднимается следующим запуском `ensure_device_log_docker_stack.ps1`.

Ручная диагностика контейнеров:

```powershell
docker compose --env-file .\tools\device-log-collector\.env.local -f .\tools\device-log-collector\docker-compose.yml ps
docker compose --env-file .\tools\device-log-collector\.env.local -f .\tools\device-log-collector\docker-compose.yml logs --tail 100
```

Остановить стек вручную:

```powershell
docker compose --env-file .\tools\device-log-collector\.env.local -f .\tools\device-log-collector\docker-compose.yml down
```

## Автозапуск receiver-стека

Installer регистрирует one-shot ensure при входе пользователя в Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\install_device_log_stack_task.ps1 -StartNow
```

Имя задачи:

```text
LostCyberHamsterDeviceLogStack
```

Сначала installer пробует Windows Scheduled Task. Если прав не хватает, создает Startup shortcut:

```text
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\LostCyberHamsterDeviceLogStack.lnk
%LOCALAPPDATA%\LostCyberHamster\device-log-stack\start_device_log_stack.cmd
```

В Docker-режиме startup launcher не держит бесконечный restart-loop. Он один раз вызывает `ensure_device_log_docker_stack.ps1`, а дальнейшую живучесть обеспечивают Docker restart policies.

## Reader-ноутбук

На втором ноутбуке агенту нужен только локально синхронизированный Dropbox.

Проверка:

```powershell
Test-Path -LiteralPath "C:\Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android"
```

Reader-агент читает последние uploads:

```powershell
$root = "C:\Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android"
if (-not (Test-Path -LiteralPath $root)) {
    $root = Join-Path $env:USERPROFILE "Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android"
}

Get-ChildItem -LiteralPath $root -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 20 |
    ForEach-Object {
        $metadataPath = Join-Path $_.FullName "metadata.json"
        if (Test-Path -LiteralPath $metadataPath) {
            $metadata = Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8 | ConvertFrom-Json
            [pscustomobject]@{
                Time = $_.LastWriteTime
                Reason = $metadata.reason
                Scene = $metadata.activeScene
                Device = $metadata.deviceModel
                Endpoint = $metadata.endpointUrl
                Path = $_.FullName
            }
        }
    } | Format-Table -AutoSize
```

Если папка не синхронизируется, проблема находится в Dropbox, а не в ngrok или collector на reader-ноутбуке.

## Bootstrap prompt для reader-ноутбука

Готовый prompt для агента на втором ноутбуке:

```text
Ты работаешь в репозитории LostCyberHamster на reader-ноутбуке. Этот ноутбук не должен поднимать ngrok и device-log collector. Логи Android APK принимает основной receiver-ноутбук через ngrok, а затем пишет их в Dropbox Exchange.

Твоя задача - убедиться, что локальный Dropbox синхронизирует общую папку логов, и читать логи оттуда.

1. Прочитай:
   - docs/rules/AGENTS.md
   - docs/android_ngrok_device_logging.md
   - при необходимости docs/rules/agent_tools.md

2. Проверь, что Dropbox установлен и запущен.

3. Найди общую папку:
   - `exchange/crystal_wave/LostCyberHamster_DeviceLogs/android` внутри локальной Dropbox-папки, которая реально совпадает с `dropbox.com/home/exchange`
   - на текущем основном ноутбуке: `C:\Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android`

4. Не запускай:
   - tools/device-log-collector/ensure_device_log_docker_stack.ps1
   - tools/device-log-collector/check_device_log_stack.ps1 как критерий готовности reader-ноутбука
   - ngrok
   - локальный collector

5. Для анализа бери свежие upload-директории из Dropbox-папки. Внутри каждой upload-директории:
   - metadata.json
   - diagnostic_log.txt
   - package.json

6. Если новых логов нет:
   - проверь, запущен ли Dropbox и завершена ли синхронизация;
   - проверь, что receiver-ноутбук включен и его stack готов;
   - не пытайся самостоятельно занимать ngrok domain на reader-ноутбуке.

Финальный отчет пользователю должен содержать путь к найденной Dropbox-папке и последние найденные uploads.
```

## Клиент в игре

Unity config:

```text
LostCyberHamster/Assets/Resources/Diagnostics/device_log_settings.json
```

Игра читает JSON через `Resources.Load("Diagnostics/device_log_settings")`. Android upload включается, когда:

- `enabled: true`;
- `allowOnAndroid: true`;
- `endpointUrl` не пустой;
- текущая платформа разрешена в `DeviceLogUploadSettings.IsPlatformAllowed()`.

Основные runtime-классы:

- `DeviceLogReporter` - создается до загрузки сцен и подписывается на lifecycle/log events;
- `DeviceLogUploadRunner` - держит очередь upload reasons;
- `DeviceLogUploader` - читает tail `diagnostic_log.txt`, собирает metadata и делает `POST /upload`;
- `DeviceLogStartupProbe` - проверяет `GET /health` и `POST /probe` при старте.

Для free ngrok клиент добавляет header:

```text
ngrok-skip-browser-warning: true
```

`POST /upload` также требует dev-token:

```text
X-LCH-Device-Log-Token: lost-cyber-hamster-device-logs
```

Это dev-gate, не production-grade security. Перед production-сборками device log upload должен быть выключен.

## Где лежат файлы upload

Внутри контейнера output root остается:

```text
/workspace/DeviceLogs/android
```

На host это bind mount в Dropbox Exchange:

```text
C:\Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android
```

Каждый upload сохраняется отдельной папкой:

```text
<outputRoot>/<createdAt>_<deviceModel>_<reason>_<sessionId>/
```

Внутри:

- `metadata.json` - device/build/session/reason/scene/endpoint/current level;
- `diagnostic_log.txt` - snapshot/tail diagnostic log из билда;
- `package.json` - summary сохраненного пакета;
- `_requests.log` в output root - access log collector;
- `_probes.log` в output root - startup probe записи.

## Smoke upload

Проверить публичный health:

```powershell
$headers = @{ "ngrok-skip-browser-warning" = "true" }
Invoke-RestMethod -Method Get -Uri "https://ladle-substance-spray.ngrok-free.dev/health" -Headers $headers
```

Сделать smoke upload через публичный endpoint:

```powershell
$headers = @{
  "X-LCH-Device-Log-Token" = "lost-cyber-hamster-device-logs"
  "ngrok-skip-browser-warning" = "true"
}
$createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
$body = @{
  metadata = @{
    sessionId = "receiver-smoke"
    reason = "receiver_docker_smoke"
    createdAtUtc = $createdAtUtc
    buildLabel = "android-dev-ngrok-logs"
    endpointUrl = "https://ladle-substance-spray.ngrok-free.dev/upload"
    deviceModel = "agent-receiver-smoke"
  }
  diagnosticLogFileName = "diagnostic_log.txt"
  diagnosticLogEncoding = "utf-8"
  diagnosticLogBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("receiver docker smoke $createdAtUtc"))
  diagnosticLogTruncated = $false
} | ConvertTo-Json -Depth 6
Invoke-RestMethod -Method Post -Uri "https://ladle-substance-spray.ngrok-free.dev/upload" -Headers $headers -Body $body -ContentType "application/json"
```

Проверить свежие uploads в Dropbox:

```powershell
$root = "C:\Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android"
Get-ChildItem -LiteralPath $root -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 5
```

## Что уже проверено и отклонено

Google Drive DriveFS не используется как Docker bind mount. Проверка показала, что контейнер может писать в смонтированный путь внутри Docker, но изменения не появляются в host-папке Google Drive, и host-файлы не видны внутри контейнера. Для надежной записи из Docker выбран Dropbox Exchange.

На текущем основном ноутбуке не использовать `%USERPROFILE%\Dropbox\exchange\crystal_wave` как output root для device logs: эта локальная папка не совпала с тем, что видно в Dropbox web. Канонический путь здесь - `C:\Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android`.

Ngrok pooling не используется для рабочих ноутбуков. Он позволяет нескольким ngrok agents держать один domain, но распределяет requests между ними. Это не решает задачу "одни и те же логи доступны всем агентам".

## Что важно помнить

- Уже установленный APK не получает новый endpoint автоматически. После изменения `device_log_settings.json` нужен новый билд.
- Это dev-инфраструктура для тестовых Android APK, не production telemetry.
- `diagnostic_log.txt` отправляется snapshots по событиям; отсутствие нового upload не означает, что телефон "не отвечает".
- Для приема логов receiver-ноутбук должен быть включен, иметь интернет и запущенные Docker stack + Dropbox.
- Reader-ноутбук читает Dropbox и не должен занимать ngrok domain.
