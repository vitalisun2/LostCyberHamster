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

Не дублировать весь `PlanningGraphBuilder`, если можно заменить зависимость:

- graph должен получать генератор actions и predicate "есть ли unresolved planning situation";
- old path может передавать старый required detector;
- role-based path передаёт `DecisionPointDetectorNew.TryDetect`.

Возможные минимальные реализации:

- маленький internal interface для action generator + situation detector;
- или constructor overload/factory, если интерфейс окажется лишним.

Критерий выбора: меньше дублирования и меньше новых сущностей при сохранении понятного ownership.

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
- `TransitionSimulator`.
- `PlanEvaluator`.

## Что не делать

- Не менять scoring.
- Не строить chain внутри graph.
- Не добавлять no-op/no-action branch-step.
- Не создавать `PlanningGraphNew`, если достаточно отвязать существующий graph от конкретного detector.

## Валидация будущей реализации

- После `SwitchLane` следующий node строится из child `PlanningState` на новой lane.
- Unsafe/no-action point не превращается в safe empty branch.
- Existing `PlanEvaluator` выбирает branch без изменений.
