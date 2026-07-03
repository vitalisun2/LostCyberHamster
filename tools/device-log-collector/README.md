# Device Log Collector

Dev-only HTTP collector для логов Android-сборок LostCyberHamster.

Основной сценарий сейчас описан в `docs/android_ngrok_device_logging.md`: установленные Android APK сами отправляют snapshots `diagnostic_log.txt` через ngrok на этот локальный collector.

## Запуск

Ручной запуск только collector-а:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\start_device_log_collector.ps1
```

Устойчивый запуск collector + ngrok supervisor:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\start_device_log_stack.ps1
```

Установить автозапуск при входе в Windows:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\install_device_log_stack_task.ps1 -StartNow
```

Installer сначала пробует Windows Scheduled Task. Если Task Scheduler недоступен из-за прав, создается user Startup shortcut с restart-loop launcher.

Collector слушает `POST /upload`, сохраняет payload в `DeviceLogs/android/` и проверяет заголовок `X-LCH-Device-Log-Token`.

## Unity config

Unity читает `Assets/Resources/Diagnostics/device_log_settings.json`.

Для текущего ngrok-сценария endpoint должен смотреть на закрепленный ngrok domain:

```json
"endpointUrl": "https://ladle-substance-spray.ngrok-free.dev/upload"
```

Для LAN-сценария endpoint может смотреть на IP компьютера разработчика, например `http://192.168.0.17:8765/upload`, но такой APK работает только в той же локальной сети.

Перед production-сборками `enabled` должен быть `false`.

## Быстрая проверка

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
