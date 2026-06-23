# NY Coin Collection Regression Analysis

Дата: 2026-06-24

## Scope

Регресс: на `01_New_York/Morning/level_01` и `01_New_York/Morning/level_02` бот не всегда подбирает монетку, хотя монетка доступна безопасно, без траты энергии и без более ценной альтернативы.

## Expected

Если coin collectable можно подобрать безопасно, бесплатно и без вытеснения более ценной цели, ветка со сбором coin должна выигрывать у равной ветки без coin.

Authoritative source:
- описание пользователя;
- `docs/Planning/in-progress/ny-regressions-plan-2026-06-24.md`;
- контракт `PlanningBranchMetricsComparer`: `CoinCollectibleValue` улучшает ветки, равные по более важным критериям.

## Commands

- `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 600 -TimeScale 1`
- `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_02' -TimeoutSeconds 600 -TimeScale 1`

## Evidence

- Baseline after handoff fix:
  - `Temp/ny_regressions_2026-06-24/2026-06-24_223609_level_01_coin_trace.txt`.
  - `level_01`: `WIN level=1 stars=3`, `damage=0`, `deadend=0`.
  - coin facts: `PassiveCollect[Coin]` appeared in plans, but actual `CollisionController collect obstacle=collectableCoin` was `4`.
  - reachable collectables in `level_01` patterns:
    - `easy_run` has coin id `3`;
    - `medium_difficulty` has coin id `6`;
    - `medium_difficulty_energy` has coin id `6`;
    - sequence gives 6 reachable coins before finish; only 4 were collected.
- Temporary diagnostic pass:
  - temporary `COIN_DIAG` added in `PlanBuilder.Build` to print best branch, best coin branch, metrics and action target ids.
  - log copy: `Temp/ny_regressions_2026-06-24/2026-06-24_224317_level_01_coin_diag.txt`.
  - key line 474:
    - visible coin: `-472146@12.32:top`;
    - best branch: `SwitchLane#-472170 -> JumpOn#-472100`;
    - best coin branch: `PassiveCollect#-472146[Coin:1] -> SwitchLane#-472170 -> JumpOn#-472100`;
    - displayed metrics were equal on energy, energy-before-major, major count and energy collectable value, and coin branch had `coin=1`;
    - `compareCoinToBest=1`, so the coin branch still lost.
- Code path proving the cause:
  - `PlanningBranchMetricsComparer.Compare` compares `ImmediateTargetEliminationCount` before `CoinCollectibleValue`.
  - `PlanningBranchMetrics.GetImmediateTargetEliminationCount` returned `0` as soon as it saw `action.FulfillsCollectibleObjective`.
  - Therefore `PassiveCollect[Coin]` at branch head masked the following `JumpOn` and made the coin branch lose to the no-coin branch, even though the route after coin was the same.
- Targeted fix:
  - `PlanningBranchMetrics` now treats `BotActionKind.PassiveCollect` as transparent in immediate route metrics.
  - This preserves the route comparison (`JumpOn` vs `JumpOver`) while allowing a free passive collectable before the same route action.
- Targeted verification:
  - `Temp/ny_regressions_2026-06-24/2026-06-24_224927_level_01_after_coin_fix.txt`:
    - `WIN level=1 stars=3`;
    - coin fires `6`, coin collects `6`, damage `0`, deadend `0`;
    - no `COIN_DIAG/HANDOFF_DIAG`.
  - `Temp/ny_regressions_2026-06-24/2026-06-24_225416_level_02_after_coin_fix.txt`:
    - `WIN level=2 stars=3`;
    - coin collects `6`, damage `0`, deadend `0`;
    - `SwitchLane -> JumpOnRoof` handoff remains present and `JumpOnRoof` fires in the previously failing segment.

## Hypotheses

- H1: `PassiveCollect` для coin не генерируется, потому что decision point закрыт required route situation.
- H2: `PassiveCollect` генерируется, но проигрывает в `PlanningBranchMetricsComparer` из-за horizon comparison или higher-priority metric.
- H3: ветка с coin строится, но async result устаревает или action отменяется до исполнения.
- H4: coin фактически собирается, но визуально выглядит пропущенной из-за другого collectable/lane/trigger.

## Root Cause

Root cause: passive collectable actions were included as blockers in immediate route metrics. A branch that safely collected a coin before continuing to the same `JumpOn` route got `ImmediateTargetEliminationCount=0`, while the branch that skipped the coin kept `ImmediateTargetEliminationCount=1`; because that metric is compared before `CoinCollectibleValue`, the free coin branch lost before coin value could be considered.

Excluded alternatives:
- Coin generation: `COIN_DIAG` showed a valid best coin branch with `PassiveCollect#<coin>[Coin:1]`.
- Safety/energy: compared branches had equal energy and energy-before-major in the targeted miss; coin collection cost was `0`.
- More valuable alternative: the no-coin and coin branches led to the same following route objective (`SwitchLane -> JumpOn`), so skipping coin did not buy an extra major objective in the targeted miss.
- Async/execution: after the metric fix, the same level collected all six coins without damage/dead-end.
