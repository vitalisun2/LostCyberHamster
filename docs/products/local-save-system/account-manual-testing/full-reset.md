# Полный сброс привязанного аккаунта

**Статус:** выполнен пользователем (Passed).

## Предусловия

- Unity Player Account связан с тестовым UGS Player ID.

## Шаги

1. Открыть `DEV` → `Account` и нажать `FULL RESET LINKED ACCOUNT`.
2. Если открылся flow Unity Player Accounts, завершить вход.
3. Дождаться результата операции.

## Ожидаемый результат

- Обе reset-кнопки недоступны во время операции.
- Unity Player Account отвязан от серверного UGS Player ID.
- Локальные сессии очищены, итоговое состояние Account — `NotStarted`.
- Серверный аккаунт не удалён.

## RCA: Windows Unity Editor

В Unity Authentication 3.4.1 standalone OAuth callback использовал hostname `localhost`. В Windows Unity Editor под сетевым фильтром браузер и listener могли выбрать разные loopback address families: callback не доходил до SDK, а flow оставался в ожидании.

## Постоянное исправление

- Официальный `com.unity.services.authentication@3.4.1` добавлен в `Packages/com.unity.services.authentication` как embedded package, чтобы override хранился в Git, а не в очищаемом `Library/PackageCache`.
- Для standalone callback hostname заменён на явный IPv4 loopback `127.0.0.1` как в listener prefix, так и в redirect URI.
- Официальный OAuth 2.0 Authorization Code + PKCE flow, проверка `state` и token exchange не изменены. Android и другие mobile flow не затронуты.

## Проверка постоянного исправления

**Статус:** выполнена пользователем (Passed).

После Unity package resolve повторить сценарий выше в Windows Unity Editor с тем же сетевым фильтром.

## Откат и сопровождение

- Для отката удалить embedded package `Packages/com.unity.services.authentication`, затем через UPM восстановить registry-версию 3.4.1 и проверить её запись в `Packages/packages-lock.json`.
- При обновлении Authentication package сверить override с upstream: удалить его, если проблема исправлена Unity, или перенести минимальную правку на новую версию.
