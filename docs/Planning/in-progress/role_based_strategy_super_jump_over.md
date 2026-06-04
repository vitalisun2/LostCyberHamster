# Role-based strategy: SuperJumpOver

## Цель

Перевести `SuperJumpOver` на role-based path: стратегия добавляет super ground jump-over action для safe blocking threat, если обычный или другой action не является ее ответственностью.

## Текущая привязка

- `SuperJumpOverStrategy` использует тот же shared-поток, что и `JumpOver`.
- `SuperJumpOverPolicy` задает `BotActionKind.SuperJumpOver`, energy cost `20`, `transform_super_jump`, upgrade delay и свой resolver behavior.
- `CanSuperJumpOverOnGround` шире обычного jump-over и включает `bigAlive`.

## Role-based доработка

- Создать `SuperJumpOverStrategyNew` в `StrategiesNew/SuperJumpOver`.
- Использовать `DecisionPointNew.Chain` и роль `BlockingThreat`.
- Перенести/создать `JumpOverSpecificationNew` и `JumpOverFireWindowFinderNew`, если старые требуют старый `ObstacleChain`.
- Добавлять action только после policy/window/resolver validation.
- Не отбрасывать action из-за наличия `JumpOver`: evaluator сам выберет более дешевую ветку, если обе safe.

## Не переносить

- Любую предвыборку против обычного jump-over.
- Старые contracts на `DecisionPoint`.

## Проверка

- `bigAlive` на дороге может породить `SuperJumpOver`, если окно безопасно.
- Для obstacle, где обычный и super оба safe, оба action доходят до graph.
- Evaluator выбирает дешевую ветку, если branch value одинаковый.
