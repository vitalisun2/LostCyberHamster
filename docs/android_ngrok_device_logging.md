# Android Ngrok Device Logging

Документ описывает dev-инфраструктуру, через которую Android APK LostCyberHamster, уже установленный на телефоне, сам отправляет snapshots `diagnostic_log.txt` на рабочий ноутбук через публичный ngrok endpoint.

## Суть

Это push-based логирование: агент не подключается к телефону и не запрашивает у него логи. Runtime игры сам отправляет snapshots на HTTP collector при важных событиях. Collector сохраняет каждый upload локально в `DeviceLogs/android`, а агент читает уже эти файлы из workspace.

Схема:

```text
Android APK -> ngrok HTTPS endpoint -> Docker ngrok container -> Docker collector container -> DeviceLogs/android
```

Текущий dev endpoint:

```text
https://ladle-substance-spray.ngrok-free.dev/upload
```

Текущий build label:

```text
android-dev-ngrok-logs
```

Важно: collector не читает ngrok и не опрашивает телефон. Телефон делает обычный `POST /upload`, ngrok tunnel переносит request на ноутбук, collector принимает request и пишет файлы.

## Готовность стека

Основной способ поднять инфраструктуру - Docker Compose:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\ensure_device_log_docker_stack.ps1 -Json
```

Этот скрипт является entrypoint для агентов. Он:

- проверяет наличие Docker CLI и готовность Docker daemon;
- при необходимости запускает Docker Desktop и ждет готовности daemon;
- создает локальный `tools/device-log-collector/.env.local` с `NGROK_AUTHTOKEN` и `NGROK_DOMAIN`;
- берет ngrok token из `$env:NGROK_AUTHTOKEN`, существующего `.env.local` или `%LOCALAPPDATA%\ngrok\ngrok.yml`;
- останавливает старый non-Docker supervisor/collector/ngrok, если они занимают тот же порт;
- выполняет `docker compose up -d --quiet-pull --remove-orphans`; на первом запуске или с `-Rebuild` дополнительно собирает локальные images;
- ждет `http://127.0.0.1:8765/health`;
- ждет `https://ladle-substance-spray.ngrok-free.dev/health` с header `ngrok-skip-browser-warning: true`;
- возвращает JSON со статусом health и compose-контейнеров.

Локальный `.env.local` содержит секрет ngrok и не коммитится.

Обычный ensure не пересобирает collector image, если `lostcyberhamster/device-log-collector:local` уже существует. Это уменьшает лишние перезапуски. После изменений `server.js`, `Dockerfile` или config запускать с явным rebuild:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\ensure_device_log_docker_stack.ps1 -Rebuild -Json
```

Проверить текущее состояние без попытки поднять стек:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\check_device_log_stack.ps1 -Json
```

## Bootstrap prompt для другого ноутбука

Если проект переносится на другой Windows-ноутбук, можно дать агенту prompt ниже. Он рассчитан на точное восстановление такой же инфраструктуры: Docker Compose collector/ngrok, закрепленный ngrok domain, локальный `DeviceLogs/android`, ensure/check scripts и автозапуск.

Перед запуском важно понять режим общего endpoint:

- Точно такой же endpoint `https://ladle-substance-spray.ngrok-free.dev/upload` возможен только с тем же ngrok account/token и тем же закрепленным domain.
- Несколько ноутбуков могут одновременно держать один endpoint только если все активные ngrok agents запущены с `--pooling-enabled`. В нашем Docker stack этот флаг уже включен.
- Pooled endpoint распределяет requests между активными ноутбуками, а не дублирует каждый upload на все collectors. Конкретный upload может сохраниться в `DeviceLogs/android` только на одном из активных ноутбуков.
- Если на новом ноутбуке используется другой ngrok domain, нужно заменить `NGROK_DOMAIN`, обновить `LostCyberHamster/Assets/Resources/Diagnostics/device_log_settings.json`, пересобрать APK и установить новый билд на телефон.

Готовый prompt для агента:

