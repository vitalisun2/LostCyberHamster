# Module plan: role-based strategies

## Назначение

Перевести стратегии с `DecisionPointKind` и scenario-specific chains на role-based `DecisionPointNew`. Стратегия должна читать роли obstacle, проверять применимость action и добавлять только safe candidates.

## Факты по текущему коду

- `IPlanningStrategy.CollectActions(...)` принимает старый `DecisionPoint`.
- Все стратегии сейчас завязаны на namespace `Planning.DecisionPoints`.
- `SwitchLaneStrategy` использует `SwitchLaneSpecification`, которая берёт `decisionPoint.Chain.FirstObstacle` и проверяет `ObstacleClassifier.DamagesOnGroundContact`.
- `SwitchLaneFireWindowCalculator` уже считает safe intervals по всем damaging obstacles target lane, а не только по chain.
- `SwitchLaneSimulator` уже меняет lane в `PlanningState` и продвигает `NextObstacleIndex`.
- `SwitchLaneRetainedValidator` не использует `context.DecisionPoint` напрямую, но его contract принимает старый `RetainedActionContext`.
- `RoofSwitchLaneExitStrategy` уже отдельная стратегия; концептуально её оставляем отдельной, потому что switch с roof-exit и ground/roof switch имеют разные условия.

## Целевая форма

Минимальная цель - не переписать стратегии, а заменить источник ситуации:

- strategy получает `DecisionPointNew`;
- strategy выбирает подходящий `ObstacleChainElementNew` по role;
- existing calculators/simulators/executors переиспользуются, если их contract не тянет старый `DecisionPoint`;
- если старый class можно адаптировать маленьким overload'ом, не создавать новый класс-дубль.

Возможный переходный contract:

- `IPlanningStrategyNew` только если нельзя безопасно изменить старый `IPlanningStrategy` без поломки активного path.
- После полной миграции оставить один нейтральный `IPlanningStrategy`, а `New`-контракты удалить.

## Первый action: SwitchLane

Минимальная адаптация:

1. Добавить role-based вход в `SwitchLaneSpecification`: искать первый `BlockingThreat` в focus chain.
2. Оставить state guard: `Run` или `RoofRun`, `!IsShifting`.
3. Target lane = противоположная текущей lane хомяка.
4. Переиспользовать `SwitchLaneFireWindowCalculator`.
5. Переиспользовать `SwitchLaneSimulator`.
6. Переиспользовать `SwitchLaneExecutor`.
7. Retained validation: переиспользовать проверочную логику, но не старый `IRetainedActionValidator` напрямую, пока он принимает старый `RetainedActionContext`.
8. Убрать зависимость от `DecisionPoint.UsesObjectiveSwitchLaneTiming` и `FireBeforeObstacle`.

Sampling policy для первого этапа:

- energy `>= 40`: пробовать `EarlyWindowSelectionRatio` и `MidWindowSelectionRatio`, как текущий high-energy branch;
- energy `< 40`: пробовать `MidWindowSelectionRatio`;
- optional-only ранний deadline не переносить, потому что optional decision point исчезает.

## Последующая миграция стратегий

Порядок после SwitchLane-only проверки:

1. `JumpOver` / `SuperJumpOver`: `BlockingThreat` + существующие jump-over safety/window проверки.
2. `JumpOnRoof` / `SuperJumpOnRoof`: `RoofSupport` + roof landing safety.
3. `PassiveRoofExit`: roof state + passive roof path; нулевой planning-step остаётся отдельной стратегией, если без него graph теряет корректный state transition.
4. `JumpOn` / `SuperJumpOn`: huntable ground `Target` + post-action safety.
5. `JumpOnFromRoof` / `SuperJumpOnFromRoof`: roof-exit huntable `Target` + bounce/re-entry safety.
6. `JumpFromRoof*`, `RoofJumpOver*`: перенос после roof-chain правил.
7. `RoofSwitchLaneExit`: адаптировать к new point contract без слияния с обычным `SwitchLane`.

## Что не делать

- Не переносить scenario-specific target-chain построение внутрь стратегий.
- Не создавать параллельный полный набор стратегий, если можно сделать небольшой overload/adapter.
- Не менять evaluator.
- Не решать focus lane внутри стратегии.
- Не подключать все стратегии сразу.

## Валидация будущей реализации

- `SwitchLane` создаёт action только от `BlockingThreat`.
- Unsafe target lane не создаёт action.
- Roof switch проходит только при target-lane roof support.
- После action child state находится на другой lane.
