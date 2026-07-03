# Android Ngrok Device Logging

Документ описывает dev-инфраструктуру, через которую Android-билды LostCyberHamster, уже установленные на телефонах, сами отправляют diagnostic logs на рабочий ноутбук через публичный ngrok endpoint.

## Суть

Это push-based логирование: агент не подключается к телефону и не запрашивает у него логи. Runtime игры сам отправляет snapshots `diagnostic_log.txt` на HTTP collector при важных событиях. Collector сохраняет каждый upload локально в `DeviceLogs/android`, а агент читает уже эти файлы из workspace.

Схема:

```text
Android APK -> ngrok HTTPS endpoint -> localhost:8765 -> tools/device-log-collector -> DeviceLogs/android
```

Важно: collector не опрашивает ngrok и не "читает" его. На ноутбуке одновременно работают два процесса:

- `ngrok` agent держит исходящее tunnel-соединение к ngrok cloud и принимает публичный HTTPS-трафик для dev domain;
- `tools/device-log-collector/server.js` слушает локальный HTTP-порт `8765` и сохраняет пришедшие requests.

Когда игра делает `POST https://ladle-substance-spray.ngrok-free.dev/upload`, ngrok cloud принимает этот request, tunnel переносит его на ноутбук, а локальный collector получает его как обычный `POST /upload` на `localhost:8765`.

Текущий dev endpoint:

```text
https://ladle-substance-spray.ngrok-free.dev/upload
```

Текущий build label:

```text
android-dev-ngrok-logs
```

## Клиент в игре

Unity config находится здесь:

```text
LostCyberHamster/Assets/Resources/Diagnostics/device_log_settings.json
```

Игра читает этот JSON через `Resources.Load("Diagnostics/device_log_settings")`. Для Android upload включается, когда:

- `enabled: true`;
- `allowOnAndroid: true`;
- `endpointUrl` не пустой;
- текущая платформа разрешена в `DeviceLogUploadSettings.IsPlatformAllowed()`.

Основные runtime-классы:

- `DeviceLogReporter` - создается до загрузки сцен и подписывается на lifecycle/log events.
- `DeviceLogUploadRunner` - держит очередь upload reasons и отправляет их последовательно.
- `DeviceLogUploader` - читает tail `diagnostic_log.txt`, собирает metadata и делает `POST /upload`.
- `DeviceLogStartupProbe` - дополнительно проверяет `GET /health` и `POST /probe` при старте.

Игра отправляет snapshots не непрерывным стримом, а по событиям:

- `session_started_awake`;
- `startup_after_first_frame`;
- `startup_after_2s`;
- `scene_loaded_<Scene>`;
- `scene_first_frame_<Scene>`;
- `scene_after_1s_<Scene>`;
- `application_paused`;
- `application_quit`;
- `runtime_error` / `runtime_exception`;
- явные точки вроде `tutorial_completed`, `game_start_exception`, `intro_exception`.

Для free ngrok клиент добавляет header:

```text
ngrok-skip-browser-warning: true
```

Без него `GET /health` может получать ngrok browser warning вместо JSON.

## Collector на ноутбуке

Collector находится здесь:

```text
tools/device-log-collector/server.js
tools/device-log-collector/start_device_log_collector.ps1
tools/device-log-collector/device-log-collector.config.json
```

Ручной запуск только collector-а:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\start_device_log_collector.ps1
```

Collector слушает `0.0.0.0:8765` и поддерживает:

- `GET /health` - проверка доступности;
- `POST /probe` - легкий startup probe;
- `POST /upload` - прием payload с `metadata` и base64 diagnostic log.

`POST /upload` требует header:

```text
X-LCH-Device-Log-Token: lost-cyber-hamster-device-logs
```

Это dev-gate, а не production-grade security. Перед production-сборками device log upload должен быть выключен.

## ngrok tunnel

ngrok пробрасывает публичный HTTPS endpoint на локальный collector:

```powershell
ngrok http --domain=ladle-substance-spray.ngrok-free.dev 8765
```

В текущей настройке ngrok account выдает закрепленный free dev domain:

```text
https://ladle-substance-spray.ngrok-free.dev
```

Требования для работы логов из любой сети:

- ноутбук включен и имеет интернет;
- `tools/device-log-collector` запущен;
- `ngrok http 8765` запущен;
- APK собран с `endpointUrl` на ngrok domain;
- Android-устройство имеет интернет.

Если любой из этих пунктов не выполнен, установленная игра продолжит работать, но uploads не дойдут до `DeviceLogs/android`.

## Устойчивость и автозапуск

Для устойчивости используется supervisor-скрипт:

```text
tools/device-log-collector/start_device_log_stack.ps1
```

Он в цикле проверяет:

- локальный collector health: `http://127.0.0.1:8765/health`;
- публичный ngrok route: `https://ladle-substance-spray.ngrok-free.dev/health`.

Если локальный health падает, supervisor перезапускает `node server.js --port 8765`. Если публичный health падает, supervisor перезапускает `ngrok http --domain=ladle-substance-spray.ngrok-free.dev 8765`.

Ручной запуск supervised stack:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\start_device_log_stack.ps1
```

Одноразовая проверка и попытка поднять stack:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\start_device_log_stack.ps1 -Once
```

Диагностика текущего состояния:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\check_device_log_stack.ps1 -Json
```

Autostart installer сначала пытается оформить Windows Scheduled Task:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\install_device_log_stack_task.ps1 -StartNow
```

Task name:

```text
LostCyberHamsterDeviceLogStack
```

Scheduled Task запускается при входе пользователя в Windows, игнорирует повторный запуск, имеет restart-on-failure и держит supervisor живым в фоне. Task хранит абсолютный путь к checkout, из которого был установлен; если репозиторий перемещен или worktree удален, task нужно переустановить.

Если Windows запрещает Task Scheduler в текущем пользовательском контексте, installer создает fallback:

```text
%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\LostCyberHamsterDeviceLogStack.lnk
%LOCALAPPDATA%\LostCyberHamster\device-log-stack\start_device_log_stack.cmd
```

Fallback запускается при входе пользователя в Windows. `.cmd`-launcher держит простой restart-loop: если supervisor PowerShell-процесс завершится, launcher подождет 10 секунд и запустит его снова.

Логи supervisor-а:

```text
tools/device-log-collector/device-log-stack.supervisor.log
tools/device-log-collector/collector.supervised.stdout.log
tools/device-log-collector/collector.supervised.stderr.log
tools/device-log-collector/ngrok.supervised.stdout.log
tools/device-log-collector/ngrok.supervised.stderr.log
```

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
- `_requests.log` в output root - access log collector-а;
- `_probes.log` в output root - startup probe записи.

## Как агент читает

Агент читает локальные файлы, а не телефон и не ngrok API.

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

## Что важно помнить

- Уже установленный APK не получает новый endpoint автоматически. После изменения `device_log_settings.json` нужно пересобрать и переустановить билд.
- Это dev-инфраструктура для тестовых Android APK, не production telemetry.
- `diagnostic_log.txt` отправляется snapshot-ами по событиям; отсутствие нового upload не значит, что телефон "не отвечает" - возможно, не было события отправки или tunnel/collector выключен.
- Если агенту нужно проверить мобильный flow, сначала убедиться, что stack жив через `check_device_log_stack.ps1`, затем просить запустить установленный APK и смотреть новые папки в `DeviceLogs/android`.
