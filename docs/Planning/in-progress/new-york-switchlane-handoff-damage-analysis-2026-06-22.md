# New York SwitchLane Handoff Damage Analysis - 2026-06-22

## Scope

Один регресс в `01_New_York/Morning/level_01`.

- Место: после участка `small_jumps`, около `07:42:00-07:42:04` в текущем diagnostic log.
- Expected: после `JumpOn smallAlive` бот не должен оставаться на верхней линии перед `bigAlive`; следующий action должен завершиться или быть заменен безопасным replan-ом.
- Actual: после `JumpOn` бот запускает `SwitchLane` на верхнюю линию, не логирует `COMPLETE SwitchLane`, затем получает damage от верхнего `bigAlive`.

Out of scope:

- Качество текущего evaluator-а в целом.
- Предыдущие лишние `JumpOver` / `SwitchLane`, если они не являются непосредственной причиной потери жизни.

## Sources

- Runtime log: `LostCyberHamster/EditorLogs/diagnostic_log.txt`, run `2026-06-22 07:41-07:42`.
- Runtime log with targeted diagnostics: `LostCyberHamster/EditorLogs/diagnostic_log.txt`, run `2026-06-22 07:53-07:54`.
- Code:
  - `LostCyberHamster/Assets/Scripts/Bot/Execution/PlanExecutor.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneExecutor.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneSimulator.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/PassiveCollect/PassiveCollectExecutor.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/PassiveCollect/PassiveCollectSimulator.cs`
- Test infrastructure:
  - `tools/invoke_open_unity_test_level.ps1`
  - `tools/read_log_channel.ps1`

## Hypotheses

### H1: Replan after `Completed|Fired` resets already fired `SwitchLane`

Meaning: `PlanExecutor.Tick` completes `JumpOn`, immediately fires next `SwitchLane`, then `RuntimeBotController` rebuilds plan because of `ActionCompleted`. If committed-prefix tail projection fails and controller falls back to a fresh live-root plan, `PlanExecutor.SetPlan` can replace the head and set `_isActionInProgress=false`. Physical lane switch already happened, but executor stops tracking it, so `COMPLETE SwitchLane` never appears.

Would confirm:

- Diagnostic log shows one tick with `executionResult=Completed|Fired`.
- Replan reason contains `ActionCompleted`.
- Current head after tick is fired `SwitchLane`.
- `BuildTailRootState` or committed-prefix projection returns `null`.
- Controller falls back to live-root build.
- `PlanExecutor.SetPlan` logs `preserveInProgressHead=false` while old head is the fired `SwitchLane`.
- After that, no `COMPLETE SwitchLane` appears before damage.

Would refute:

- Committed-prefix build succeeds.
- `SetPlan` preserves the in-progress `SwitchLane`.
- Executor continues tracking the `SwitchLane` until damage.

Status: confirmed.

### H2: `SwitchLane` remains tracked but never completes

Meaning: executor still has `SwitchLane` as in-progress head, but `SwitchLaneExecutor.IsCompleted` never returns true because runtime lane/shifting state does not match the action.

Would confirm:

- `SetPlan` preserves the in-progress head.
- Repeated ticks keep old head as `SwitchLane`.
- `SwitchLaneExecutor.IsCompleted` sees `isShifting=true` for too long, or final lane differs from target.

Would refute:

- In-progress flag is reset by replan before completion.

Status: refuted. The log shows `_isActionInProgress` is reset by `SetPlan` before the damage.

### H3: Planner intentionally routes to unsafe top lane

Meaning: execution handoff is correct, but the selected branch itself is unsafe: it sends the hamster to top lane and then does not plan a valid action before top `bigAlive`.

Would confirm:

- `SwitchLane` completes normally.
- New selected plan after completion keeps hamster on top with no valid action before `bigAlive`.
- Damage happens while executor is waiting for a future action, not because tracking was reset.

Would refute:

- The action tracking is lost before normal completion.

Status: refuted as primary cause. The branch is not executed to its planned continuation; the already fired head is erased by replan.

### H4: Collision/damage system damages during a valid transition

Meaning: bot execution is correct, but collision treats a valid transition or protected state as `RunState` damage.

Would confirm:

- Executor still tracks a valid in-progress action or transition state at damage time.
- Collision log shows state/protection inconsistent with expected transition.

Would refute:

- At damage time bot/runtime state is plain `Run` because action tracking was reset or action completed incorrectly.

Status: refuted. Damage happens in `Run`, `protected=False`, `pending=null`, after the bot has already dropped the in-progress action.

## Facts

- Existing log:
  - `07:41:59.460`: plan is `JumpOn -> SwitchLane -> PassiveCollect[Energy:20] -> PassiveAdvance -> SwitchLane -> ...`.
  - `07:42:00.454`: `FIRE kind=JumpOn ... desc=Jump on smallAlive`.
  - `07:42:02.278`: `COMPLETE kind=JumpOn ...`.
  - `07:42:02.278`: immediately after, `FIRE kind=SwitchLane ... targetLane=top desc=Switch lane before smallNotAliveRoad`.
  - There is no later `COMPLETE kind=SwitchLane`.
  - `07:42:04.036`: damage on `lane=top`, obstacle `bigAlive#-4788`, `state=Run`.
  - `07:42:06.772`: second damage on `lane=top`, obstacle `bigAlive#-4812`, `state=Run`.
