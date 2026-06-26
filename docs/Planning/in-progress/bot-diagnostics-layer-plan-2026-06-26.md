# Bot diagnostics layer plan — 2026-06-26

## Scope

- Goal: centralize bot diagnostic logging behind typed helpers while keeping domain logic as the source of diagnostic reasons.
- Current state: `DebugManager` is the file sink, but many bot logs are hardcoded directly in planners, strategies, executors and runtime controller.
- Non-goal: change planning, strategy selection, execution timing or level geometry.

## Design

- Keep `DebugManager` as low-level transport: timestamp, file path, channel and file IO.
- Add `Assets.Scripts.Bot.Diagnostics` layer:
  - `BotDiagnosticLevel`: `Essential`, `Verbose`, `Trace`.
  - `BotDiagnosticCategory`: execution, planning, branch selection, dead-end, strategy, replan, runtime safety, pattern, economy, test result.
  - `BotDiagnostics`: central category/level gate and write methods.
  - Thematic helpers:
    - `BotExecutionDiagnostics`
    - `BotRuntimeEventDiagnostics`
    - `BotReplanDiagnostics`
    - `BotStrategyDiagnostics`
- Stable code paths should call diagnostics helpers directly. The helpers check category and level before expensive string formatting.
- Strategy/planning code must still compute and return reasons; diagnostics only records existing facts and reasons.

## Levels

- Essential: test result, damage/dead-end summary, applied plan, action fire/complete/cancel, energy economy.
- Verbose: strategy rejection, branch choice, window diagnostics, accepted risky-but-valid route details.
- Trace: per-candidate/per-obstacle traces when a regression requires deep execution path proof.

## Implementation steps

1. Add central diagnostics primitives. Done.
2. Convert reusable existing diagnostics. Done:
   - `HamsterActionLogger` -> `BotExecutionDiagnostics`.
   - `RuntimeBotEventTracker` -> `BotRuntimeEventDiagnostics`.
3. Convert stable runtime/planning call sites. Done for:
   - plan chain and plan result summary;
   - dead-end summary;
   - pattern spawn/detail;
   - strategy rejection context where the reason already exists.
4. Leave large one-off investigation formatters in place for now, but route their output through `BotDiagnostics` so they are gated consistently. Done.
5. Run compile validation. Done: `dotnet build LostCyberHamster/Assembly-CSharp.csproj -v:minimal`.

## Validation

- `dotnet build LostCyberHamster/Assembly-CSharp.csproj -v:minimal`: passed with existing Unity/package warnings, 0 errors.

## Follow-up cleanup

- Move the large `PlanBuilder` investigation formatters to `BotPlanningDiagnostics` or delete them after current regression work is finished.
- Replace remaining direct `DebugManager.DiagLog*` calls in bot code by category-specific helpers.
- Add a small doc section listing categories and recommended enablement for regression investigation modes.
