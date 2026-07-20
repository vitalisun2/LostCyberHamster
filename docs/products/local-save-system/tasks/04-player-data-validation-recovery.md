# 04. PlayerData validation и recovery

## Цель

Разделить valid, safely repairable и rejected saves; обеспечить безопасный load.

## Depends on

- Нет.

## Feature

- [Валидация и recovery](../features/03-validation-and-recovery.md)

## Scope

- `PlayerDataValidationResult Validate(PlayerData data)`.
- `RepairSafe(PlayerData data, PlayerDataValidationResult result)`.
- Pipeline deserialize, migrate, validate, repair, revalidate.
- Один `PlayerData.Backup`: strict-valid rotation, backup promotion и validated default fallback.
- Exception handling на persistence boundary.

## Acceptance

- Valid data не меняется.
- Repair идемпотентен и после него validation успешна.
- Rejected data не получает silent mutation и не перезаписывает good save.
- Missing, corrupt и old saves загружаются без crash.
- Повреждённый primary восстанавливается из backup и не заменяет его при promotion.

## Validation

- Unit cases из [feature criteria](../features/03-validation-and-recovery.md).
- Narrow integration tests missing/corrupt/old save.
- [Automated testing plan](../testing.md).

## Out of scope

- Ambiguous repair policy.
