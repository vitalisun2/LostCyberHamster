# Module plan: retained validation integration

## Назначение

Сохранить стабильность committed-префикса плана без переноса старой `DecisionPointKind`/required/optional логики в role-based path.

Retained validation отвечает только на вопрос: можно ли оставить уже выбранное действие из прошлого плана, или оно устарело/стало unsafe и хвост нужно пересобрать.

## Факты по текущему коду

- `PlanBuilder.ProjectCommittedPrefix` сохраняет actions старого плана и валидирует boundary-action через `RetainedActionRevalidator`.
- Старый `RetainedActionRevalidator` сам ищет `targetObstacle`, затем пытается восстановить старый `DecisionPoint` вокруг target.
- Старый revalidator содержит generic exception `CanTargetLiveOutsideDecisionChain`, потому что старая модель разделяла required blocker chain и optional/target chains.
- `SwitchLaneRetainedValidator` фактически использует `PlanningState`, projected `WorldSnapshot`, retained obstacle и action; `DecisionPoint` напрямую не читает.
- В role-based модели `DecisionPointNew` описывает одну текущую planning-ситуацию, а target используется detector'ом только для выбора focus lane.

## Целевая форма

Создать отдельный финальный new-path retained слой:

- `RetainedActionRevalidatorNew`;
- `RetainedActionContextNew`;
- `IRetainedActionValidatorNew`;
- `IPlanningStrategyNew.RetainedValidator`.

`RetainedActionRevalidatorNew` — только dispatcher и context builder:

1. Проверяет входы.
2. Проецирует snapshot через `PlanningSnapshotProjector.Project`.
3. Находит obstacle, к которому привязан retained action, по `TargetObstacleInstanceId`, затем fallback по `TargetObstacleIndex`.
4. Находит `IRetainedActionValidatorNew` по `BotActionKind`.
5. Передает context в validator стратегии.

Смысловую валидность доказывает только стратегия:

- applicability action к текущему hamster state;
- актуальность obstacle для этого action;
- role/type проверки, если они нужны стратегии;
- fire window;
- runtime outcome;
- post-action safety;
- roof support/result checks.

## Что не делать

- Не вызывать старый `DecisionPointDetector`.
- Не вызывать `DecisionPointDetectorNew` из generic revalidator.
- Не хранить `DecisionPointNew` в generic retained context по умолчанию.
- Не проверять generic membership target в chain.
- Не переносить `CanTargetLiveOutsideDecisionChain`.
- Не создавать общий сложный retained framework для future strategies сверх dispatcher/context/validator contract.
- Не смешивать old `RetainedActionContext` и `RetainedActionContextNew`.

Если стратегии для проверки нужен chain или role-based situation, она строит это внутри своего `*RetainedValidatorNew` локально и только для своего action kind.

## Первый runtime этап

SwitchLane-only:

1. Добавить `SwitchLaneRetainedValidatorNew`.
2. Использовать existing `SwitchLaneFireWindowCalculator`.
3. Проверять retained obstacle как blocking threat/action anchor, а не как target-hunt objective.
4. Проверять дорожное состояние, target lane, fire shift и safe intervals.
5. Подключить validator через `SwitchLaneStrategyNew.RetainedValidator`.

## Поведение для ещё не перенесенных strategies

Если для `action.Kind` нет `IRetainedActionValidatorNew`, `RetainedActionRevalidatorNew` возвращает `false`.

Это безопасно: action не сохраняется, хвост плана пересобирается. После миграции каждой strategy добавляется её `*RetainedValidatorNew`.

## Риски

- Нельзя сохранять action только потому, что obstacle ещё найден: валидность должна подтвердить strategy-specific validator.
- Generic revalidator не должен становиться вторым planner'ом или decision point detector'ом.
- Target-bound actions (`JumpOn*`, roof-to-road/roof-to-roof variants) переносятся вместе со своими strategies, потому что у каждого вида action разные anchors, windows и safety checks.

## Валидация будущей реализации

- `RetainedActionRevalidatorNew` не содержит ссылок на старый `DecisionPointDetector`, `DecisionPointKind`, `DecisionPoint`, `ObstacleChain`.
- `RetainedActionRevalidatorNew` не содержит generic проверки "target внутри/вне chain".
- Retained `SwitchLane` остаётся валидным, если хомяк в дорожном `Run`, retained obstacle найден, action ведёт на другую lane и fire shift всё ещё внутри safe interval.
- Retained `SwitchLane` становится invalid, если obstacle исчез, action ведёт на текущую lane, target lane unsafe или хомяк уже не в дорожном `Run`.
