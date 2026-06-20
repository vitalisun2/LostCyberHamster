# New York smallNotAlive JumpOver regression analysis

Дата: 2026-06-20

## Scope

Один регресс: начало `01_New_York/Morning/level_01`, первый `easy_run`.

Expected: после первого `SwitchLane` с верхней линии на нижнюю, бот должен бесплатно обойти нижний `smallNotAliveRoad`: `SwitchLane` обратно на верхнюю пустую линию, затем вернуться вниз для дальнейшего `JumpOn`, если он нужен.

Actual: бот выбирает `JumpOver` через нижний `smallNotAliveRoad`, тратит 10 энергии без смысловой необходимости.

Out of scope для этого документа: последующие повреждения на `bigAlive` после второго `small_jumps`.

## Sources

- Runtime log: `LostCyberHamster/EditorLogs/diagnostic_log.txt`.
- Level asset: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/level_01/level_01.json`.
- Pattern asset: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.
- Planner code:
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningGraphBuilder.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneFireWindowCalculator.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/Simulation/PlanningStateTransition.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/DecisionPointDetector.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/ObstacleChainBuilder.cs`
- Test infrastructure: `tools/invoke_open_unity_test_level.ps1`.

## Hypotheses

### H1: Switch-back action is not generated

Meaning: after simulating the first `SwitchLane` to bottom lane, `ActionGenerator` / `SwitchLaneStrategy` does not produce a valid `SwitchLane` back to the top lane before bottom `smallNotAliveRoad`.

Would confirm:
- Diagnostic log contains no `early_switch_back status=generated`.
- Code/log shows `SwitchLaneStrategy` rejected the action due to detection, specification, or fire-window.

Would refute:
- Diagnostic log contains `early_switch_back status=generated`.

Status: refuted. Diagnostic log shows `early_switch_back status=generated`.

### H2: Switch-back action is generated but pruned by graph builder

Meaning: the valid `SwitchLane -> SwitchLane` route exists as a candidate, but `PlanningGraphBuilder` drops it via redundant-switch, ancestor-cycle, null-state, or dominance pruning.

Would confirm:
- Diagnostic log contains `early_switch_back status=generated` followed by `pruned_*`.

Would refute:
- Diagnostic log contains `early_switch_back status=accepted`, or no generated candidate exists.

Status: refuted for the first regression. Diagnostic log shows the relevant depth=1 candidate was `accepted`.

### H3: Switch-back branch reaches leaf but evaluator still chooses JumpOver

Meaning: both full branches exist, but `PlanEvaluator` chooses the energy-spending branch incorrectly.

Would confirm:
- Candidate branch log shows both:
  - `SwitchLane -> JumpOver...`
  - `SwitchLane -> SwitchLane...`
- The selected `best` branch is the `JumpOver` one despite equal or better objective metrics for the switch-back branch.

Would refute:
- Switch-back branch is absent before evaluator comparison.

Status: confirmed in narrowed form. The switch-back branch reaches comparison, but loses because fixed action-depth horizon gives it lower `MajorObjectiveCount`.

### H4: Post-completion committed prefix preserves an already bad initial choice

Meaning: after the first `SwitchLane` completes, the replan log `JumpOver -> ...` is not a fresh decision; it is retained from the initial plan because committed prefix keeps the next action.

Would confirm:
- Initial plan already contains `SwitchLane -> JumpOver -> ...`.
- After first `SwitchLane` completion, the plan starts with `JumpOver`.

Would refute:
- Initial plan had a good second action, but post-completion replan replaced it with `JumpOver`.

Status: confirmed. Initial plan already contains the bad second action, and post-completion replan preserves it through committed prefix.

## Facts

- Runtime log shows level start with two spawned `easy_run` patterns after lookahead change:
  - `[Bot PATTERN] SPAWN patternIndex=0 pattern=easy_run`
  - `[Bot PATTERN] SPAWN patternIndex=1 pattern=easy_run`
- Runtime log shows initial plan:
  - `[Bot PLAN] SwitchLane -> JumpOver -> JumpOn -> JumpOver -> JumpOn`
- After the first `SwitchLane` completes, runtime log shows:
  - `[Bot PLAN] JumpOver -> JumpOn -> SwitchLane -> PassiveCollect[Coin:1] -> SwitchLane -> JumpOn`
- Runtime log shows actual fire:
  - `[Bot EXEC] FIRE kind=JumpOver ... desc=Jump over smallNotAliveRoad`
  - `[Energy] spent amount=10`
- `easy_run` geometry:
  - top lane has first `bigAlive` around x=8, coin around x=28.2, next `bigAlive` around x=46.6.
  - bottom lane has `smallNotAliveRoad` around x=22.2 and later `smallAlive` around x=61.6.
- `RuntimeBotController.BuildCommittedPrefix` retains two actions from the existing plan.
- Targeted diagnostic run:
  - `dotnet build LostCyberHamster/Assembly-CSharp.csproj` succeeded with existing warnings and no errors.
  - `tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 120 -TimeScale 1` reached `[TEST RESULT] FAIL` later in the level; first-regression logs were collected before the later fail.
