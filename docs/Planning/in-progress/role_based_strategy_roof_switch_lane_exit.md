# Role-based strategy: RoofSwitchLaneExit

## Цель

Перевести `RoofSwitchLaneExit` как отдельную roof-state стратегию, не смешивая ее с road `SwitchLane`.

## Текущая привязка

- `RoofSwitchLaneExitPlanner` требует старый `DecisionPoint`, `IsDecisionRequired` и first obstacle как context.
- Состояние: `RoofRun`, on roof, not shifting, есть roof support.
- Target lane - opposite lane.
- Safety: на fire shift не должно быть target roof support, затем `RoofExitSafety.IsSafeDuringRunFromRoof`.
- Energy cost action равен `0`.

## Role-based доработка

- Создать `RoofSwitchLaneExitStrategyNew` в `StrategiesNew/RoofSwitchLaneExit`.
- Использовать первый `BlockingThreat` в `DecisionPointNew.Chain` как context obstacle; если threat нет, strategy не создает action.
- Убрать `FireBeforeObstacle`/`IsDecisionRequired`; latest fire shift считать от context obstacle и safety interval.
- Переиспользовать `SwitchLaneFireWindowCalculator`, если контракт не меняется.
- Держать strategy отдельно от road `SwitchLaneStrategyNew`.

## Не переносить

- Road switch-lane logic в эту стратегию.
- Старый deadline через optional/required decision points.

## Проверка

- Roof-state branch может выбрать смену линии со сходом, если passive/current-lane exit unsafe.
- Если на target lane есть roof support в момент fire, action запрещается.
- После симуляции state становится ground `Run` на target lane.
