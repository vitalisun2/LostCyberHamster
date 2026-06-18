# NY switch lane oscillation analysis - 2026-06-18

## Scope

Regression: на первом уровне New York bot выбирает лишние чередующиеся `SwitchLane` вместо более прямого прохождения препятствий прыжками.

Expected: если две successful-ветки доходят до горизонта планирования, ветка, которая реально разбирает больше препятствий, не должна проигрывать только потому, что временно экономит 10 energy через дополнительный `SwitchLane`.

Actual: выбранная max-depth ветка может закончиться `SwitchLane` к уже ближайшему obstacle и оставить этот obstacle следующим (`finalNext` не продвижается), хотя есть successful-альтернатива с меньшим числом tap-ов и большим `finalNext`.

## Sources

- Fresh run log: `Temp/ny_level_01_switch_oscillation_candidates_2026-06-18_1529.txt`
- Ranking code: `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanEvaluator.cs`
- Branch metrics: `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningBranchMetrics.cs`
- Switch action creation: `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`

## Hypotheses

1. Geometry forces the oscillation.
   - Confirms: all successful alternatives must contain the same extra `SwitchLane` sequence.
   - Refutes: a successful alternative exists that resolves the contested obstacle without the extra `SwitchLane`.
   - Status: refuted.

2. SwitchLane strategy generates an invalid target.
   - Confirms: generated action points to impossible or wrong-lane obstacle.
   - Refutes: generated actions are physically valid and alternatives differ only by final action choice.
   - Status: refuted for the analyzed case.

3. Evaluator ranks a lower-progress max-depth branch above a higher-progress branch because energy is compared before progress.
   - Confirms: selected branch has lower `finalNext` and lower energy; rejected branch has higher `finalNext` but higher energy.
   - Refutes: selected branch has equal or higher progress than alternatives.
   - Status: confirmed.

## Facts

- At log lines 683-685, `PLAN_BUILD` has `successCount=2`.
- Selected candidate: `cost=10 taps=5 finalNext=18`, ending with `SwitchLane before smallNotAliveRoadAndRoof targetIdx=18 targetId=-74692`.
- Rejected candidate: `cost=20 taps=4 finalNext=19`, same prefix, but final action is `JumpOver` over the same `smallNotAliveRoadAndRoof targetIdx=18 targetId=-74692`.
- `PlanEvaluator.CompareBranches` compared `JumpOnObjective` priority, then total energy, then tap count. It did not compare `FinalNextObstacleIndex`.

## Root Cause

Max-depth successful branches were ranked without real progress. Because `SwitchLane` costs 0 energy, a branch that only dodges into another lane and leaves the same obstacle as the next unresolved target won over a branch that spent 10 energy to actually clear that obstacle. This created visible back-and-forth lane switching.

## Fix

After `JumpOn` objective priority, compare `FinalNextObstacleIndex` descending before energy and tap count. Dead-end fallback ranking still uses the same branch comparator as successful branches.

## Verification

- Fresh run after the fix: `Temp/ny_level_01_switch_oscillation_after_progress_fix_2026-06-18_1536.txt`
- Result: `WIN level=1 stars=3`.
- Previously bad area changed from `finalNext=18` with a final reverse `SwitchLane` to a higher-progress branch:
  - line 101: selected branch reaches `finalNext=22` and uses `JumpOver` over `smallNotAliveRoadAndRoof` instead of leaving it unresolved through a reverse `SwitchLane`.
  - line 107: follow-up selected branch reaches `finalNext=23`.

## Remaining Risk

The fresh log still contains other switch-heavy plans later in the level. They are not proven to have the same root cause and should be investigated as a separate regression if they are visually wrong.