- Diagnostic log for the first regression:
  - `early_switch_back status=generated depth=1 ... prev=SwitchLane(... desc=Switch lane before bigAlive) candidate=SwitchLane(... desc=Switch lane before smallNotAliveRoad)`
  - `early_switch_back status=accepted depth=1 ... candidate=SwitchLane(... desc=Switch lane before smallNotAliveRoad)`
  - Candidate comparison:
    - `best=major=2 energyCost=40 coin=0 taps=1 actions=5 chain=SwitchLane -> JumpOver -> JumpOn -> JumpOver -> JumpOn`
    - valid switch-back candidates exist, for example:
      - `major=1 energyCost=10 coin=1 taps=4 actions=6 chain=SwitchLane -> SwitchLane -> PassiveCollect[Coin:1] -> SwitchLane -> JumpOn -> SwitchLane`
      - `major=1 energyCost=10 coin=0 taps=4 actions=6 chain=SwitchLane -> SwitchLane -> PassiveAdvance -> SwitchLane -> JumpOn -> SwitchLane`
- No logged `SwitchLane -> SwitchLane` candidate for the first regression reached `major=2` within the current `MaxSearchDepth=6`.
- `PlanningGraphBuilder` stops expansion when `currentNode.Depth >= MaxSearchDepth`.
- Before the evaluator change, `PlanningBranchMetrics.CompareObjectivePriority` compared `LifeCollectibleValue`, then `MajorObjectiveCount`; energy was compared only after objective priority.
- Before the evaluator change, `PlanEvaluator.CompareBranches` therefore chose higher `MajorObjectiveCount` before energy cost.

## Hypothesis Status

- H1 refuted. The switch-back action is generated.
- H2 refuted for the first regression. The relevant depth=1 switch-back action is accepted, not pruned.
- H3 confirmed in a narrower form. The switch-back branch reaches candidate comparison, but loses because it has lower `MajorObjectiveCount` inside the fixed action-depth horizon.
- H4 confirmed. The initial plan already contains `SwitchLane -> JumpOver -> ...`; after the first `SwitchLane` completes, the next `JumpOver` is preserved by committed prefix rather than newly chosen from a clean state.

## Temporary Diagnostics

Added targeted diagnostics for one run:

- `PlanBuilder`: logs early branches where first action is `SwitchLane` before `bigAlive` and second action is either `JumpOver` or `SwitchLane` before `smallNotAliveRoad`.
- `PlanningGraphBuilder`: logs `generated / pruned_* / accepted` only for `SwitchLane` back before `smallNotAliveRoad` immediately after a `SwitchLane` before `bigAlive`.

Status: diagnostics were temporary and removed after analysis.

## Root Cause

The root cause is an unfair branch horizon, not invalid level geometry and not missing `SwitchLane` capability.

The valid energy-free detour branch is built:

`SwitchLane -> SwitchLane -> ... -> SwitchLane -> JumpOn ...`

But it spends more planning actions before reaching the same future useful targets:

- one action to leave the dangerous top lane,
- one action to return to the top lane and avoid `smallNotAliveRoad`,
- optional `PassiveCollect` or `PassiveAdvance`,
- another `SwitchLane` to return for the later `JumpOn`.

The direct branch:

`SwitchLane -> JumpOver -> JumpOn -> JumpOver -> JumpOn`

uses fewer actions because it pays energy instead of lane routing. With `MaxSearchDepth=6`, this branch can see two `JumpOn` objectives (`major=2`), while the valid detour branch sees only one (`major=1`) before the horizon cuts it off. Since `PlanEvaluator` compares `MajorObjectiveCount` before energy, it selects the energy-spending `JumpOver` branch.

In short: fixed action-count depth makes energy-spending shortcuts look strategically better because they see farther into the level than energy-saving detours.

## Proposed Solution

Change branch evaluation so route energy efficiency is not blocked by `MajorObjectiveCount`, while useful energy for objectives does not make targets disappear.

Implementation plan, adjusted after runtime checks and discussion:

1. Keep the current action-depth boundary in `PlanningGraphBuilder`.
2. Remove `TapCount` from branch quality: it duplicates action/input complexity without adding useful domain priority.
3. Split energy cost in `PlanningBranchMetrics`:
   - `RouteEnergyCost`: energy spent to pass the route without gaining a major objective.
   - `ObjectiveEnergyCost`: energy spent by an action that gains a major objective.
4. Use one branch-priority order in both evaluator and same-state dominance pruning:
   - `LifeCollectibleValue`
   - `RouteEnergyCost`
   - `MajorObjectiveCount`
   - `ObjectiveEnergyCost`
   - `CoinCollectibleValue`
   - `ActionCount`
5. Move this priority order into one shared comparer so pruning and final evaluator cannot diverge.
6. Keep `ActionCount` only as the final tie-breaker when semantic value and energy are equal.
7. Do not add a local obstacle rule like "prefer `SwitchLane` over `JumpOver`".

Expected effect for this regression:

- The direct `JumpOver` branch no longer wins merely because it sees more future major objectives inside the depth horizon.
- The free `SwitchLane` detour should beat energy-spending `JumpOver` when the life value is equal.
- Major objectives still matter before energy spent directly on those objectives, so `JumpOn` targets are not ignored.

Avoided quick fixes:

- Do not simply increase `MaxSearchDepth`: it only moves the same horizon bias farther and can regress again on denser patterns.
- Do not add a local hard rule "prefer SwitchLane over JumpOver": it hides the real issue and can break cases where jumping is actually the correct route.

## Verification

After implementation:

- Run `01_New_York/Morning/level_01`.
- Check first `easy_run` plan:
  - expected: second step after first `SwitchLane` is a `SwitchLane`/free bypass route, not `JumpOver`.
- Check runtime:
  - no `[Energy] spent amount=10` for the first `smallNotAliveRoad`.
- Then continue separately with the later `bigAlive` damage regression.
