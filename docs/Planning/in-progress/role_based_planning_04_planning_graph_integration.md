# Module plan: planning graph integration

## Назначение

Сохранить существующий механизм построения веток и заменить только зависимость graph от старого required detector.

## Факты по текущему коду

- `PlanningGraphBuilder.ExploreNode` ограничен `MaxSearchDepth = 6`.
- Для каждого candidate вызывается `TransitionSimulator.Simulate`, затем создаётся child node и рекурсия продолжается.
- Leaf добавляется, если candidates пусты и `HasUnresolvedRequiredDecision(...) == false`.
- Для optional interests старый graph добавляет leaf даже при наличии optional actions, если required decision не найден.
- `HasUnresolvedRequiredDecision` сейчас напрямую вызывает старый `DecisionPointDetector.TryDetectRequiredDecisionPoint`.
- Dominance использует `PlanningStateKey` как ключ state и `PlanningBranchMetrics` как стоимость branch для этого state.

## Целевая форма

На этапе миграции создать отдельный `PlanningGraphBuilderNew`, чтобы не рефакторить сложный старый graph до проверки нового path:

- `PlanningGraphBuilderNew` использует `ActionGeneratorNew`;
- `PlanningGraphBuilderNew` проверяет unresolved planning situation через `DecisionPointDetectorNew.TryDetect`;
- `TransitionSimulatorNew` использует `IPlanningStrategyNew` и не смешивается со старым `TransitionSimulator`;
- старый `PlanningGraphBuilder` и старый runtime path остаются без изменений до cleanup.

## Leaf rule в role-based модели

- Если generator не дал actions и detector не строит point для projected state, branch закрывается как safe leaf.
- Если detector строит point, но все strategies отфильтровали actions, branch не закрывается как safe leaf.
- Отдельный `required` flag в `DecisionPointNew` не вводим.
- Optional leaf behavior старого path не переносим: target-hunt становится обычным branch через focus lane, roles и evaluator.

## Что переиспользовать

- `PlanningGraphNode`.
- `PlanningBranch`.
- `PlanningBranchMetrics`.
- `PlanningStateKey`.
- `TransitionSimulatorNew` как отдельный диспетчер симуляторов для `IPlanningStrategyNew`.
- `PlanEvaluator`.

## Что не делать

- Не менять scoring.
- Не строить chain внутри graph.
- Не добавлять no-op/no-action branch-step.
- Не рефакторить старый `PlanningGraphBuilder` в этом шаге.
- Не смешивать old/new strategy contracts в одном graph или simulator.

## Валидация будущей реализации

- После `SwitchLane` следующий node строится из child `PlanningState` на новой lane.
- Unsafe/no-action point не превращается в safe empty branch.
- Existing `PlanEvaluator` выбирает branch без изменений.
