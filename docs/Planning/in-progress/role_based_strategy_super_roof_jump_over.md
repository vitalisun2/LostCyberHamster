# Role-based strategy: SuperRoofJumpOver

## Цель

Перевести `SuperRoofJumpOver` как super-вариант обработки `RoofOccupantHazard`.

## Текущая привязка

- `SuperRoofJumpOverStrategy` использует shared `RoofJumpOver` flow.
- `SuperRoofJumpOverPolicy` задает `BotActionKind.SuperRoofJumpOver`, energy cost `20`, super clips и upgrade travel.
- Runtime resolver должен подтвердить expected state и roof support.

## Role-based доработка

- Создать `SuperRoofJumpOverStrategyNew` в `StrategiesNew/SuperRoofJumpOver`.
- Читать `RoofOccupantHazard` из `DecisionPointNew.Chain`.
- Перед подключением проверить тот же detector-risk, что и для ordinary `RoofJumpOver`.
- Добавлять super action независимо от ordinary candidate.

## Не переносить

- Старый roof occupant builder.
- Фильтр против ordinary roof jump-over.

## Проверка

- Оба safe roof-jump-over action доходят до graph.
- Evaluator предпочитает ordinary при равной branch value.
- Super остается доступным, если ordinary не проходит runtime outcome.
