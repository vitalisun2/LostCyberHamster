# Collectible Graph Regression Analysis — 2026-06-18

## Scope

Один регресс: после изменения `ObstacleRoleClassifier`, где collectable стал попадать в graph как факт типа obstacle, уровень `01_New_York/Morning/test_collectables` начал падать.

Expected: при `test_collectables_02` бот выбирает безопасную ветку через `SuperJumpOver` и не теряет жизнь. Если какая-то ветка ведёт к dead-end, она может быть fallback только при полном отсутствии successful branches.

Actual: бот выбрал план `SwitchLane`, перешёл на нижнюю линию, подобрал `collectablePizza` при `energy=100`, затем получил damage от нижней цепочки `smallNotAliveRoadAndRoof`, и automation завершилась `[TEST RESULT] FAIL`.

Safety invariant: безопасность выше целей и collectables. Ветка с прогнозируемой потерей жизни не является successful branch. Dead-end branch допустима только как диагностический fallback, если безопасных веток нет вообще; тогда это либо ошибка бота, либо реально непроходимая геометрия уровня.

## Sources

- Лог: `LostCyberHamster/EditorLogs/diagnostic_log.txt`, запуск `2026-06-18 21:38:42`.
- Уровень: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_collectables/test_collectables.json`.
- Паттерны: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.
- Graph/planner:
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningGraphBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/DecisionPointDetector.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/ObstacleChainBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/ObstacleRoleClassifier.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/ObstacleChainElement.cs`
- Strategies/transitions:
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneSimulator.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/PassiveCollect/PassiveCollectPlanner.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/PassiveCollect/PassiveCollectSimulator.cs`

## Hypotheses

### H1 — Dead-end fallback incorrectly beats successful branch

What confirms: `PlanBuilder` compares dead-end branches against successful branches or can return dead-end fallback while successful branches exist.

What rejects: `PlanBuilder` returns successful branch first whenever `graphResult.Branches` has at least one candidate.

Status: rejected by code. `PlanBuilder.Build()` selects `bestBranch = SelectBest(graphResult.Branches)` and immediately returns successful result if `bestBranch != null`. Dead-end fallback is used only after that.

### H2 — The chosen `SwitchLane` branch was classified as successful, not dead-end

What confirms: runtime activated `SwitchLane` without pending dead-end report until later spawn/life-loss, and `PlanBuilder` cannot choose dead-end if successful branches exist.

What rejects: log would show dead-end report attached to the original `SwitchLane` plan before executing it.

Status: supported. The log shows `[Bot PLAN] SwitchLane`, then the bot executes it, later damage confirms dead-end on `SpawnPattern`. Because `PlanBuilder` chooses dead-end fallback only when no successful branch exists, this implies `SwitchLane` was produced as a successful branch at initial selection.

### H3 — Making all collectables active roles allowed optional collectable-only progress to hide a required threat behind it

What confirms: after `SwitchLane`, the current-lane decision point can be a collectable with `Collectible` role, no required role; `PlanningGraphBuilder.HasUnresolvedPlanningSituation()` treats chains without required roles as resolved; `PassiveCollect` can remove/pick the optional collectable and move projection forward, while a later required threat remains outside the checked unresolved situation.

What rejects: graph checks for required threats beyond optional collectable-only decision points before accepting leaf branches.

Status: confirmed by code and log.

Evidence:

- `ObstacleRoleClassifier` now adds `ObstacleRole.Collectible` by type fact for every collectable.
- `ObstacleChainBuilder.TryFindFirstActiveElement()` stops at the nearest active role element on the focus lane.
- `PlanningGraphBuilder.HasUnresolvedPlanningSituation()` calls `DecisionPointDetector.TryDetect()` once and then checks only `decisionPoint.Chain.HasAnyRequiredPlanningRole()`.
- `ObstacleChainElement.HasAnyRequiredPlanningRole` explicitly ignores pure `Collectible`.
- `PlanningGraphBuilder.ExploreNode()` adds a successful leaf when `hasUnresolvedPlanningSituation == false`.
- Runtime log shows the chosen single-action branch:
  - `[Bot PLAN] SwitchLane`
  - then bottom-lane `collectablePizza` collected with `Energy delta=0`
  - then damage from bottom-lane `smallNotAliveRoadAndRoof`
- Temporary diagnostic run `2026-06-18 21:50:07` directly confirms the premature leaf:
  - `[Bot TEMP_GRAPH] leaf reason=candidates_no_required depth=1 ... lane=bottom actions=SwitchLane nearest=collectablePizza roles=Collectible required=False`
  - `[Bot TEMP_GRAPH] leaf reason=no_actions_no_required depth=1 ... lane=top actions=SuperJumpOver nearest=none`
  - `[Bot PLAN] SwitchLane`

Interpretation: after `SwitchLane`, the nearest bottom-lane active element was an optional collectable. Because it had no required role, the graph treated the state as resolved and accepted `SwitchLane` as successful leaf, without looking past that optional collectable to the lower-lane threats.

### H4 — `SuperJumpOver` branch disappeared after collectable-as-fact because collectable extended an over-chain and invalidated jump geometry

What confirms: top current-line chain at `test_collectables_02` includes a collectable after the threat, causing `SuperJumpOverStrategy` to return no action.

What rejects: log/code evidence that `SuperJumpOver` action is still generated as a successful branch, but loses to `SwitchLane`.

Status: not required for root cause. Even if `SuperJumpOver` was generated, `PlanEvaluator` would prefer a supposedly successful `SwitchLane` branch because objective priority is equal and `SwitchLane` has lower energy cost. The critical bug is that unsafe `SwitchLane` was allowed into successful branches at all.

## Facts

- `test_collectables_02` contains a top-line `bigAlive` and bottom-line chain: three `smallNotAliveRoadAndRoof` plus a bottom-line `collectablePizza`.
- Regression log:
  - `21:38:51.153` pattern `test_collectables_02` spawned.
  - `21:38:51.165` plan activated: `SwitchLane`.
  - `21:38:53.153` bot collected `collectablePizza` on bottom with `energy=100`; economy delta was `0`.
  - `21:38:54.495` bot took damage from `smallNotAliveRoadAndRoof`.
  - `21:38:55.516` dead-end confirmed only later, reason `SpawnPattern`.
- `PlanBuilder` does not choose dead-end fallback while any successful branch exists.
- `ObstacleChainElement.HasAnyRequiredPlanningRole` treats `Collectible` as non-required.
- Current changed `ObstacleRoleClassifier` adds `ObstacleRole.Collectible` for every collectable type, regardless of current `CollectibleValuePolicy` value.
- `PlanEvaluator.CompareBranches()` prefers objective priority first, then lower energy cost, then lower tap count. A false-successful `SwitchLane` with no energy cost can therefore beat `SuperJumpOver`.

## Current Root-Cause State

Root cause proven.

The regression is caused by changing collectable role assignment from "positive-value collectable" to "any collectable type" without changing graph termination semantics.

Before the change, a zero-value energy collectable at `energy=100` was invisible to the decision graph, so detection continued to required threats behind it. After the change, the zero-value energy collectable became the nearest active decision point. Because pure collectables are optional, `HasUnresolvedPlanningSituation()` classified that state as resolved and allowed the current branch to become successful leaf. That successful leaf was `SwitchLane`, which was cheaper than `SuperJumpOver`, so evaluator selected it. In runtime, this branch led to lower-lane hazards and life loss.

This is not a dead-end fallback priority bug. It is a graph completeness bug: optional collectable-only decision points can prematurely terminate branch safety analysis and hide required threats behind them.

## Implemented Solution

The fix preserves the intended design "collectable as graph fact", but graph termination now skips through optional-only collectable situations when checking whether a branch is safe to accept as successful.

Implemented direction:

- Do not make pure collectable chains required.
- Do not let pure collectable chains by themselves mark a branch as fully resolved.
- For branch completion safety, look past optional-only collectable decision points until either:
  - a chain with required role is found, meaning unresolved required situation exists;
  - no active planning role remains, meaning the branch can be a successful leaf.

This keeps collectables visible to `PassiveCollect` for value evaluation, but prevents optional zero-value collectables from hiding life-threatening obstacles behind them.

Implementation shape:

- Keep `ObstacleRoleClassifier` factual: every collectable still gets `ObstacleRole.Collectible`.
- `PlanningGraphBuilder.HasUnresolvedPlanningSituation()` now answers "is there any required planning situation ahead?" rather than only checking the nearest decision point.
- The helper advances past pure optional collectable chains by moving the scan start to the obstacle after that chain, without mutating state and without marking collectables as removed.
- `ExploreNode` adds a successful leaf only when this helper proves there is no required situation ahead.
- Temporary `[Bot TEMP_GRAPH]` diagnostics were not left in source code; they remain only in this document as evidence from the investigation run.
- `PassiveAdvance` was added as a no-input planning action so a branch can safely wait for an opposite-lane blocker to pass, then continue to a later `SwitchLane`.

## Verification

- `dotnet build LostCyberHamster/Assembly-CSharp.csproj` succeeded with existing project warnings and no errors.
- `tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_collectables' -TimeoutSeconds 180` succeeded with `[TEST RESULT] WIN`.
- BOT log confirmed the original regression path is fixed:
  - previous unsafe bare `SwitchLane` was not selected;
  - disputed second-pattern plan became `SuperJumpOver -> JumpOver -> PassiveAdvance -> SwitchLane -> PassiveCollect[Energy:30]`;
  - `PassiveAdvance` fired and completed before the energy `SwitchLane`;
  - energy, coin, life, crystal priorities were exercised in the test-level run.
- Full test-level suite was intentionally not run after user direction on 2026-06-18.
