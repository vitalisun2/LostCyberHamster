# NY Level 01 Life Loss Analysis

Дата: 2026-06-23

## Scope

Уровень: `01_New_York/Morning/level_01`.

Цель: найти и исправить реальные потери жизни на первом уровне Нью-Йорка после async replan/max depth 6 без локальных патчей по симптомам.

## Commands

- Reproduce:
  `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 240 -TimeScale 1`

## Evidence Log

Первый прогон:

- Command: `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 240 -TimeScale 1`
- Result: `FAIL`.
- Место: второй подряд `medium_difficulty`, перед `bigAlive` на top lane.
- Actual:
  - перед столкновением бот выполнил `JumpOver -> JumpOver -> SwitchLane`;
  - после второго `JumpOver` энергия стала `18`;
  - дальше нужен `SuperJumpOver`, но стратегия вернула `Недостаточно энергии ... нужно 20, доступно 18`;
  - `CollisionController damage ... obstacle=bigAlive ... lane=top`.

Проверка `relief_energy` была отклонена как слишком сильная помощь уровню.
Точечный вариант с одним энергетиком на `x=61` не помог: падение происходит в ранней связке паттерна, до этой координаты.

## Hypotheses

- Ошибка async/replan: опровергнуто. Лог показывает обычный dead-end по энергии, без stale apply/cancel.
- Ошибка выбора действия: опровергнуто для точки падения. Применимые стратегии перечислены в `Bot DEAD_END`; единственный прямой обход требует 20 энергии.
- Контентный energy-budget: подтверждено. Две тяжелые секции подряд оставляют бота с `18/20` перед обязательным super-действием.

## Root Cause

Root cause: в `level_01` два `medium_difficulty` идут подряд, и второй повтор до ранней связки `bigAlive/smallNotAliveRoad*` не дает достаточно энергии для обязательного `SuperJumpOver`.

Кодовый путь:

- `SuperJumpOverStrategy` отсекает применимое действие по контракту энергии: policy cost `20`, available `18`.
- `PlanningGraphBuilder.AddDeadEndBranch` сохраняет ветку, которая дошла до unresolved dead-end.
- `PlanBuilder.BuildDeadEndFallbackResult` возвращает safe-prefix plan с dead-end report, потому что успешной ветки нет.
- `RuntimeBotController.OnLivesLost` подтверждает pending dead-end после фактического damage.

Это не рассинхрон исполнения: collision происходит уже после `Bot DEAD_END` с причиной `available=18`, а не из-за trigger miss или отмены action.

## Fix

- Создан контентный вариант `medium_difficulty_energy`: тот же `medium_difficulty`, но с одним дополнительным `collectableEnergetic` на bottom lane, `x=11.2`.
- Второй `medium_difficulty` в `level_01` заменен на `medium_difficulty_energy`.
- `relief_energy` не используется; помощь ограничена одним энергетиком в проблемной связке.

## Validation

- JSON parse: `PatternsCollection.json` и `level_01.json` валидны.
- `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 300 -TimeScale 1`
  - Result: `WIN level=1 stars=3`.
  - `TEST FINISH state=FINISHED lives=3 energy=78`.
  - `CollisionController damage`, `Bot DEAD_END`, `Damage` markers отсутствуют.
  - Новый энергетик собран: `collectableEnergetic#-357396`, energy `47 -> 77`.

## Regression After Semantic Metrics

Вопрос для второго диагностического прохода: подтвердить, что новая потеря жизни создается не стратегией `SwitchLane` и не collision-моделью, а async-merge контрактом `committedPrefix + tailPlan`: уже запущенный/закоммиченный prefix сохраняется, tail после него строится как root dead-end (`depth=0`), но prefix все равно остается на исполнении.

Факты:

- Fail log перед вторым проходом: после `patternIndex=7` план стал `JumpOver -> JumpOn -> SwitchLane`, затем `SwitchLane` завершился в `bottom`, после чего collision произошел с `bigAlive` на `bottom`.
- Подтвержденный report: `reason=ActionCompleted`, `depth=0`, `projection=0.00`; это означает dead-end в tail-root, а не после раскрытия новой ветки на глубину 3.
- Code path:
  - `RuntimeBotController.BuildCommittedPrefix` безусловно сохранял первые 2 action текущего плана.
  - `AsyncPlanRebuilder.BuildPlanForRequest` симулировал committed prefix и строил tail уже после него.
  - `PlanBuilder.BuildDeadEndFallbackResult` возвращал `BotPlan` даже при `DeadEndReport`.
  - `RuntimeBotController.ApplyPlanBuildResult` сохранял pending dead-end report и передавал plan в executor.
- Значит второй еще не начатый action был превращен в immutable action. Свежий replan мог знать, что после него tail dead-end, но заменить сам action уже не мог.

Root cause: async committed-prefix policy смешивала два разных понятия: физически необратимое действие и удобный lookahead для instant handoff. Для async replan immutable должен быть только реально in-progress head-action; все еще не начатые действия должны быть заменяемы свежим планом.

Правка:

- `BuildCommittedPrefix` теперь сохраняет только `request.IsActionInProgress ? head : empty`.
- `TryApplyCompletedAsyncReplan` отклоняет async-result, если после capture действие успело стартовать, а result был построен без in-progress контекста.
- Временный `ASYNC_DIAG` удален.

Validation after async-prefix fix:

- Build: `dotnet build LostCyberHamster/LostCyberHamster.sln --no-restore` -> 0 errors, existing warnings only.
- Command: `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 600 -TimeScale 1`
- Result: `WIN level=1 stars=3`, `TEST FINISH state=FINISHED lives=3 energy=80`.
- `CollisionController damage`, `Bot DEAD_END`, `ASYNC_DIAG` отсутствуют.
- Есть один `CANCEL PassiveCollect target-not-found` после исчезновения collectable; потери жизни/тупика нет.