```text
Ты работаешь в репозитории LostCyberHamster на новом Windows-ноутбуке. Нужно поднять такую же dev-инфраструктуру Android device logging через Docker + ngrok, как описано в docs/android_ngrok_device_logging.md.

Цель: установленный Android APK должен отправлять diagnostic logs на публичный endpoint ngrok, ngrok должен прокидывать трафик в локальный Docker collector, collector должен сохранять uploads в DeviceLogs/android, а агент должен видеть ready-состояние через check/ensure scripts.

Выполни строго по шагам:

1. Прочитай правила проекта:
   - docs/rules/AGENTS.md
   - docs/android_ngrok_device_logging.md
   - при необходимости docs/rules/agent_tools.md

2. Проверь рабочую ветку и код:
   - checkout должен быть на integration/unity-live или на ветке, которая содержит коммит с Docker stack for Android device logs.
   - В репозитории должны существовать:
     - tools/device-log-collector/docker-compose.yml
     - tools/device-log-collector/Dockerfile
     - tools/device-log-collector/Dockerfile.ngrok
     - tools/device-log-collector/ensure_device_log_docker_stack.ps1
     - tools/device-log-collector/check_device_log_stack.ps1
     - tools/device-log-collector/ngrok-watchdog.sh
     - tools/device-log-collector/device-log-collector.config.json

3. Проверь prerequisites:
   - Windows PowerShell доступен.
   - Docker Desktop установлен.
   - Команды `docker version` и `docker compose version` выполняются.
   - Если Docker daemon не запущен, `ensure_device_log_docker_stack.ps1` попробует запустить Docker Desktop сам, но если Docker Desktop не установлен или требует ручной настройки WSL2/лицензии/login, остановись и попроси пользователя завершить установку.

4. Получи ngrok credentials безопасно:
   - Нужен ngrok authtoken от того account, которому принадлежит domain.
   - Для точного совпадения текущего endpoint нужен domain: ladle-substance-spray.ngrok-free.dev
   - Не печатай authtoken в чат, логи или commit.
   - Предпочтительный способ: попросить пользователя один раз выполнить `ngrok config add-authtoken <token>` или установить `$env:NGROK_AUTHTOKEN` только в текущей сессии.
   - Скрипт сам создаст `tools/device-log-collector/.env.local`; этот файл локальный и не должен коммититься.

5. Убедись, что все активные ноутбуки, которые держат тот же ngrok domain, обновлены до pooling-режима.
   - В `tools/device-log-collector/docker-compose.yml` у ngrok service должен быть флаг `--pooling-enabled`.
   - Если старый ноутбук еще обслуживает `ladle-substance-spray.ngrok-free.dev` без `--pooling-enabled`, новый ноутбук не сможет подключиться к тому же domain.
   - Решение: обновить старый ноутбук до этого коммита и запустить `ensure_device_log_docker_stack.ps1 -Rebuild -Json` либо временно остановить старый stack.
   - Помни, что pooled endpoint load-balances requests: один upload попадает на один из активных collectors, а не во все `DeviceLogs/android` сразу.

6. Из корня репозитория запусти ensure:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\ensure_device_log_docker_stack.ps1 -Json

7. Если collector/ngrok images еще не существуют или менялись Dockerfile/server.js/config, запусти с rebuild:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\ensure_device_log_docker_stack.ps1 -Rebuild -Json

8. Успешный результат ensure:
   - `localHealth.ok` = true
   - `publicHealth.ok` = true
   - контейнер `lostcyberhamster-device-log-collector` running/healthy
   - контейнер `lostcyberhamster-device-log-ngrok` running/healthy или running сразу после старта, затем healthy после start period
   - `DeviceLogs/android` существует

9. Отдельно проверь состояние:
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\check_device_log_stack.ps1 -Json

   Ожидаемый результат:
   - `ready` = true
   - `dockerReady` = true
   - `localHealth.ok` = true
   - `publicHealth.ok` = true

10. Проверь публичный health вручную:
    $headers = @{ "ngrok-skip-browser-warning" = "true" }
    Invoke-RestMethod -Method Get -Uri "https://ladle-substance-spray.ngrok-free.dev/health" -Headers $headers

11. Сделай smoke upload через публичный endpoint:
    $headers = @{
      "X-LCH-Device-Log-Token" = "lost-cyber-hamster-device-logs"
      "ngrok-skip-browser-warning" = "true"
    }
    $createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    $body = @{
      metadata = @{
        sessionId = "new-laptop-smoke"
        reason = "new_laptop_docker_smoke"
        createdAtUtc = $createdAtUtc
        buildLabel = "android-dev-ngrok-logs"
        endpointUrl = "https://ladle-substance-spray.ngrok-free.dev/upload"
        deviceModel = "agent-new-laptop-smoke"
      }
      diagnosticLogFileName = "diagnostic_log.txt"
      diagnosticLogEncoding = "utf-8"
      diagnosticLogBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("new laptop docker smoke $createdAtUtc"))
      diagnosticLogTruncated = $false
    } | ConvertTo-Json -Depth 6
    Invoke-RestMethod -Method Post -Uri "https://ladle-substance-spray.ngrok-free.dev/upload" -Headers $headers -Body $body -ContentType "application/json"

12. Проверь, что smoke upload сохранился:
    Get-ChildItem -LiteralPath "DeviceLogs/android" -Directory |
      Sort-Object LastWriteTime -Descending |
      Select-Object -First 5 |
      ForEach-Object {
        $metadataPath = Join-Path $_.FullName "metadata.json"
        if (Test-Path -LiteralPath $metadataPath) {
          Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8 | ConvertFrom-Json |
            Select-Object reason, sessionId, deviceModel, endpointUrl
        }
      }

13. Установи автозапуск ensure при входе пользователя в Windows:
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\install_device_log_stack_task.ps1 -StartNow

    Нормальные варианты результата:
    - Windows Scheduled Task `LostCyberHamsterDeviceLogStack`
    - или, если нет прав, Startup shortcut:
      `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\LostCyberHamsterDeviceLogStack.lnk`

14. Если что-то не поднялось:
    - Не коммить `.env.local`.
    - Проверь `docker ps -a --filter "name=lostcyberhamster-device-log"`.
    - Посмотри logs:
      docker compose --env-file .\tools\device-log-collector\.env.local -f .\tools\device-log-collector\docker-compose.yml logs --tail 120
    - Если public health 404/timeout, проверь:
      - правильный ли `NGROK_DOMAIN`;
      - не занят ли domain другим ноутбуком без `--pooling-enabled`;
      - валиден ли ngrok authtoken;
      - есть ли интернет;
      - healthy ли collector.
    - Если public health зеленый, но smoke upload не появился локально, проверь другой активный pooled ноутбук: request мог попасть туда.

15. Финальный отчет пользователю должен содержать:
    - какой domain используется;
    - где лежит `.env.local` без показа token;
    - результат ensure/check;
    - путь к последнему smoke upload в `DeviceLogs/android`;
    - установлен ли автозапуск;
    - были ли нужны ручные действия пользователя.

Не меняй production settings. Не коммить секреты, `DeviceLogs/`, `.env.local`, Docker-generated state или локальные логи.
```

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

