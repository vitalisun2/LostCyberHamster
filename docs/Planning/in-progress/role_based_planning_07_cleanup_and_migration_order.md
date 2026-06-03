# Module plan: cleanup and migration order

## Назначение

Зафиксировать порядок удаления старой scenario-specific архитектуры после проверки role-based path. Cleanup - обязательная часть цели: иначе временные `New`-сущности сами станут овер-инжинирингом.

## Удалять только после полной миграции

- `DecisionPointKind`.
- Старый `DecisionPointDetector`.
- Старые scenario-specific chain builders:
  - `BlockingThreatChainBuilder`;
  - `GroundJumpOnTargetChainBuilder`;
  - `JumpOnFromRoofTargetChainBuilder`;
  - `RoofJumpOnTargetChainBuilder`;
  - `RoofOccupantHazardChainBuilder`.
- Старые target composers:
  - `GroundJumpOnTargetChainComposer`;
  - `JumpOnFromRoofTargetChainComposer`.
- Compatibility properties старого `DecisionPoint`.
- Временные `*New` contracts/adapters, если после миграции можно вернуть нейтральные имена.

## Оставить и переиспользовать

- `ObstacleClassifier` как factual source для role classifier.
- `RoofRunProjection`, если roof/passive continuation rules нужны detector/strategies.
- `PlanningState`, `PlanningStateTransition`, `PlanningSnapshotProjector`.
- `PlanningGraphNode`, `PlanningBranch`, `PlanningBranchMetrics`, `PlanningStateKey`.
- `TransitionSimulator`, `ActionInProgressProjector`, `PlanEvaluator`, `PlanExecutor`, если role-based contracts можно подключить без дублирования.

## Миграционный порядок

1. `DecisionPointsNew`.
2. Role-based `SwitchLane` adaptation.
3. Role-based action generation.
4. Planning graph integration через замену old required leaf-check.
5. Retained validation integration для `SwitchLane`.
6. `RuntimeBotController` wiring только со `SwitchLane`.
7. Ручная проверка SwitchLane-only сценариев.
8. Последовательная миграция остальных strategies.
9. Удаление старых builders/composers/contracts.
10. Финальное переименование временных `New` сущностей, где это безопасно.
11. Перенос финальной архитектуры из in-progress/brainstorm в `docs/architecture_knowledge_base.md`.

## Условия для cleanup

- `rg "Planning.DecisionPoints"` не находит active role-based code.
- Все active strategies используют role-based point contract.
- Runtime path покрывает все нужные `BotActionKind`.
- Пользователь подтвердил ручную проверку целевых уровней.

## Валидация будущего cleanup

- `git diff --check`.
- Проверка `.csproj` после удаления/переименования `.cs` файлов.
- Компиляция по явному запросу пользователя.
- Ручная Unity-проверка пользователем.
