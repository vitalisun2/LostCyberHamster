# Role-based strategy: JumpFromRoof

## Цель

Перевести `JumpFromRoof` на role-based path: стратегия добавляет обычный прыжок с крыши на дорогу, когда автоматический сход с крыши приведет к столкновению с `BlockingThreat`.

## Текущая привязка

- `JumpFromRoofSpecification` требует `RoofRun`, on roof, roof support, not shifting, energy >= `10`.
- Первый obstacle старого decision chain должен быть same-lane damaging road obstacle и не roof.
- Roof occupant hazard явно не перехватывается: это ответственность `RoofJumpOver`.
- `lastRoof` берется из `RoofRunProjection.TryFindLastPassiveRoof`.
- Если gap до threat >= `RunFromRoofTravel`, action не планируется.

## Role-based доработка

- Создать `JumpFromRoofStrategyNew` в `StrategiesNew/JumpFromRoof`.
- Использовать первый `BlockingThreat` в `DecisionPointNew.Chain`, исключая `RoofSupport` и `RoofOccupantHazard`.
- Сохранить проверку опасности автоматического roof exit через gap и `RunFromRoofTravel`.
- Создать new shared specification/finder/retained validator, если старые принимают old `DecisionPoint` или `ObstacleChain`.

## Не переносить

- Зависимость от `IsDecisionRequired`.
- Перехват roof occupant hazards.
- Локальный выбор между `JumpFromRoof`, `SuperJumpFromRoof`, `PassiveRoofExit`, `RoofSwitchLaneExit`.

## Проверка

- Если простой сход безопасен, action не создается.
- Если сход попадает в ближайший road threat, создается safe jump action.
- Roof occupant на passive roof path должен уходить в roof-jump-over strategy.
