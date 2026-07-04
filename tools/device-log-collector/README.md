# Device Log Collector

Dev-only HTTP collector для логов Android-сборок LostCyberHamster.

Основной сценарий описан в `docs/android_ngrok_device_logging.md`: установленный Android APK сам отправляет snapshots `diagnostic_log.txt` через ngrok на collector основного ноутбука, а collector пишет uploads в Dropbox Exchange.

## Основной запуск

Поднять или проверить весь Docker stack collector + ngrok:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\ensure_device_log_docker_stack.ps1 -Json
```

Скрипт сам:

- запускает Docker Desktop при необходимости;
- находит/создает `%USERPROFILE%\Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android`;
- создает локальный `.env.local` с ngrok token;
- останавливает старый non-Docker stack, если он занимает порт;
- поднимает Docker Compose;
- ждет local health и public ngrok health.

После изменений collector-а пересобрать image явно:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\ensure_device_log_docker_stack.ps1 -Rebuild -Json
```

Проверить состояние без запуска:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\check_device_log_stack.ps1 -Json
```

Установить автозапуск ensure при входе в Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\install_device_log_stack_task.ps1 -StartNow
```

Docker Compose файлы:

```text
tools/device-log-collector/Dockerfile
tools/device-log-collector/Dockerfile.ngrok
tools/device-log-collector/docker-compose.yml
tools/device-log-collector/ngrok-watchdog.sh
```

Контейнеры:

- `lostcyberhamster-device-log-collector`;
- `lostcyberhamster-device-log-ngrok`.

Ngrok контейнер собран как wrapper над официальным `ngrok/ngrok:3-alpine`: он сам проверяет публичный `/health` и перезапускается через Docker restart policy, если tunnel перестал отвечать.

Ngrok должен быть активен только на receiver-ноутбуке. Второй ноутбук не поднимает ngrok/collector и читает уже синхронизированные файлы из Dropbox.

## Unity config

Unity читает `Assets/Resources/Diagnostics/device_log_settings.json`.

Для текущего ngrok-сценария endpoint должен смотреть на закрепленный ngrok domain:

```json
"endpointUrl": "https://ladle-substance-spray.ngrok-free.dev/upload"
```

Перед production-сборками `enabled` должен быть `false`.

## Collector API

Collector слушает:

- `GET /health`;
- `POST /probe`;
- `POST /upload`.

`POST /upload` сохраняет payload в Docker mount `/workspace/DeviceLogs/android`, который на host указывает на:

```text
%USERPROFILE%\Dropbox\exchange\crystal_wave\LostCyberHamster_DeviceLogs\android
```

Upload проверяет header:

```text
X-LCH-Device-Log-Token: lost-cyber-hamster-device-logs
```

## Ручная быстрая проверка

```powershell
$headers = @{ 'X-LCH-Device-Log-Token' = 'lost-cyber-hamster-device-logs' }
$body = @{
  metadata = @{ sessionId = 'manual'; reason = 'manual_test'; createdAtUtc = (Get-Date).ToUniversalTime().ToString('o') }
  diagnosticLogFileName = 'diagnostic_log.txt'
  diagnosticLogEncoding = 'utf-8'
  diagnosticLogBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('hello'))
  diagnosticLogTruncated = $false
} | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri 'http://localhost:8765/upload' -Headers $headers -Body $body -ContentType 'application/json'
```

## Legacy fallback

Старые non-Docker скрипты оставлены для ручной отладки:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\start_device_log_collector.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\start_device_log_stack.ps1
```

Для обычной работы агентов использовать Docker ensure-скрипт.
