---
description: "Прогнать все тестовые уровни бота автопрогоном и вывести итоговый SUMMARY. Список уровней брать динамически тем же способом, что и Tools/Test Level/Launch...: источник истины — все test*.json под Assets/Content/locations/**/levels/**. Используй когда нужно: запустить валидацию, проверить регрессию, прогнать все уровни после правок бота."
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
4. Не используй никакой hardcoded-список уровней из этого prompt. Полный список уровней нужно брать динамически тем же способом, что делает `Tools/Test Level/Launch...` в `TestLevelLauncher.cs` и `invoke_run_all_test_levels.ps1`: источник истины — все `test*.json` под `LostCyberHamster/Assets/Content/locations/**/levels/**`.
5. После завершения прогона проверь, что в итоговом `SUMMARY` скрипта перечислены все динамически найденные test-level адреса. Если нужно перепроверить полноту, ориентируйся на ту же логику discovery, что у launcher-а, а не на этот prompt.
6. Для каждого уровня используй результат, который уже печатает `invoke_run_all_test_levels.ps1`:

   **A. Статус прогона** — `WIN`, `FAIL`, `ERR` или `SEMF` из итогового `SUMMARY`.

   **B. Semantic action summary** — строку `actions=[...]` из итогового `SUMMARY` и, при необходимости, детали по `diagnosticLogPath` из `test_level_response.json`.

7. Выведи итоговый SUMMARY в формате:

```
SUMMARY
─────────────────────────────────────────────────────────────
01_New_York/Morning/test_switch_lane        : WIN  actions=[SwitchLane=5]
01_New_York/Morning/test_jump_over          : WIN  actions=[JumpOver=2]
01_New_York/Morning/test_superjump_over     : FAIL actions=[SuperJumpOver=1]
01_New_York/Morning/test_jump_on_roof       : WIN  actions=[JumpOnRoof=4]
─────────────────────────────────────────────────────────────
Passed: 3 / 4
```

8. Если хотя бы один уровень завершился с `FAIL`, `ERR` или `SEMF`:
   - кратко опиши расхождение по итогам `SUMMARY` и, если нужно, по `diagnosticLogPath`: какой уровень упал, какой был статус, какие действия зафиксированы
   - **не чини** — только сообщи. Правки — отдельная задача.

## Важно

- Выполнять все шаги автономно, без запроса подтверждения у пользователя.
- Не перекомпилировать скрипты перед прогоном — скрипт делает это сам.
- Не запускать уровни по одному вручную — только через `invoke_run_all_test_levels.ps1`.
- Не поддерживать вручную список test-level'ов в этом prompt: новые уровни должны подхватываться автоматически через discovery из `Assets/Content/locations`.
- После вывода SUMMARY остановиться и ждать инструкций пользователя.
