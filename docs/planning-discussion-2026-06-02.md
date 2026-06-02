# Planning discussion — 2026-06-02

> Source: chat conversation (Russian). This note captures the agreed conceptual model and a concrete walk-through on current code.

## Context / goal
We discussed simplifying the planning model: reducing specialized chain-builders (especially for targets) by moving toward a single `DecisionPoint` that carries an `ObstacleChain` where each obstacle is annotated with “features” (threat/opportunity/roof/target, lane, etc.). Strategies would use these features to generate actions; the tree builder would expand branches over all safe actions; the evaluator would pick the best branch.

Key objective: ensure the planner can still build profitable multi-step branches (e.g., *jump to roof → later do a beneficial action*) while keeping the system simpler.

## What the current tree builder does
`PlanningGraphBuilder` recursively explores possible actions from a projected `PlanningState`:
- candidates: `_actionGenerator.Generate(currentNode.State, worldSnapshot)`
- for each candidate: `_transitionSimulator.Simulate(...)` → recurse
- adds leaf branches when it’s allowed to stop expanding

The current code also uses required-decision detection to decide whether “stopping” is allowed:
- `HasUnresolvedRequiredDecision(...)` calls `DecisionPointDetector.TryDetectRequiredDecisionPoint(...)` on a projected snapshot.
- If there is **no** unresolved required decision, a leaf branch is added even if optional candidates exist (comment: *Optional planning interests must stay skippable*).

## Existing chain grouping rules (already in code)
The “gap < hamster width → same chain” rule exists:
- `ThreatChainCollector`: expands chain while `gap < planningState.Hamster.Width`.
- Roof passive continuation uses `hamster.Width * PassiveContinuationGapFactor` (`RoofRunProjection`).
- Obstacles are collected/sorted by `LeftX` in `SnapshotBuilder`.

So the basic operations needed for a unified chain builder (sorting + gap-based grouping) already exist, but are currently split across threat/roof components.

## How JumpOn completion safety is handled (current code)
`JumpOnStrategy` does **two-stage gating**:
1) Finds a `fireShift` via `JumpOnFireWindowFinder` (analytic window + runtime resolver confirmation at resolver point).
2) Before adding the action, checks post-completion safety:

```csharp
float completionWorldShift = fireShift + travel.ActionTravel;
if (!TargetRemovalPostActionSafety.IsSafeAfterCompletion(
        planningState,
        worldSnapshot,
        window.TargetObstacleIndex,
        window.TargetObstacle.InstanceId,
        completionWorldShift))
{
    return; // do not add JumpOn
}
```

So `JumpOn` can be rejected if the *end of the full action* would be unsafe due to upcoming obstacles.

## Concrete walk-through scenario (as discussed)
**Scenario** (bottom lane; top lane empty):
- bottom: `bigNotAlive` with roof (threat + roof opportunity)
- later: `smallAlive` (jump-on target)
- after/at completion: `smallNotAlive` located such that the *completion/landing/"bounce"* after `JumpOn` would intersect it

**Root node action generation**:
- `SwitchLane` is generated (safe window; top lane is empty).
- `JumpOnRoof` on `bigNotAlive` is generated if a safe fire window exists.
- `JumpOn` onto `smallAlive` is attempted but **rejected** by `TargetRemovalPostActionSafety.IsSafeAfterCompletion(...)` because completion would collide with `smallNotAlive`.

**Branches built**:
- Branch A: `[SwitchLane]` → reaches a safe state; with no immediate obstacles on the top lane it becomes a leaf.
- Branch B: `[JumpOnRoof]` → transitions to `RoofRun` state; may require/enable further roof-related actions (e.g., `PassiveRoofExit` depending on strategy set), generally costing more energy/steps.

**Select best**:
- The `JumpOn` branch does not exist (action not generated).
- Between Branch A and Branch B, the evaluator will typically pick `SwitchLane` because it’s safe and usually cheaper than roof path in this configuration.

**Runtime outcome**:
- The executor receives the head of the best branch: `SwitchLane`. Bot switches to the top lane and continues running until replanning.

## Simplification direction (summary)
A valid simplification is to:
- keep a single chain builder that produces an obstacle chain annotated with features (lane + roles)
- let strategies generate safe actions from that chain
- rely on the existing safety gating patterns (e.g., `TargetRemovalPostActionSafety`) to prevent unsafe “profitable” actions

If required/optional gating is removed, the system still needs a clear rule for when “doing nothing” is acceptable; one way is to model it explicitly or ensure safety checks prevent the planner from selecting empty/unsafe continuations.
