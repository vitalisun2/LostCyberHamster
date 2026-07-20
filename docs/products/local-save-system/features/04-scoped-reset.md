# 04. Scoped reset

## Что и зачем

Progress reset создаёт валидный default `PlayerData`, не удаляя настройки, account и чужие `PlayerPrefs` keys.

## Алгоритм

1. `ResetPlayerProgress()` удаляет только `PlayerData` и `PlayerData.Backup`.
2. Создаёт default `PlayerData`.
3. Выполняет `Validate`; для repairable результата — `RepairSafe` и повторный `Validate`.
4. Сохраняет только успешно validated progress.

## Критерии приёмки

- `Settings` и account сохраняются.
- `ResetSettings()` сбрасывает только настройки.
- DevTools full reset только оркестрирует scoped reset operations.
- `PlayerPrefs.DeleteAll` не используется.
- Rejected default data не сохраняется.

## Вне рамок

- Account deletion.
