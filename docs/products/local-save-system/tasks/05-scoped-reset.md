# 05. Scoped reset

## Цель

Сбрасывать progress без удаления настроек, account и чужих prefs.

## Depends on

- [04. PlayerData validation и recovery](04-player-data-validation-recovery.md)

## Feature

- [Scoped reset](../features/04-scoped-reset.md)

## Scope

- `ResetPlayerProgress()` только для primary и backup progress keys.
- Validate, safe repair и повторная validate default data.
- `ResetSettings()` только для настроек.
- DevTools orchestration scoped reset operations.
- Удаление использования `PlayerPrefs.DeleteAll`.

## Acceptance

- Progress reset сохраняет `Settings`, account и чужие keys.
- Только valid default progress сохраняется.
- Full DevTools reset вызывает явные scoped operations.

## Validation

- Narrow integration test progress-only reset.
- [Automated testing plan](../testing.md).

## Out of scope

- Account deletion.
