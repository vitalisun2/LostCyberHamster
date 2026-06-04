# Role-based strategy: SuperJumpOnFromRoof

## Цель

Перевести `SuperJumpOnFromRoof` на role-based path как отдельный roof-to-road target action с super timing.

## Текущая привязка

- `SuperJumpOnFromRoofStrategy` повторяет `JumpOnFromRoof` поток и зависит от `DecisionPointKind.JumpOnFromRoofTarget`.
- `SuperJumpOnFromRoofPolicy` задает `BotActionKind.SuperJumpOnFromRoof`, energy cost `20`, super clips и upgrade travel.
- `TargetRemovalPostActionSafety` проверяет безопасность после уничтожения target.

## Role-based доработка

- Создать `SuperJumpOnFromRoofStrategyNew` в `StrategiesNew/SuperJumpOnFromRoof`.
- Работать с ролью `Target` в `DecisionPointNew.Chain` и проверкой `CanJumpOnFromRoofObstacle`.
- Сохранить `RoofRunProjection.TryFindLastPassiveRoof`, super travel, resolver и post-action safety.
- Добавлять super action вместе с ordinary, если оба safe.

## Не переносить

- Старый target-chain composer.
- Любой фильтр против ordinary `JumpOnFromRoof`.

## Проверка

- При одинаковом target evaluator выбирает cheaper ordinary branch, если обе ветки равноценны.
- Если ordinary не проходит runtime outcome, но super проходит, super branch остается доступной.
- `fulfillsJumpOnObjective` выставляется так же, как в ordinary target actions.
