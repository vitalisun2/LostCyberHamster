# NY Level 02 Life Loss Analysis

Дата: 2026-06-24

## Scope

Регресс: на `01_New_York/Morning/level_02` бот теряет жизнь после некоторого расстояния на уровне.

## Expected

`level_02` должен проходиться без `CollisionController damage` и без подтвержденного `Bot DEAD_END`, если в контенте есть достижимый безопасный маршрут с текущими bot contracts.

Authoritative source:
- описание пользователя;
- `docs/Planning/in-progress/ny-regressions-plan-2026-06-24.md`;
- runtime markers `CollisionController damage`, `Bot DEAD_END`, `TEST FINISH lives`.

## Commands

- `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_02' -TimeoutSeconds 600 -TimeScale 1`

## Evidence

- Fail log: `Temp/ny_regressions_2026-06-24/2026-06-24_215534_level_02_fail.txt`.
- Repro command result: `FAIL`.
- First damage:
  - log line 392: `CollisionController damage ... lane=top ... obstacle=bigNotAlive#-448600 ... x=[-2.98,0.90] lane=top`.
  - before damage: `JumpOver -> SwitchLane -> JumpOnRoof -> PassiveCollect[Energy] -> PassiveCollect[Coin] -> PassiveRoofExit`.
  - executed sequence:
    - line 383: `FIRE JumpOver targetId=-448212`.
    - line 387: `COMPLETE JumpOver`.
    - line 388: `FIRE SwitchLane targetId=-448234 ... targetLane=top`.
    - line 389: new active plan became only `SwitchLane`.
    - line 391: `COMPLETE SwitchLane`.
    - line 392: damage by top `bigNotAlive#-448600`.
- Second diagnostic pass:
  - temporary log `HANDOFF_DIAG` added in `RuntimeBotController.TryApplyCompletedAsyncReplan`.
  - log copy: `Temp/ny_regressions_2026-06-24/2026-06-24_220645_level_02_win_handoff_diag.txt`.
  - key line 394:
    - async result during in-progress `SwitchLane` had `deadEnd=True`;
    - current plan was `SwitchLane -> JumpOnRoof -> PassiveCollect[Energy] -> PassiveCollect[Coin] -> PassiveRoofExit`;
    - result plan was only `SwitchLane`;
    - dead-end reasons were generated after projecting the already-started `SwitchLane`.
  - same run won only because after `SwitchLane` completion a later replan arrived in time:
    - line 401: `Bot PLAN JumpOnRoof -> ...`;
    - line 402: `FIRE JumpOnRoof`.
- Code path:
  - `PlanExecutor.Tick` completes in-progress head, calls `AdvanceHead()`, then immediately `TryFireCurrentHead()` in the same tick. Therefore a preserved `JumpOnRoof` tail would be eligible for same-frame handoff.
  - `RuntimeBotController.BuildCommittedPrefix` commits only current in-progress head.
  - `AsyncPlanRebuilder.BuildPlanForRequest` prepends committed head to tail build result. If tail build is dead-end fallback with no tail actions, result becomes head-only.
  - `RuntimeBotController.IsAsyncResultApplicableToCurrentExecution` accepted any result whose first action matched the current in-progress head.
  - `RuntimeBotController.ApplyPlanBuildResult` then called `_executor.SetPlan(plan)`, replacing the old tail.
- Targeted fix:
  - `RuntimeBotController.ShouldPreserveCurrentHandoffTail` rejects only the narrow case where an in-progress async result is a `deadEnd` fallback, current plan has a next action, result plan has only equivalent head, and applying it would erase the handoff tail.
- Targeted verification:
  - log copy: `Temp/ny_regressions_2026-06-24/2026-06-24_221301_level_02_win_after_handoff_fix.txt`.
  - result: `WIN level=2 stars=3`.
  - line 384: `SwitchLane -> JumpOnRoof -> ...` remained active after the same switch.
  - line 386: `COMPLETE SwitchLane`.
  - line 388: `FIRE JumpOnRoof`.
  - no `CollisionController damage`.

## Hypotheses

- H1: evaluator выбирает семантически худшую ветку при наличии безопасной.
- H2: planner не видит безопасную ветку из-за horizon/depth/pruning.
- H3: async handoff применяет stale plan или фиксирует неверный prefix.
- H4: execution не успевает/не может выполнить корректный planned action.
- H5: контентный energy budget делает безопасный маршрут недостижимым без дополнительной энергии.

## Root Cause

Root cause: async replan could apply a dead-end fallback result captured while the head action was already in progress and replace the current plan tail with a head-only plan. This destroyed the immediate post-action handoff (`JumpOnRoof`) that `PlanExecutor` is designed to execute in the same tick after `SwitchLane` completion.

Excluded alternatives:
- Energy shortage: energy before the problematic sequence was sufficient (`value=72` after `JumpOver`, `JumpOnRoof` cost is `10`).
- Level geometry: the diagnostic win run proved the same geometry is passable by the planned `JumpOnRoof` when the tail is available; after fix the same `SwitchLane -> JumpOnRoof` sequence fires successfully.
- Evaluator priority: the evaluator had already selected the correct route (`JumpOver -> SwitchLane -> JumpOnRoof -> ...`) before async fallback erased its tail.
- Execution inability: `PlanExecutor.Tick` supports immediate next-head execution after completion; post-fix log shows `JumpOnRoof` fires after `SwitchLane` completion.