- Targeted diagnostic run:
  - `07:54:24.324`: selected plan is `JumpOn -> SwitchLane -> PassiveCollect[Energy:20] -> PassiveAdvance -> SwitchLane -> ...`.
  - `07:54:25.299`: `FIRE kind=JumpOn`.
  - `07:54:27.131`: `COMPLETE kind=JumpOn` and immediately `FIRE kind=SwitchLane ... targetLane=top`.
  - `07:54:27.133`: executor reports `CompletedThenNext ... nextResult=Fired ... newHead=SwitchLane ... inProgress=True`.
  - `07:54:27.134`: replan starts because of `ActionCompleted` while `executorInProgress=True`, current head is the fired `SwitchLane`.
  - `07:54:27.135-07:54:27.136`: committed prefix step 0 projects the in-progress `SwitchLane` successfully.
  - `07:54:27.137`: committed prefix step 1 (`PassiveCollect Energy collectablePizza`) fails with `simulate-pending-null reason=trigger-not-reachable`.
  - `07:54:27.138`: controller falls back to live-root build: `live-root reason=tail-root-null`.
  - `07:54:27.140`: live-root build returns `built actions=0`.
  - `07:54:27.140`: `SetPlan wasInProgress=True preserve=False oldHead=SwitchLane ... newHead=none`.
  - `07:54:27.673`: collectible is collected on top lane, so the world keeps moving through the route, but executor is no longer tracking the fired `SwitchLane`.
  - `07:54:28.891`: damage on top `bigAlive`, `state=Run`, `protected=False`, `pending=null`.
- Code facts:
  - `PlanExecutor.Tick` completes the current action, advances head, immediately tries to fire the next head, and returns `Completed | nextHeadResult`.
  - `RuntimeBotController.TickBot` requests replan on `ActionCompleted` even when the same tick also fired the next action.
  - `BuildPlanForCurrentExecutionState` uses a two-action committed prefix. If tail-root projection returns `null`, it falls back to `_planBuilder.Build(LastSnapshot)`.
  - `PlanExecutor.SetPlan` preserves an in-progress action only when the old head is equivalent to the new head. Empty or different fresh plan resets `_isActionInProgress`.
  - `PlanBuilder` can return an empty successful plan as "continue running", so an empty live-root plan does not automatically create a dead-end report.

## Root Cause

The bot loses the already fired action during replan.

Precise chain:

1. `JumpOn` completes.
2. In the same executor tick, the next action `SwitchLane` is fired and becomes in-progress.
3. Because the tick contains `Completed`, `RuntimeBotController` immediately rebuilds the plan.
4. Rebuild tries to preserve committed prefix: fired `SwitchLane` plus next `PassiveCollect`.
5. Projection of fired `SwitchLane` succeeds.
6. Revalidation of the next pending `PassiveCollect` says `trigger-not-reachable`.
7. Current code treats this as failure of the whole committed prefix and falls back to a fresh live-root build.
8. Fresh build returns an empty plan.
9. `SetPlan` receives empty plan while `SwitchLane` is physically in progress, cannot preserve the old head, and resets `_isActionInProgress`.
10. The hamster remains on top lane with no tracked action/plan and runs into top `bigAlive`.

So the primary bug is not collision, not level geometry, and not `SwitchLaneExecutor.IsCompleted`. The primary bug is unsafe handoff between executor and replanner: a replan is allowed to replace an already fired in-progress head with an empty/different plan.

The faulty part is the generic `trigger-reachable` revalidation. It compares an old action contract with a projected snapshot and applies input-action semantics to no-input actions like `PassiveCollect`. In this deterministic level flow, committed-prefix actions should be trusted and simulated, not re-proven with a separate trigger check.

## Proposed Solution

Remove generic committed-prefix trigger revalidation.

Recommended architecture:

1. Keep committed-prefix as `head + next action`.
2. During replan, project/simulate committed-prefix directly.
3. Do not call a separate `ShouldRetainPendingCommittedAction` / `IsTriggerStillReachable` check before simulation.
4. Add a guard in `PlanExecutor.SetPlan` or at the call site:
   - if an action is in progress, a new plan with empty/different head must not silently reset `_isActionInProgress`;
   - replacing an in-progress head should require an explicit cancellation path.
5. Treat empty live-root plan during active-action handoff as suspicious. It can mean "continue running" in normal planning, but it must not be allowed to erase a physical action already started.

This keeps the logic simple: committed-prefix is trusted as the already chosen short execution contract; the planner rebuilds only after simulating it.

## Verification

Performed:

- Added targeted handoff/replan diagnostics around `Completed|Fired`, committed-prefix projection, live-root fallback, and `SetPlan` preservation.
- `dotnet build LostCyberHamster/Assembly-CSharp.csproj --no-restore` succeeded. Existing `System.Net.Http` warnings remain.
- Ran `./tools/invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 120 -TimeScale 1`.
- The run reproduced the first damage and proved H1 with the diagnostic sequence above.
- Removed generic `trigger-reachable` committed-prefix revalidation from `RuntimeBotController`.
- `dotnet build LostCyberHamster/Assembly-CSharp.csproj --no-restore` succeeded after the removal. Existing warnings remain.

Validation after the fix should prove:

- After `CompletedThenNext ... nextResult=Fired ... newHead=SwitchLane`, replan keeps `SwitchLane` as head.
- No `SetPlan wasInProgress=True preserve=False oldHead=SwitchLane newHead=none/different`.
- The pending `PassiveCollect` is simulated as part of committed-prefix; there is no `trigger-not-reachable` fallback.
- Bot does not reach top `bigAlive` with empty plan / `pending=null`.
