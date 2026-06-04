# Role-based strategy: SuperJumpOn

## Цель

Перевести `SuperJumpOn` на role-based path без локального выбора "лучшего" target action.

## Текущая привязка

- `SuperJumpOnStrategy` повторяет ground `JumpOn` поток и зависит от `DecisionPointKind.GroundJumpOnTarget`.
- `SuperJumpOnPolicy` задает `BotActionKind.SuperJumpOn`, energy cost `20`, super clips и `SuperJumpOutcomeResolver`.
- Стратегия также использует `TargetRemovalPostActionSafety` и `fulfillsJumpOnObjective`.

## Role-based доработка

- Создать `SuperJumpOnStrategyNew` в `StrategiesNew/SuperJumpOn`.
- Использовать role-based `Target` из `DecisionPointNew.Chain`.
- Сохранить state/energy/window/resolver/post-action checks.
- Если обычный `JumpOn` и `SuperJumpOn` оба safe, добавить оба.
- New shared-классы делать только там, где старый контракт принимает old `ObstacleChain` или `DecisionPoint`.

## Не переносить

- `GroundJumpOnTarget` как обязательный сценарий detector'а.
- Любую предварительную дедупликацию с ordinary jump-on.

## Проверка

- Safe `SuperJumpOn` добавляется как отдельный action.
- При одинаковом target evaluator должен предпочесть `JumpOn` из-за energy cost `10` против `20`, если остальные метрики равны.
- Unsafe landing/re-entry action не добавляется.
