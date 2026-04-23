# Roof obstacle strategies plan

## 1. Roof landing: `bigNotAlive` / `mediumNotAlive`

Ситуация: хомяк бежит по дороге и должен запрыгнуть на крышу `bigNotAlive` или `mediumNotAlive`.

Что нужно:

- `JumpToRoofStrategy` для поиска окна обычного `Jump`, который в runtime даёт `JumpOnRoof`;
- отдельный planning transition в `RoofRun`;
- отдельный bot handler под landing на крышу, потому что текущий `JumpActionHandler` завершает действие только при возврате в `Run`, а здесь успешный итог - `RoofRun`.

Краткая логика:

- детектим впереди roof obstacle на текущей линии;
- ищем fire window, где runtime-исход равен `JumpOnRoof`;
- после успешного действия переводим planning state в roof-mode.

## 2. Roof threat jump: `smallNotAliveRoadAndRoof`

Ситуация: хомяк уже на крыше, и впереди на той же крыше или на следующей крыше стоит `smallNotAliveRoadAndRoof`.

Что нужно:

- `RoofJumpStrategy`;
- `DecisionPointKind` для roof threat;
- runtime handler для `BotActionKind.RoofJump` в `PlanExecutor`.

Краткая логика:

- в roof-mode считаем `smallNotAliveRoadAndRoof` обязательной угрозой;
- ищем окно обычного `RoofJumpRequest`, где runtime даёт безопасный `RoofJump`;
- после прыжка остаёмся в `RoofRun` на текущей или следующей roof support.

## 3. Roof threat avoid by lane switch

Ситуация: хомяк на крыше и избегает `smallNotAliveRoadAndRoof` не прыжком, а `Tap` на соседнюю линию.

Вывод по ресерчу:

- отдельный `roof switch lane` handler не нужен;
- отдельная roof-aware стратегия нужна.

Почему:

- runtime уже принимает `Tap` в `RoofRun`;
- текущий `SwitchLaneActionHandler` не привязан к дороге и может переиспользоваться;
- текущая `SwitchLaneStrategy` не подходит, потому что запрещает `hamster.IsOnRoof`, работает только от `BlockingGroundObstacle` и не моделирует roof outcome;
- текущий `PlanningStateTransition.ApplyLaneSwitch()` после lane switch сбрасывает `IsOnRoof = false`, а для roof tap итог может быть либо `RoofRun`, либо `RunFromRoof`.

Что нужно:

- отдельная `RoofSwitchLaneStrategy` или отдельная roof-ветка внутри `SwitchLaneStrategy`;
- roof-aware transition после `Tap`;
- проверка, остаётся ли после shift roof support на target lane.

Краткая логика:

- если рядом есть безопасная roof support, shift остаётся roof-to-roof;
- если roof support нет, shift становится осознанным переходом в `RunFromRoof`;
- unsafe window считаем не только по lane overlap, но и по сохранению roof support после shift.

## 4. Итог по ближайшему срезу

Ближайшие три feature-среза:

1. `JumpToRoofStrategy` + handler для landing на `bigNotAlive` / `mediumNotAlive`.
2. `RoofJumpStrategy` + handler для перепрыгивания `smallNotAliveRoadAndRoof` на крыше.
3. `RoofSwitchLaneStrategy` без нового handler, но с новой roof-aware planning логикой.