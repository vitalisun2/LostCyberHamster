# SwitchLane handoff latency analysis — 2026-06-13

## Scope

После исправления `SuperJumpOver bigAlive` уровень `01_New_York/Morning/level_01` стал падать позже: цепочка `SuperJumpOver -> SwitchLane -> JumpOver`, где `JumpOver` отменялся сразу после завершения `SwitchLane` из-за уже закрытого trigger-window.

## Источники

- `EditorLogs/diagnostic_log.txt`, канал `BOT`, прогон `01_New_York/Morning/level_01`.
- `LostCyberHamster/Assets/Scripts/Bot/Execution/PlanExecutor.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneTiming.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneFireWindowCalculator.cs`
- `LostCyberHamster/Assets/Scripts/GameEngine/Controllers/ShiftTransformAnimatorController.cs`
- `LostCyberHamster/Assets/Animations/Hamster/ShiftTransformAnimator.controller`

## Факты

- `PlanExecutor` делает immediate handoff в тот же tick после `Completed`.
- В новом fail `SwitchLane` завершился, затем `JumpOver` сразу получил `trigger-gate-cancel after-window-close`.
- `SwitchLaneTiming.DecisionDuration = 0.45f` совпадает с `ShiftTransformAnimator.controller` transition duration.
- Runtime `IsShifting` обновляется через `TapMechanics.OnUpdate()`, а бот тикает в `OnLateUpdate()`.
- Если tap отправлен в `LateUpdate`, Animator transition стартует/фиксируется в следующих кадрах, поэтому следующий bot action надёжно доступен не ровно через `0.45с`, а после frame-latency.

## Корень проблемы

Planning считал `SwitchLane` завершённым ровно по длительности Animator transition. Runtime отдаёт следующий action только после наблюдаемого `IsShifting=false` в игровом цикле, что добавляет frame-latency. Из-за этого планер разрешал хвост `SwitchLane -> JumpOver`, который на runtime приходил к `JumpOver` уже после закрытия окна.

## Решение

Учитывать runtime handoff latency в `SwitchLaneTiming.DecisionTravel`. Это меняет общий planning-инвариант `SwitchLane`, а не локально расширяет `JumpOver` окно или trigger gate.

## Проверка

- `01_New_York/Morning/level_01` после правки проходит прежний handoff `SwitchLane -> JumpOver` без immediate cancel.
- Дальше уровень падает позже по отдельной причине экономики/энергии; regression set не запускался в этой итерации.
