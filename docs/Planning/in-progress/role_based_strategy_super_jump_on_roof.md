# Role-based strategy: SuperJumpOnRoof

## Цель

Перевести `SuperJumpOnRoof` на role-based model как отдельный safe action-кандидат для посадки на `RoofSupport`.

## Текущая привязка

- `SuperJumpOnRoofStrategy` использует тот же shared finder, что `JumpOnRoof`.
- `SuperJumpOnRoofPolicy` задает `BotActionKind.SuperJumpOnRoof`, energy cost `20`, super clip и super resolver.
- Стратегия создает action только если landing window и runtime outcome совпадают.

## Role-based доработка

- Создать `SuperJumpOnRoofStrategyNew` в `StrategiesNew/SuperJumpOnRoof`.
- Использовать `RoofSupport` role из `DecisionPointNew.Chain`.
- Сохранить отдельную policy, executor, simulator и retained validator под new contract.
- Добавлять super action независимо от обычного `JumpOnRoof`, если он safe.

## Не переносить

- Optional roof target point.
- Фильтр "обычный roof jump покрывает super".

## Проверка

- Когда ordinary и super посадки обе safe, оба action остаются в candidates.
- При равной branch value evaluator предпочитает cheaper ordinary action.
- Если только super resolver подтверждает посадку, остается super branch.
