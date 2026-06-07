# Role-based strategy: RoofJumpOver

## Цель

Перевести `RoofJumpOver` на role-based path для hazards на текущем passive roof path.

## Текущая привязка

- `RoofJumpOverSpecification` требует `RoofRun`, on roof, roof support, not shifting, energy >= `10`.
- Первый obstacle должен подтверждаться `RoofRunProjection.TryFindDamagingOccupantOnPassiveRoofPath`.
- `RoofJumpOverFireWindowFinder` подтверждает runtime outcome и сохранение roof support.
- `RoofJumpOverSimulator` оставляет state в `RoofRun`.

## Role-based доработка

- Создать `RoofJumpOverStrategyNew` в `StrategiesNew/RoofJumpOver`.
- Использовать роль `RoofOccupantHazard`.
- Проверить миграционный риск: текущий `DecisionPointDetectorNew` стартует после passive roof chain; если это скрывает occupants на roof path, detector/chain building надо скорректировать до подключения стратегии.
- Сохранить resolver validation и проверку passive roof continuation.

## Не переносить

- `RoofOccupantHazardChainBuilder`.
- Обработку обычных road threats этой стратегией.
- Локальную конкуренцию с `JumpFromRoof`.

## Проверка

- `smallNotAliveRoadAndRoof` на passive roof path создает `RoofJumpOver`, если window safe.
- После action hamster остается в `RoofRun`.
- Если occupant не входит в passive roof path, action не создается.
