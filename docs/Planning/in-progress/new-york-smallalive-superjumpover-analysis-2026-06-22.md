# New York smallAlive SuperJumpOver regression analysis 2026-06-22

## Scope

- Уровень: `01_New_York/Morning/level_01`, первый уровень Нью-Йорка.
- Сценарий: нижняя линия, впереди `smallAlive`, сразу за ним `smallNotAlive`, дальше заметный gap до следующего препятствия.
- Expected: бот выбирает обычный `JumpOn` на `smallAlive`; отскок/траектория безопасно проходит `smallNotAlive`; стоимость 10 энергии.
- Actual: бот выбирает `SuperJumpOver smallAlive`, фактически перепрыгивая оба препятствия; стоимость 20 энергии.
- Цель: доказать, почему `SuperJumpOver` выбран вместо энергоэффективного `JumpOn`, без постоянных правок логики.

## Sources

- `EditorLogs/diagnostic_log.txt`, канал `BOT`.
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanEvaluator.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningBranchMetricsComparer.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningGraphBuilder.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Planning/ActionGenerator.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOn/JumpOnStrategy.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOn/JumpOnFireWindowFinder.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpPlanning/TargetRemovalPostActionSafety.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOn/JumpOnSimulator.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/Simulation/PlanningStateTransition.cs`
- `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOver/SuperJumpOverStrategy.cs`
- `LostCyberHamster/Assets/Scripts/GameEngine/Mechanics/JumpOutcomeResolver.cs`

## Hypotheses

### H1: evaluator incorrectly prefers SuperJumpOver despite valid cheaper JumpOn

- Would confirm: logs show a valid branch starting with `JumpOn` with lower `EnergyCost` than the selected `SuperJumpOver` branch.
- Would refute: comparer currently ranks by `EnergyCost` first, or logs show `JumpOn` branch is absent/not valid/higher total energy.
- Status: refuted as stated. `JumpOn` is valid, but its best full branch has the same total `EnergyCost` as the selected `SuperJumpOver` branch, not lower.

### H2: JumpOnStrategy rejects this case before it reaches evaluator

- Would confirm: targeted strategy log for the `smallAlive + smallNotAlive` chain shows `JumpOnStrategy` returns dead-end/not applicable, while `SuperJumpOverStrategy` returns an action.
- Would refute: targeted log shows `JumpOnStrategy` returns a valid action at the same projected planning state.
- Status: refuted. Targeted log shows `JumpOnStrategy` returns valid `JumpOn cost=10` at the same projected state where `SuperJumpOverStrategy` returns valid `SuperJumpOver cost=20`.

### H3: JumpOn simulator/post-action model cannot represent clearing the following smallNotAlive by bounce/trajectory

- Would confirm: `JumpOn` runtime-window confirms hit on `smallAlive`, then `TargetRemovalPostActionSafety` rejects because after removing only target `smallAlive`, projected `smallNotAlive` overlaps the hamster on Run re-entry.
- Would refute: `JumpOn` rejection is caused by another reason, for example no analytic window, insufficient energy, or wrong target resolution.
- Status: refuted for this regression. `JumpOn` action is generated successfully; no post-action safety rejection occurs for the selected decision point.

### H4: full-branch energy after JumpOn is not actually better than SuperJumpOver

- Would confirm: targeted branch/plan logs show `JumpOn` path needs extra paid action immediately after, making total branch energy >= selected branch.
- Would refute: `JumpOn` path is not present at the decision point, or the only known blocker is strategy validation.
- Status: confirmed. Branch-level log shows the best `JumpOn` branch has total `E=20,A=6`; selected `SuperJumpOver` branch also has `E=20,A=6`.

### H5: tie-breaking is accidental strategy order, not meaningful preference

- Would confirm: comparer returns equality for equal `EnergyCost` and `ActionCount`, and `SuperJumpOverStrategy` is registered before `JumpOnStrategy`.
- Would refute: comparer has a later semantic tie-breaker that should prefer `JumpOn`, or `JumpOnStrategy` is explored first.
- Status: confirmed.

## Facts

- Existing BOT log at `09:36:25.479` shows selected plan starts with `SuperJumpOver`; at `09:36:26.443` it fires `Super jump over smallAlive`.
- Current `PlanningBranchMetricsComparer.Compare` compares `EnergyCost` first, then `ActionCount`.
- `JumpOnPolicy.EnergyCost` is 10; `SuperJumpOverPolicy.EnergyCost` is 20.
- `JumpOnFireWindowFinder` requires both runtime hit on the target and `TargetRemovalPostActionSafety.IsSafeAfterCompletion`.
- `TargetRemovalPostActionSafety` removes only the jump-on target from safety analysis, then checks future same-lane damaging ground obstacles at Run re-entry.
- `JumpOnSimulator` advances with `AdvanceAfterTargetRemoval`, which records only `action.TargetObstacleInstanceId` as removed.
- `JumpOutcomeResolver.ResolveJump` runtime check can return `JumpOnObstacle` for `smallAlive` based on the jump resolver point, but that resolver does not by itself remove/skip a following obstacle in planning state.
- Temporary strategy log at `09:51:27` for the relevant `smallAlive + smallNotAlive` chain:
  - `SuperJumpOverStrategy`: valid action `SuperJumpOver cost=20 desc=Super jump over smallAlive`.
  - `JumpOnStrategy`: valid action `JumpOn cost=10 desc=Jump on smallAlive`.
- Temporary branch log at `09:51:27.154-09:51:27.155`:
  - selected branch: `E=20,A=6`, starts with `SuperJumpOver[E=20,desc=Super jump over smallAlive]`.
  - best `JumpOn` branch: `E=20,A=6`, starts with `JumpOn[E=10,desc=Jump on smallAlive]`, then later contains `JumpOver[E=10,desc=Jump over smallNotAliveRoad]`.
  - best `SuperJumpOver` branch: `E=20,A=6`.
- `RuntimeBotController` registers `SuperJumpOverStrategy` before `JumpOnStrategy`.
- `PlanEvaluator.SelectBest` keeps the current `best` when `CompareBranches(candidate, best) == 0`.
- `PlanningBranchMetricsComparer` compares only `EnergyCost`, then `ActionCount`; for `E=20,A=6` vs `E=20,A=6` it returns equality.

## Root Cause

Root cause: this is not a local `JumpOn` validation failure. `JumpOn` is generated and valid. The selected `SuperJumpOver` appears because the evaluator compares full branches only by total energy cost and action count. In the reproduced horizon, the best `JumpOn` branch costs 10 for `JumpOn` plus another 10 for a later `JumpOver`, so its total branch energy is also 20. The best `SuperJumpOver` branch costs 20 once and has the same action count. Since the comparer sees the branches as equal, selection falls back to generation order; `SuperJumpOverStrategy` is registered before `JumpOnStrategy`, so the super jump branch remains selected.

## Proposed Solution

Add a simple semantic tie-breaker for equal full-branch cost/action count: prefer useful target interaction (`JumpOn`) over pure avoidance (`SuperJumpOver`) when the compared branches are otherwise equal by primary metrics. This should be implemented as an explicit branch metric or comparer rule, not by relying on strategy order.

Minimal meaningful order for this case:

1. `EnergyCost`
2. `ActionCount`
3. `JumpOnObjectiveCount` or equivalent target-interaction count
4. deterministic fallback if still equal

This keeps energy efficiency first, but prevents expensive-looking avoidance from winning only because it was generated earlier when the full-branch cost ties.

## Verification

Observed with temporary targeted logs on `01_New_York/Morning/level_01`. Diagnostic code was removed after collecting evidence. Final verification for a fix should rerun the same level and check that the plan at this moment uses `JumpOn smallAlive`, not `SuperJumpOver smallAlive`.