- `lostcyberhamster-device-log-collector` - Node.js HTTP collector, слушает `0.0.0.0:8765` внутри контейнера, опубликован на ноутбуке как `127.0.0.1:8765`, пишет в bind mount `DeviceLogs/android`;
- `lostcyberhamster-device-log-ngrok` - локальный wrapper image на базе официального `ngrok/ngrok:3-alpine`, поднимает tunnel на `http://collector:8765` с закрепленным `--url=https://ladle-substance-spray.ngrok-free.dev` и `--pooling-enabled`.

Оба сервиса имеют `restart: unless-stopped`. У collector есть Docker healthcheck, а ngrok стартует только после healthy collector.

Ngrok контейнер дополнительно запускается через `ngrok-watchdog.sh`: wrapper проверяет публичный `/health` с header `ngrok-skip-browser-warning: true`. Если несколько проверок подряд не проходят, wrapper завершает контейнер с ошибкой, и Docker restart policy поднимает ngrok заново. Это закрывает случай, когда процесс жив, но публичный tunnel перестал отдавать route.

`--pooling-enabled` нужен для одновременной работы нескольких ноутбуков на одном закрепленном ngrok domain. Все активные ноутбуки должны быть запущены с этим флагом. Ngrok будет балансировать requests между agents, поэтому upload из телефона может сохраниться на любом активном ноутбуке в его локальном `DeviceLogs/android`.

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

