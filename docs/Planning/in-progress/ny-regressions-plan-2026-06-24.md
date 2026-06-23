# NY Regressions Plan

Дата: 2026-06-24

## Scope

Проверить регрессы, замеченные вручную на `01_New_York/Morning/level_01` и `01_New_York/Morning/level_02`, затем повторно пройти все тестовые уровни на win/life-loss/description compliance/лишние `SwitchLane`.

## Наблюдаемые регрессы

1. `level_01` и `level_02`: бот не всегда подбирает монетку, хотя она доступна безопасно, без траты энергии и без более ценной альтернативы.
2. `level_02`: бот теряет жизнь после некоторого расстояния на уровне; точный pattern нужно определить по `Bot PATTERN`/damage логам.
3. Тестовые уровни: после правок evaluator нужно проверить лишние перестроения, особенно в `test_jump_over` и `test_super_jump_over`, где уже был доказан регресс `SwitchLane -> JumpOver`.

## Что проверить

- Для монеток:
  - есть ли `PassiveCollect` candidate в нужной точке;
  - не отсекается ли collectable через `CollectibleValuePolicy`, horizon compare или pruning;
  - не проигрывает ли бесплатная монетка из-за порядка метрик после `EnergyBeforeFirstMajor`, `MajorObjectiveCount`, `EnergyCost`;
  - нет ли stale async plan, который заменяет ветку со сбором монетки более свежей веткой без неё.
- Для потери жизни на `level_02`:
  - найти pattern, obstacle id/type/lane и action sequence перед damage;
  - доказать, где расходится expected/actual: generator, simulator, evaluator/pruning, async apply или execution;
  - если life-loss подтвержден, фиксить сразу, но только после доказанного root cause.
- Для `SwitchLane`:
  - составить по каждому test level последовательность `Bot EXEC FIRE`;
  - сверить с description паттернов;
  - считать лишним `SwitchLane`, если он не приводит к collectable, required lane route, roof setup или безопасному обходу без последующего jump-over.

## Минимальные прогоны

1. `01_New_York/Morning/level_01` вручную/automation: проверить монетки и отсутствие damage.
2. `01_New_York/Morning/level_02`: воспроизвести life-loss и зафиксировать pattern.
3. Targeted test levels:
   - `test_jump_over`
   - `test_super_jump_over`
   - уровни с collectables
4. Полный `tools/invoke_run_all_test_levels.ps1`: итоговая проверка всех тестовых уровней.

## Критерии готовности

- `level_01` и `level_02` проходят без потерь жизни.
- Бесплатные безопасные монетки без более ценной альтернативы собираются.
- Все test levels проходят `WIN`, без `CollisionController damage`, `Bot DEAD_END` и без semantic mismatch с description.
- Лишние `SwitchLane` устранены или доказаны как expected route.
- Временные диагностические логи удалены перед коммитом.
