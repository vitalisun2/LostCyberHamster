# Module plan: retained validation integration

## Назначение

Перевести retained validation на role-based context без дублирования strategy logic и без зависимости от старого `DecisionPointDetector`.

## Факты по текущему коду

- `RetainedActionRevalidator` напрямую создаёт старый `DecisionPointDetector`.
- Revalidator сначала ищет target obstacle по `TargetObstacleInstanceId`, затем fallback'ится на `TargetObstacleIndex`.
- Затем он пытается найти старый decision point containing retained target.
- Если target decision point не найден, он fallback'ится на старый required decision point.
- Если target не входит в required chain, target-bound actions разрешаются через `CanTargetLiveOutsideDecisionChain`.
- Старый `RetainedActionContext` хранит старый `DecisionPoint`.
- `SwitchLaneRetainedValidator` использует planning state, projected snapshot, target obstacle и action; старый `DecisionPoint` напрямую не читает.

## Целевая форма

Минимальный переход:

- Новый retained context должен содержать `PlanningState`, projected `WorldSnapshot`, `DecisionPointNew`, `TargetObstacle`, `TargetObstacleIndex`, `PlannedAction`.
- Новый revalidator не должен вызывать старый `DecisionPointDetector`.
- Для SwitchLane можно переиспользовать существующую проверочную логику через adapter/helper, потому что она не зависит от `DecisionPointKind`.
- Для target-bound actions сохранить идею из текущего кода: target может жить вне первого blocker chain, а финальную валидность должна доказать стратегия.

## Первый runtime этап

Для SwitchLane-only этапа:

1. Спроецировать snapshot через `PlanningSnapshotProjector`.
2. Найти retained target obstacle по instance id/index.
3. Построить `DecisionPointNew` для текущей projected situation.
4. Проверить, что action kind имеет validator.
5. Проверить fire-window и target-lane safety через SwitchLane retained logic.

## Что не делать

- Не создавать общий сложный retained framework заранее для всех future target actions.
- Не смешивать старый `RetainedActionContext` с `DecisionPointNew`.
- Не вызывать старый detector из нового path.
- Не переносить все old retained validators до миграции соответствующих strategies.

## Риски

- Target-bound actions требуют отдельного переноса вместе с `JumpOn*` strategies, иначе можно преждевременно зафиксировать неверный generic rule.
- In-progress head-action остаётся ответственностью `PlanBuilder`/`ActionInProgressProjector`; retained validation проверяет только boundary actions.

## Валидация будущей реализации

- Retained `SwitchLane` остаётся валидным, если target obstacle найден и fire shift всё ещё внутри safe interval.
- Retained `SwitchLane` становится invalid, если target obstacle исчез, action ведёт на текущую lane или target lane unsafe.
