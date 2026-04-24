# Roof obstacle strategies plan

## 1. Current slice: roof landing on `bigNotAlive` / `mediumNotAlive`

Ситуация: хомяк бежит по дороге и должен обычным `Jump` приземлиться на крышу препятствия, получив в runtime исход `JumpOnRoof`.

### Что уже сделано

- выделен отдельный decision point `RoofLanding`, чтобы посадка на крышу не смешивалась с обычным `BlockingGroundObstacle`;
- добавлена `JumpOnRoofStrategy` с точным поиском fire window, где shared runtime resolver подтверждает именно `JumpOnRoof`;
- стратегия строит `PlannedAction` c `BotActionKind.JumpOnRoof`, `TriggerX`, `CompletionWorldShift`, `TargetObstacleInstanceId` и стоимостью энергии;
- добавлен отдельный planning transition в `RoofRun` через `PlanningStateTransition.ApplyRoofRunAfterLanding(...)`;
- добавлена retained revalidation для `BotActionKind.JumpOnRoof`, чтобы уже выбранное jump-based действие не пересобиралось, если окно и exact runtime outcome всё ещё валидны;
- `BotActionKind.JumpOnRoof` зарегистрирован в `PlanExecutor`;
- `JumpOnRoofStrategy` зарегистрирована в `RuntimeBotController`.

### Что означает `planned action for JumpOnRoof`

Это не отдельная подсистема, а конкретный результат planning-слоя: стратегия не просто говорит «можно прыгнуть на крышу», а создаёт готовый `PlannedAction`, который потом сможет исполнить execution-слой. В этом action уже лежат точка запуска, ожидаемый world shift до завершения и target obstacle.

### Что осталось в текущем срезе

- выполнить полную runtime-валидацию: `recompile_scripts` и прогон всех четырёх test level-ов без регрессии в уже существующих трёх сценариях.

## 2. Next slice: roof threat jump for `smallNotAliveRoadAndRoof`

Это отдельная следующая механика, не часть незавершённого roof landing execution.

Что понадобится:

- `RoofJumpStrategy`;
- отдельный `DecisionPointKind` для roof threat;
- runtime handler для `BotActionKind.RoofJump` в `PlanExecutor`.

Краткая логика:

- в roof-mode считаем `smallNotAliveRoadAndRoof` обязательной угрозой;
- ищем окно `RoofJumpRequest`, где runtime даёт безопасный `RoofJump`;
- после прыжка остаёмся в `RoofRun` на текущей или следующей roof support.

## 3. Next slice: roof threat avoid by lane switch

Это тоже отдельная следующая механика, не часть незавершённого roof landing execution.

Вывод по ресерчу:

- отдельный `roof switch lane` handler не нужен;
- нужна отдельная roof-aware planning логика.

Почему:

- runtime уже принимает `Tap` в `RoofRun`;
- текущий `SwitchLaneActionHandler` не привязан к дороге и может переиспользоваться;
- текущая `SwitchLaneStrategy` не подходит, потому что запрещает `hamster.IsOnRoof`, работает только от `BlockingGroundObstacle` и не моделирует roof outcome;
- текущий `PlanningStateTransition.ApplyLaneSwitch()` после lane switch сбрасывает `IsOnRoof = false`, а для roof tap итог может быть либо `RoofRun`, либо `RunFromRoof`.

Что понадобится:

- отдельная `RoofSwitchLaneStrategy` или отдельная roof-ветка внутри `SwitchLaneStrategy`;
- roof-aware transition после `Tap`;
- проверка, остаётся ли после shift roof support на target lane.

## 4. Current status summary

1. Roof landing: planning и execution подключены end-to-end; осталась только runtime-валидация.
2. Roof threat jump: не начат.
3. Roof switch lane: не начат.