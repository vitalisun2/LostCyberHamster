# Device Log Collector

Dev-only HTTP collector для логов Android-сборок LostCyberHamster.

## Запуск

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\device-log-collector\start_device_log_collector.ps1
```

Collector слушает `POST /upload`, сохраняет payload в `DeviceLogs/android/` и проверяет заголовок `X-LCH-Device-Log-Token`.

## Unity config

Unity читает `Assets/Resources/Diagnostics/device_log_settings.json`.

Для текущего LAN-сценария endpoint должен смотреть на IP компьютера разработчика:

```json
"endpointUrl": "http://192.168.0.17:8765/upload"
```

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
