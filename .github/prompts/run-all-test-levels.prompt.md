---
description: "Прогнать все тестовые уровни бота автопрогоном и вывести итоговый SUMMARY. Используй когда нужно: запустить валидацию, проверить регрессию, прогнать все уровни после правок бота."
name: "Run All Test Levels"
agent: "agent"
---

Прогони все тестовые уровни автопрогоном, соблюдая правила из [agent_tools.md](../docs/rules/agent_tools.md).

## Шаги

1. Убедись, что Unity Editor открыт и проект загружен. Если нет — сообщи об этом и остановись.
2. Запусти скрипт автопрогона:
   ```powershell
   .\invoke_run_all_test_levels.ps1 -TimeoutSeconds 120
   ```
3. Дождись завершения всех уровней.
4. После завершения каждого уровня прочитай его `diagnosticLogPath` из `test_level_response.json` и выполни два анализа:

   **A. WIN/FAIL** — по маркеру `[TEST RESULT]` в логе.

   **B. Покрытие паттернов** — косвенный признак корректности поведения:
   - Подсчитай **ожидаемое** число целевых паттернов: количество `ref` в `patternSequence` уровня, чей `ref` содержит корень имени уровня (т.е. игнорируй `relief`, `relief_energy` и прочие технические паттерны).
   - Подсчитай **фактическое** число выполненных целевых действий в логе: строки `[BotV2 EXEC] COMPLETE` с `desc=`, соответствующим целевому действию уровня (см. таблицу ниже).
   - Проверь: `фактическое == ожидаемое`.

   | Уровень | Корень (фильтр ref) | Целевая строка в логе |
   |---|---|---|
   | test_switch_lane | `test_switch_lane_` | `COMPLETE kind=Tap` + `desc=Switch lane` |
   | test_jump_over | `test_jump_over_` | `COMPLETE kind=Jump` + `desc=Jump over` |
   | test_superjump_over | `test_superjump_over_` | `COMPLETE` + `desc=SuperJump over` |
   | test_jump_on_roof | `test_jump_on_roof_` | `COMPLETE kind=Jump` + `desc=Jump on roof` |

5. Выведи итоговый SUMMARY в формате:

```
SUMMARY
─────────────────────────────────────────────────────────────
test_switch_lane      : WIN    patterns: 5/5 ✓
test_jump_over        : WIN    patterns: 2/2 ✓
test_superjump_over   : FAIL   patterns: 1/2 ✗
test_jump_on_roof     : WIN    patterns: 4/4 ✓
─────────────────────────────────────────────────────────────
Passed: 3 / 4   Pattern coverage: 3 / 4
```

6. Если хотя бы один уровень завершился с FAIL, UNKNOWN или patterns < expected:
   - кратко опиши расхождение: что ожидалось, что получилось, последние строки с `[TEST RESULT]`
   - **не чини** — только сообщи. Правки — отдельная задача.

## Важно

- Выполнять все шаги автономно, без запроса подтверждения у пользователя.
- Не перекомпилировать скрипты перед прогоном — скрипт делает это сам.
- Не запускать уровни по одному вручную — только через `invoke_run_all_test_levels.ps1`.
- После вывода SUMMARY остановиться и ждать инструкций пользователя.
