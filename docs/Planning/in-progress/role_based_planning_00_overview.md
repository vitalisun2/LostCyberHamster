# Role-based planning refactor: overview

## Цель

Перевести planning бота на role-based модель и одновременно упростить архитектуру. Это не переписывание ради новых классов, а последовательный рефакторинг текущего pipeline к более поддерживаемой схеме:

- меньше scenario-specific builders/composers;
- меньше специальных флагов `required/optional/kind`;
- больше ответственности у стратегий, которые и должны решать applicability/safety action;
- сохранение уже работающих механизмов дерева, симуляции, evaluator и executor;
- код проще расширять новой стратегией или новой ролью obstacle без добавления ещё одного специального builder.

## Принципы

- **SRP/GRASP:** detector описывает текущую planning-ситуацию; strategy создаёт safe actions; graph строит ветки; evaluator выбирает ветку; executor исполняет head-action.
- **KISS:** один decision point для одного planning node, один focus lane chain, без optional decision point коллекции.
- **DRY:** target-hunt не должен повторяться в builders/composers и стратегиях.
- **Clean Code:** новые типы добавляются только если у них есть одна ясная ответственность и они заменяют существующую сложность.
- **Без овер-инжиниринга:** не создавать `RuntimeCompositionNew`, `RuntimeNew` и другие искусственные слои, если достаточно изменить wiring в существующем `RuntimeBotController`.

## Факты по текущему коду

- `SnapshotBuilder` собирает obstacles до `VisionRightEdgeX` и сортирует по `LeftX`; `ObstacleSnapshot` уже содержит type, lane, bounds и instance id.
- `ActionGenerator.Generate` сейчас строит required decision point, затем optional jump-on decision points.
- `DecisionPointDetector` сценарный: required builders ищут roof occupant hazard, jump-on-from-roof target, current-lane ground target и blocking threat; optional builders ищут off-lane ground target и roof jump-on target.
- `DecisionPointKind`, `IsDecisionRequired`, `FireBeforeObstacle` и target composers заранее задают сценарий ситуации до стратегий.
- `PlanningGraphBuilder` уже строит дерево по узлам: берёт actions, симулирует action, создаёт child state и рекурсивно продолжает.
- `SwitchLaneSimulator` через `PlanningStateTransition.ApplyLaneSwitch` меняет lane в child `PlanningState`; следующий decision point строится уже из нового projected state.
- `JumpOnObjectiveRules.HighPriorityEnergyThreshold = 40`, target-hunt проверка сейчас использует `Energy >= 40`.
- `PlanEvaluator` уже предпочитает branches с `FulfillsJumpOnObjective`, затем сравнивает energy cost, tap count и tie-breakers.

## Что не так сейчас

- Detector/builders выбирают сценарий (`BlockingThreat`, `GroundJumpOnTarget`, `JumpOnFromRoofTarget`, `RoofJumpOnTarget`) вместо описания фактов о текущих obstacles.
- Target-hunt логика размазана между builders/composers и стратегиями.
- Optional/required split создаёт отдельные decision points, deadline через `FireBeforeObstacle` и специальные ветки в graph.
- Chain может быть искусственно расширен под конкретную цель, а не описывать ближайшую актуальную ситуацию.
- Добавление новой target/roof логики подталкивает к новому builder/composer вместо переиспользования roles + strategies + evaluator.

## Целевая схема

1. `DecisionPointsNew` строит role-based `DecisionPoint` для одной текущей ситуации: выбирает focus lane, собирает one-line chain, помечает obstacles ролями.
2. Role-based action generation передаёт один point активным стратегиям. На первом runtime-этапе активен только `SwitchLane`.
3. Стратегии сами читают roles и добавляют только safe actions.
4. `PlanningGraphBuilder` по возможности переиспользуется: нужно отвязать leaf-check от старого required detector, а не дублировать весь graph.
5. `PlanEvaluator`, `PlanningBranch`, `PlanningState`, `TransitionSimulator` и `PlanExecutor` переиспользуются, пока не появится доказанная причина менять их.
6. `RuntimeBotController` остаётся точкой сборки зависимостей; отдельный `RuntimeCompositionNew` не нужен.

## Принятая модель focus chain

Для этого refactor-плана фиксируем one-line chain:

- detector сначала выбирает focus lane;
- chain содержит только obstacles focus lane;
- obstacles другой линии участвуют только в target scan для выбора focus lane;
- после `SwitchLane` child `PlanningState` уже находится на другой lane, и следующий decision point строится заново для этой новой ситуации.

## Порядок реализации

1. Реализовать `DecisionPointsNew` без runtime hookup.
2. Адаптировать `SwitchLane` под role-based point, переиспользуя существующие calculator/simulator/executor.
3. Ввести role-based action generation без optional/required split.
4. Отвязать graph leaf-check от старого `TryDetectRequiredDecisionPoint`.
5. Адаптировать retained validation минимально для `SwitchLane`.
6. Подключить новый путь в `RuntimeBotController` только со `SwitchLane`.
7. Проверить SwitchLane-only сценарии.
8. Мигрировать остальные стратегии по одной.
9. Удалить старые scenario-specific builders/composers и временные adapters.

## Валидация

- Для plan/docs-only шага compile не требуется.
- При будущих `.cs` изменениях: обновлять `.csproj`, проверять XML summary/comments только в затронутых файлах.
- Компиляция и Unity/autoplay проверки запускаются только по явному запросу пользователя.
