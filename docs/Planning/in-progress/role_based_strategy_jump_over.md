# Role-based strategy: JumpOver

## Цель

Перевести `JumpOver` на `IPlanningStrategyNew`: стратегия должна добавлять обычный ground jump-over action, если первый релевантный `BlockingThreat` в role-based chain можно безопасно перепрыгнуть.

## Текущая привязка

- `JumpOverStrategy` использует старый `DecisionPoint` и `decisionPoint.Chain.FirstObstacle`.
- `JumpOverSpecification` требует: hamster не на крыше, не shifting, энергии не меньше `10`, первый obstacle на текущей линии и разрешен `JumpOverPolicy.CanJumpOverObstacle`.
- `JumpOverFireWindowFinder` и runtime resolver подтверждают окно прыжка.
- `JumpOverSimulator` уже возвращает `Run` через `PlanningStateTransition.ApplyRunAfterOver`.

## Role-based доработка

- Создать `JumpOverStrategyNew` в `StrategiesNew/JumpOver`.
- Новый specification должен читать `DecisionPointNew.Chain.First` или первый `BlockingThreat` focus-chain.
- Оставить policy/executor/simulator, если их контракт не меняется.
- Для shared-классов, где входом остается старый `ObstacleChain`, создать `*New` версию под `ObstacleChainNew`.
- Если action безопасен, добавить его в список; не сравнивать с `SuperJumpOver`.

## Не переносить

- Старую зависимость от `Planning.DecisionPoints.DecisionPoint`.
- Локальные фильтры "обычный прыжок покрывает super".
- Любое ранжирование: стоимость `10` должна учитываться evaluator'ом.

## Проверка

- `smallAlive/smallNotAliveRoad/smallNotAliveRoadAndRoof` на дороге создают `JumpOver`, если окно и resolver валидны.
- `bigAlive` не создает обычный `JumpOver`, потому что policy его не разрешает.
- После симуляции следующий decision point строится из нового `PlanningState`.
