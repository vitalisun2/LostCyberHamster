# Role-based strategy: JumpOn

## Цель

Перевести ground `JumpOn` на role-based model: стратегия должна находить `Target` в focus-chain, доказывать безопасное напрыгивание и добавлять target action.

## Текущая привязка

- `JumpOnStrategy.TryResolveActionChain` принимает только `DecisionPointKind.GroundJumpOnTarget`.
- `JumpOnSpecification` ищет первый ground jump-on target через старый chain helper.
- `JumpOnFireWindowFinder` подтверждает окно runtime resolver'ом.
- После окна стратегия вызывает `TargetRemovalPostActionSafety.IsSafeAfterCompletion`.
- `PlannedAction` помечается `fulfillsJumpOnObjective`, если энергия hamster >= `JumpOnObjectiveRules.HighPriorityEnergyThreshold` (`40`).

## Role-based доработка

- Создать `JumpOnStrategyNew` в `StrategiesNew/JumpOn`.
- Искать первый element с ролью `Target`, который фактически разрешен `CanJumpOnGroundObstacle`.
- Сохранить gates: hamster в road run, не shifting, energy >= `10`.
- Создать new shared-варианты для specification/window/retained validator, если им нужен `ObstacleChainNew`.
- Сохранять `TargetRemovalPostActionSafety`: стратегия отвечает за доказательство полной safety action.

## Не переносить

- `DecisionPointKind.GroundJumpOnTarget`.
- Специальные ground target chain builders/composers.
- Фильтрацию super/ordinary: evaluator сравнит target value и energy.

## Проверка

- `smallAlive` в focus-chain создает `JumpOn`, если окно и post-action safety валидны.
- Если после отскока есть столкновение, action не добавляется.
- Branch с target получает objective priority через `fulfillsJumpOnObjective`.
