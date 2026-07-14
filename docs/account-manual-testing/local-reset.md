# Локальный сброс аккаунта

**Статус:** не выполнен пользователем (pending).

## Предусловия

- Игра запущена с текущей локальной identity.

## Шаги

1. Открыть `DEV` → `Account` и нажать `RESET LOCAL ACCOUNT STATE`.

## Ожидаемый результат

- Локальные сессии Unity Authentication и Player Accounts очищены сразу, без подтверждения.
- Серверная привязка, прогресс, Cloud Save и Analytics не изменены.
- Следующий запуск Account выбирает `CreateGuest`.
