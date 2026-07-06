# shift_force_switch Big Life damage analysis — 2026-07-06

## Scope

- Regression: `01_New_York/Evening/level_05`, pattern index 5, `shift_force_switch`.
- User-observed actual: bot runs on the bottom lane and takes damage by colliding with the bottom-lane Big Life.
- Expected source: user report and gameplay contract for bot survival/action planning; when enough energy is available, the bot should use an available super jump instead of running into a damaging obstacle.
- Current branch/worktree: `integration/unity-live`, no separate branch/worktree.

## Commands

- `Get-Content -Raw docs/rules/AGENTS.md`
- `Get-Content -Raw docs/rules/agent_tools.md`
- `git status --short --branch`
- `rg -n "shift_force_switch|Shift Force Switch|force_switch|Big Life|big life|super_jump|super jump|life" LostCyberHamster/Assets/Content LostCyberHamster/Assets/Scripts/Bot docs -g "*.json" -g "*.cs" -g "*.md"`
- `Get-Content -Raw tools/invoke_open_unity_test_level.ps1`
- `Get-Content -Raw LostCyberHamster/Assets/Content/locations/01_New_York/levels/Evening/level_05/level_05.json`
- `./tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Evening/level_05' -TimeoutSeconds 180 -TimeScale 1`
- `./tools/read_log_channel.ps1 -Channel STAB -Tail 200`
- `./tools/read_log_channel.ps1 -Channel BOT -Tail 500`
- `./tools/read_log_channel.ps1 -Channel ECO -Tail 200`

## Facts

- `level_05.json` pattern sequence index 5 uses `ref: "shift_force_switch"`.
- Minimal reproduction candidate: `./tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Evening/level_05' -TimeoutSeconds 120 -TimeScale 1`.
- Minimal reproduction executed with `TimeoutSeconds 180`, result `FAIL`.
- Runtime damage: `CollisionController damage ... state=Run ... lane=bottom ... obstacle=bigAlive#-246522 ... x=[-3.01,-2.01]`.
- Energy before damage is sufficient for super jump: ECO channel shows `value=77` at `19:31:51.988`.
- Immediately before damage the bot executes `PassiveAdvance targetId=-247144 ... runtimeNearestThreat=bigAlive#-246522 ... runtimeUnsafe=[1.09,3.73] runtimeIntersects=False`.
- Dead-end causes after damage include `SuperJumpOverStrategy: Нет безопасного окна для перепрыгивания: bigAlive требует дополнительный зазор, которого нет в этом участке.`

## Second Diagnostic Pass

Question: does `SuperJumpOverStrategy` reject a runtime-achievable jump because the bigAlive fire-window padding collapses the window, and does `PlanBuilder` then install the dead-end safe-prefix plan as executable actions?

Temporary diagnostics to add:
- Essential strategy log at `JumpOverChainCalculator` when bigAlive padding collapses a jump-over window.
- Essential replan log at `PlanBuilder.BuildDeadEndFallbackResult` when a dead-end fallback plan with actions is returned.

## Reopened Diagnostic Pass

Question: while bot is in `RoofRun` on `bigNotAlive`, does `ActionGenerator` expose the road `bigAlive` as a current obstacle-chain to `JumpOnFromRoof` / `SuperJumpOnFromRoof`, and if exposed, which resolver/window/resource check rejects it?

Temporary diagnostics added:
- Essential roof-state action generation summary in `BotStrategyDiagnostics.LogActionGenerationContext`, called from `ActionGenerator`.
- `LogStrategySkipped` now includes ordinary `JumpOnFromRoof` as well as `SuperJumpOnFromRoof`.

## Critical Route Diagnostic Pass

Question: if `SuperJumpOver` or roof `JumpOnFromRoof` is available around the bottom `bigAlive`, where does the branch stop being a successful leaf: strategy generation, transition simulation, graph pruning, or dead-end candidate selection?

Temporary diagnostics added:
- Deduped all high-frequency temporary strategy logs by unique decision/obstacle/window key to avoid changing runtime timing with log spam.
- Essential `DEAD_END_CANDIDATE_TRACE` for only branches containing `SuperJumpOver`, `JumpOnFromRoof`, `SuperJumpOnFromRoof`, or `PassiveRoofExit`, emitted from `PlanningGraphBuilder.AddDeadEndBranch`.
- Deduped `DEAD_END_FALLBACK_PLAN` by plan/report key.

## Hypotheses

- Reopened 2026-07-06 after user challenge: earlier conclusion about double bigAlive padding is not accepted as proven root cause. Treat it as one hypothesis only. Need prove why none of the apparently available strategies produced a safe action.
- H1: planner cannot see a valid super-jump action for the lower Big Life.
- H2: planner sees super jump but branch ranking/collectable priority selects an unsafe lower-line run.
- H3: runtime executor/gate has a valid super-jump plan but fails to fire it.
- H4: level/pattern data does not actually make a super jump possible or energy is insufficient.
- H5: while on roof, planner does not expose the road bigAlive as an ObstacleChain decision point for JumpOnFromRoof/SuperJumpOnFromRoof.
- H6: JumpOnFromRoof/SuperJumpOnFromRoof sees the road bigAlive but rejects it by reach/window/runtime-target/resource constraints.
- H7: PassiveRoofExit is selected because it is the only applicable action for a MovingBoundary decision point, not because road threat was validated as safe.

## Evidence Table

