# 03. Валидация и recovery

## Что и зачем

Missing, corrupt и old saves не создают invalid runtime state. Safe repairs отделены от ambiguous data, которую нельзя молча исправлять.

## Load pipeline

1. Deserialize.
2. Migrate.
3. `PlayerDataValidationResult Validate(PlayerData data)`.
4. Для repairable результата вызвать `RepairSafe(data, result)`.
5. Повторить `Validate`.
6. Для rejected primary проверить единственный `PlayerData.Backup`.
7. Валидный backup повысить в primary, не ротируя повреждённый primary.
8. Если оба кандидата отсутствуют или rejected, записать validated defaults.

## Критерии приёмки

- Safe repair детерминирован и идемпотентен.
- Null collections, точные set-like duplicates и отсутствующий пустой catalog progress repairable.
- Null `PlayerData`, negative/overflow resources, contradictory reward flags, conflicting duplicate level records и unknown purchased skin rejected без silent mutation.
- Raw level progress проверяется до нормализации; malformed values не отбрасываются и не clamp-ятся молча.
- Load exceptions перехватываются на persistence boundary.
- Programmer errors используют exception/assert; ожидаемые business rejects возвращают result.
- Missing, corrupt и old saves восстанавливаются без crash.
- Normal save ротирует в backup только loadable и valid текущий primary.
- Повреждённый primary никогда не перезаписывает хороший backup.
- Corrupt или rejected backup очищается, если primary уже valid либо выбран default.

## Вне рамок

- Ambiguous automatic repair.
- Business operation validation внутри persistence layer.
