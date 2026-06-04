# Role-based strategy: PassiveRoofExit

## Цель

Сохранить `PassiveRoofExit` как реальный planning transition без tap/energy, который переводит `RoofRun` в `Run`, если пассивный сход с крыши безопасен.

## Текущая привязка

- `PassiveRoofExitPlanner` требует `decisionPoint.IsDecisionRequired`.
- Состояние: `RoofRun`, on roof, not shifting, есть roof support.
- `lastRoof` ищется через `RoofRunProjection.TryFindLastPassiveRoof`.
- `RoofExitSafety.IsSafeDuringRunFromRoof` проверяет весь интервал схода.
- Energy cost action равен `0`.

## Role-based доработка

- Создать `PassiveRoofExitStrategyNew` в `StrategiesNew/PassiveRoofExit`.
- Контекстным obstacle считать первый obstacle role-based chain только как planning/execution anchor.
- Safety пассивного схода считать по текущей линии hamster через `RoofExitSafety`.
- Убрать зависимость от `IsDecisionRequired`: наличие `DecisionPointNew` уже означает актуальную planning-ситуацию.
- Сохранить `RoofExitSafety` и симулятор перехода `RoofRun -> Run`.
- Добавлять action только если пассивный exit безопасен.

## Не переносить

- Required/optional semantics.
- Любое предположение, что отсутствие tap не требует branch: graph должен видеть изменение state.

## Проверка

- Safe passive exit создает zero-energy action.
- Unsafe run-from-roof interval action не создает.
- После симуляции следующий decision point строится уже как ground `Run`.