| case | expected | actual | selected branch/result | excluded alternatives | first divergence |
|---|---|---|---|---|---|
| TBD | super jump over/around bottom Big Life when energy is sufficient | damage on bottom Big Life | TBD | TBD | TBD |

## Reproduction 2026-07-06 20:32

Command: `./tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Evening/level_05' -TimeoutSeconds 220 -TimeScale 1`.
Result: FAIL.
Facts to analyze:
- `ACTION_GEN_ROOF_DIAG` at 20:32:27.184: RoofRun bottom, current `bigAlive#-268206`, actions include both `PassiveRoofExit` and `JumpOnFromRoof`.
- `DEAD_END_CANDIDATE_TRACE` at 20:32:41.346: `JumpOnFromRoof` branch exists but becomes dead-end at reportDepth=1/reportProjection=17.88.
- `DEAD_END_CANDIDATE_TRACE` at 20:32:41.344 and `DEAD_END_FALLBACK_PLAN` at 20:32:41.346: selected safe-prefix is `PassiveRoofExit -> PassiveAdvance`, reportDepth=2/reportProjection=11.27.
- `JUMP_OVER_BIGALIVE_WINDOW_COLLAPSE` at 20:32:44.215: `SuperJumpOver`, energy=76, hamsterX=[-4.60,-2.96], bigAlive#-268206 x=[-2.20,-1.20], jumpTravel=5.13, before=[0.10,0.66], padding=0.49, after=[0.59,0.17].
- Damage at 20:32:44.478: RunState collision with `bigAlive#-268206`, hamsterX=[-4.60,-2.96], obstacle x=[-2.98,-1.98].

## Git History: single bigAlive padding

- `d264d729b` (`Add role-based strategy specs for new planning path`, 2026-06-04) introduced `ApplyBigAliveCollisionPadding` with both `lastFireShift -= padding` for first `bigAlive` and `firstFireShift += padding` for last `bigAlive`.
- `7ac31ea0` (`Improve bot level validation diagnostics`, 2026-06-13) added `coversSingleObstacle = firstObstacle.InstanceId == lastObstacle.InstanceId` and skipped the second padding for a single `bigAlive`.
- Same commit added `docs/Planning/in-progress/superjump-over-bigalive-window-analysis-2026-06-13.md`; its facts match the current pattern: one `bigAlive` was first and last chain obstacle, base window stayed valid, double padding collapsed the window before `JumpOverFireWindowFinder`/runtime resolver.
- `8d9d294c` (`Adjust jump-over timing for single bigAlive`, 2026-06-26) removed the `coversSingleObstacle` exclusion and changed the comment to `including single bigAlive`. The commit touched only `JumpOverChainCalculator.cs` and has no explanatory body/tests/docs in the commit.

## Validation Regression: skipping second single-bigAlive padding

Scope: `01_New_York/Evening/level_05`, pattern index 5, after changing `JumpOverChainCalculator.ApplyBigAliveCollisionPadding` to skip `firstFireShift += padding` when `firstObstacle.InstanceId == lastObstacle.InstanceId`.

Command: `./tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Evening/level_05' -TimeoutSeconds 220 -TimeScale 1`.

Result: FAIL.

Facts:
- The original lower `bigAlive` was bypassed: `[21:06:55.748] FIRE kind=SuperJumpOver targetId=-275358`, `[21:06:57.117] COMPLETE kind=SuperJumpOver targetId=-275358`.
- Later in the same pattern, the bot fired another single-target `SuperJumpOver`: `[21:07:02.568] FIRE kind=SuperJumpOver targetId=-276124 ... obstacleLeftX=-0.68 window=[-0.65,-2.37]`.
- Runtime damage was not `RunState`; it was `BigAliveJumpOverlap` while already in `SuperJumpOver`: `[21:07:03.643] damage ... state=SuperJumpOver ... lane=top hamsterX=[-4.60,-2.96] obstacle=bigAlive#-276124 ... x=[-4.77,-3.77] lane=top`.
- Overlap from the logged intervals is `min(-2.96,-3.77)-max(-4.60,-4.77)=0.83`.
- Runtime threshold is `CollisionController.BigAliveJumpDamageOverlapThreshold = 0.3`, applied as `_hamster.ColliderWidth * threshold`; the logged hamster interval width is `1.64`, threshold about `0.49`, so `0.83 > 0.49`.
- Energy was sufficient: before the action the bot had `43`, then spent `10` for jump and `10` for super upgrade, ending at `23`.

Code path:
- `SuperJumpOverPolicy.BigAliveCollisionPaddingRatio` returns `CollisionController.BigAliveJumpDamageOverlapThreshold`.
- `JumpOverChainCalculator.ApplyBigAliveCollisionPadding` uses `hamster.Width * policy.BigAliveCollisionPaddingRatio` as planning padding.
- The experimental `coversSingleObstacle` guard skipped `firstFireShift += padding` for the same obstacle being both first and last in the chain.
- `SuperJumpOverExecutor` fired inside the resulting window.
- `CollisionController.ProcessTriggerEnter` then called `HasCollisionWithBigAliveInJumpState`; for `SuperJumpOver` and `bigAlive`, it computes X-overlap and damages when overlap is greater than the same `BigAliveJumpDamageOverlapThreshold`.

Conclusion: skipping the second padding for a single `bigAlive` violates the runtime collision contract. The assumption that the base jump-over window is enough for a single obstacle only covers the resolver's discrete start/end overlap model; runtime also checks trigger-enter overlap during the `SuperJumpOver` animation.
