# Role-based strategy: JumpOnFromRoof

## Цель

Перевести `JumpOnFromRoof` на role-based path: стратегия находит road `Target` из roof-run state, доказывает safe roof-to-road jump-on и добавляет target action.

## Текущая привязка

- `JumpOnFromRoofStrategy.TryResolveActionChain` принимает только `DecisionPointKind.JumpOnFromRoofTarget`.
- Перед action восстанавливается `lastRoof` через `RoofRunProjection.TryFindLastPassiveRoof`.
- `JumpOnFromRoofSpecification` требует `RoofRun`, on roof, roof support, not shifting, energy >= `10`.
- Планирование разрешено при energy >= `40` или если автоматический сход с крыши опасен.
- После окна проверяется `TargetRemovalPostActionSafety`.

## Role-based доработка

- Создать `JumpOnFromRoofStrategyNew` в `StrategiesNew/JumpOnFromRoof`.
- Искать `Target` в `DecisionPointNew.Chain`, фактически разрешенный `CanJumpOnFromRoofObstacle`.
- `lastRoof` продолжать получать через `RoofRunProjection`, потому что это runtime-геометрия схода с крыши.
- Убрать зависимость от target-chain composer: фокусную линию уже выбрал detector, а strategy доказывает валидность target.
- `fulfillsJumpOnObjective` оставить по `JumpOnObjectiveRules`.

## Не переносить

- `JumpOnFromRoofTarget` как отдельный kind.
- Сценарный chain builder/composer для target.
- Поиск target вне focus-chain внутри стратегии; выбор focus lane делает detector.

## Проверка

- Target на focus lane создает action только при валидном fire window и safe re-entry.
- При energy < `40` action допустим только если простой сход с крыши опасен.
- Если target вне focus-chain, эта стратегия его не достраивает вручную.
