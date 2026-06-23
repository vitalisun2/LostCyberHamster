# Test Level SwitchLane Regression Analysis

Дата: 2026-06-24

## Scope

Проверить все 16 test levels на `WIN`, отсутствие damage/dead-end, соответствие pattern description и лишние `SwitchLane`.

## Expected

`should <action>` паттерны должны приводить к соответствующим действиям, `should not <action>` не должны выполнять запрещенное действие. `SwitchLane` считается expected только если он нужен для route, collectable, roof setup или безопасного обхода без последующего jump-over.

Authoritative source:
- `PatternsCollection.json` descriptions для `test_*`;
- `docs/rules/agent_tools.md`;
- `docs/Planning/in-progress/ny-regressions-plan-2026-06-24.md`.

## Commands

- `.\tools\invoke_run_all_test_levels.ps1 -TimeoutSeconds 180 -TimeScale 1`

## Evidence

### Batch before fix

Command:
- `.\tools\invoke_run_all_test_levels.ps1 -TimeoutSeconds 180 -TimeScale 1`

Artifact:
- `Temp/all_test_levels_2026-06-23_225604`

Facts:
- All 16 test levels returned `WIN`.
- Damage markers: 0.
- Description checker passed all expected patterns.
- Suspicious extra `SwitchLane` remained in `01_New_York/Morning/test_super_jump_over`.

Problem fragment from `Temp/all_test_levels_2026-06-23_225604/01_New_York_Morning_test_super_jump_over.txt`:

| Pattern | Expected | Actual |
| --- | --- | --- |
| `test_super_jump_over_02` / `test_super_jump_over_03` overlap | direct `SuperJumpOver` where description says `should super jump over ...` | `SwitchLane` to top before `bigAlive`, immediately `SwitchLane` back to bottom before `smallNotAliveRoadAndRoof`, then `SuperJumpOver` |

The semantic checker did not fail because the required `SuperJumpOver` still fired for the pattern obstacle id. The visual regression was the extra route setup before that action.

### Diagnostic pass

Temporary log:
- `SJO_DIAG` in `PlanBuilder.Build`, removed after proof.

Command:
- `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_super_jump_over' -TimeoutSeconds 180 -TimeScale 1`

Artifact:
- `Temp/ny_regressions_2026-06-24/2026-06-24_test_super_jump_over_sjo_diag.txt`

Key facts:
- Before next pattern entered the horizon, plan was direct: `SuperJumpOver -> PassiveCollect[Energy] -> PassiveCollect[Energy]`.
- When `test_super_jump_over_03` spawned, selected best became:
  - `SwitchLane -> SuperJumpOver -> SwitchLane -> PassiveCollect[Energy] -> PassiveCollect[Energy] -> SwitchLane`.
- The direct branch existed in the same candidate set:
  - `SuperJumpOver -> PassiveCollect[Energy] -> SwitchLane -> PassiveCollect[Energy] -> SwitchLane -> SwitchLane`.
- Selected branch and direct branch had equal metrics:
  - `life=0,beforeMajor=20,major=0,cost=20,immTarget=0,immBypass=20,energy=38,coin=0,actions=6`.
- `PlanningBranchComparer.Compare(direct, selected) == 0`, so `PlanEvaluator.SelectBest` kept the first branch returned by graph order.
- After the first `SwitchLane`, the same tie shape allowed the second `SwitchLane` before the eventual `SuperJumpOver`.

Code references:
- `PlanEvaluator.SelectBest`: replaces current best only when `PlanningBranchComparer.Compare(candidate, best) < 0`.
- `PlanningBranchMetricsComparer.Compare`: before fix had no criterion that distinguished route with immediate setup (`SwitchLane -> SuperJumpOver`) from direct route (`SuperJumpOver`) when energy/objectives/collectibles/action count were equal.
- `PlanningGraphBuilder.ShouldPruneSameState`: uses the same comparer via `PlanningBranchComparer.IsBetterOrEqual`, so evaluator and pruning shared the same blind spot.

### Fix verification

Fix:
- Added `ImmediateRouteSetupActionCount` to `PlanningBranchMetrics`.
- Added it to `PlanningBranchMetricsComparer` after collectable tie-breakers and before final `ActionCount`.
- Reason for ordering: route directness is a cleanliness tie-breaker, not a semantic objective. It must not suppress safe collectables.

Targeted command:
- `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_super_jump_over' -TimeoutSeconds 180 -TimeScale 1`

Artifact:
- `Temp/ny_regressions_2026-06-24/2026-06-24_test_super_jump_over_after_route_setup_metric.txt`

Result:
- `WIN`.
- No `SJO_DIAG`.
- Real `SwitchLane` `FIRE` count in `test_super_jump_over`: 3 -> 1.
- Removed regression sequence:
  - before: `SwitchLane -> SwitchLane -> SuperJumpOver`;
  - after: `SuperJumpOver`, later one `SwitchLane` only for energy/next route.

Regression found during batch after first metric placement:
- `test_collectables_03` collected 2/3 coins.
- Cause: `ImmediateRouteSetupActionCount` was initially placed before `CoinCollectibleValue`, so a safe coin branch with extra collectable switch lost before the comparer considered coin value.
- Correction: moved `ImmediateRouteSetupActionCount` after `EnergyCollectibleValue` and `CoinCollectibleValue`.
- Targeted verification:
  - `Temp/ny_regressions_2026-06-24/2026-06-24_test_collectables_after_route_metric_order_fix.txt`: 3 coin fires, 3 coin collects, damage 0.
  - `Temp/ny_regressions_2026-06-24/2026-06-24_test_super_jump_over_after_order_fix.txt`: `SwitchLane=1`, `SuperJumpOver=3`, damage 0.

Final test-level control:
- Command: `.\tools\invoke_run_all_test_levels.ps1 -TimeoutSeconds 180 -TimeScale 1`
- Artifact: `Temp/all_test_levels_2026-06-24_000940`
- Result: all 16 levels passed.
- Damage markers: 0 in every per-level summary.
- Description/semantic checks: passed for all checked patterns.
- `test_super_jump_over`: `SwitchLane=1`, `SuperJumpOver=3`.
- `test_collectables`: semantic ok, all 3 coins in `test_collectables_03` collected.

## Root Cause

`PlanningBranchMetricsComparer` did not encode local route directness when two branches were otherwise equivalent. Therefore `SwitchLane -> SuperJumpOver` and direct `SuperJumpOver` compared as equal, and `PlanEvaluator.SelectBest` kept whichever branch appeared first in graph enumeration. Since same-state pruning uses the same comparer, the same equality also affected pruning decisions. The fix belongs in branch metrics/comparer, not in a test-level special case or strategy guard, because this is a cross-cutting branch-ranking invariant. The directness metric must stay below collectable value, because collectables are explicit objectives and route cleanliness is only a final semantic tie-breaker.