## Автозапуск

Installer регистрирует one-shot ensure при входе пользователя в Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\install_device_log_stack_task.ps1 -StartNow
```

Имя задачи:

```text
LostCyberHamsterDeviceLogStack
```

Сначала installer пробует Windows Scheduled Task. Если прав не хватает, создает fallback:

```text
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\LostCyberHamsterDeviceLogStack.lnk
%LOCALAPPDATA%\LostCyberHamster\device-log-stack\start_device_log_stack.cmd
```

В Docker-режиме fallback launcher не держит бесконечный restart-loop. Он один раз вызывает `ensure_device_log_docker_stack.ps1`, а дальнейшую живучесть обеспечивают Docker restart policies.

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

## Где лежат логи

Output root задается в `tools/device-log-collector/device-log-collector.config.json`:

```text
DeviceLogs/android
```

Каждый upload сохраняется отдельной папкой:

```text
DeviceLogs/android/<createdAt>_<deviceModel>_<reason>_<sessionId>/
```

Внутри:

- `metadata.json` - device/build/session/reason/scene/endpoint/current level;
- `diagnostic_log.txt` - snapshot/tail diagnostic log из билда;
- `package.json` - summary сохраненного пакета;
- `_requests.log` в output root - access log collector;
- `_probes.log` в output root - startup probe записи.

## Как агент читает

Агент читает локальные файлы, а не телефон и не ngrok API.

Перед мобильным тестом:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\ensure_device_log_docker_stack.ps1 -Json
```

Быстро посмотреть свежие uploads текущего ngrok-билда:

```powershell
$root = "DeviceLogs/android"
Get-ChildItem -LiteralPath $root -Directory |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 20 |
    ForEach-Object {
        $metadataPath = Join-Path $_.FullName "metadata.json"
        if (Test-Path -LiteralPath $metadataPath) {
            $metadata = Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($metadata.buildLabel -eq "android-dev-ngrok-logs" -or $metadata.endpointUrl -like "*ladle-substance-spray.ngrok-free.dev*") {
                [pscustomobject]@{
                    Time = $_.LastWriteTime
                    Reason = $metadata.reason
                    Scene = $metadata.activeScene
                    Device = $metadata.deviceModel
                    Endpoint = $metadata.endpointUrl
                    Path = $_.FullName
                }
            }
        }
    } | Format-Table -AutoSize
```

Прочитать diagnostic log конкретного upload:

```powershell
Get-Content -LiteralPath "DeviceLogs/android/<upload-dir>/diagnostic_log.txt" -Tail 200
```

Проверить локальный collector:

```powershell
Invoke-RestMethod -Method Get -Uri "http://localhost:8765/health"
```

Проверить публичный ngrok route:

```powershell
$headers = @{ "ngrok-skip-browser-warning" = "true" }
Invoke-RestMethod -Method Get -Uri "https://ladle-substance-spray.ngrok-free.dev/health" -Headers $headers
```

## Legacy fallback

Старые non-Docker скрипты оставлены для ручной отладки:

```text
tools/device-log-collector/start_device_log_collector.ps1
tools/device-log-collector/start_device_log_stack.ps1
```

Они не являются основным способом поддерживать инфраструктуру. Если агенту нужен готовый стек для логов с телефона, сначала использовать Docker ensure-скрипт.

## Что важно помнить

- Уже установленный APK не получает новый endpoint автоматически. После изменения `device_log_settings.json` нужен новый билд.
- Это dev-инфраструктура для тестовых Android APK, не production telemetry.
- `diagnostic_log.txt` отправляется snapshots по событиям; отсутствие нового upload не означает, что телефон "не отвечает".
- Для приема логов ноутбук должен быть включен, иметь интернет и запущенный Docker stack.
