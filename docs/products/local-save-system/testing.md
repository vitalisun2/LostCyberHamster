# Автоматические проверки Local Save System

## Unit tests

- Commit сохраняет полный snapshot на обязательном checkpoint; failed operation не делает commit.
- Storyline completion и reward status переживают JSON round-trip без дубликатов.
- Valid `PlayerData` не изменяется; safe repair исправляет null collections и точные дубликаты, повторный repair идемпотентен.
- Null data, отрицательные ресурсы, противоречивые rewards, конфликтующий level progress и неизвестные skins отклоняются без мутации.
- Malformed raw level records отклоняются до нормализации; точные дубликаты repairable и идемпотентны.
- Недостаток ресурса, отрицательное начисление и overflow не меняют баланс.
- Scoped progress/settings resets затрагивают только собственные ключи.

## Узкие EditMode integration tests

- Missing, corrupt и устаревший save проходят load/recovery без crash.
- Corrupt primary восстанавливается из единственного backup; bad primary не перезаписывает backup.
- Полный encrypted `PlayerData` round-trip сохраняет level, quest и storyline progress.
- Progress reset сохраняет `Settings`, Account и посторонние `PlayerPrefs` keys.

Тесты добавляются вместе с соответствующей задачей. Реализованный test suite является источником истины; отдельные сценарные документы не ведутся.
