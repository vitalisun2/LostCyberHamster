# Collectible Evaluator Simplification Plan — 2026-06-20

## Scope

Доработать оценку bot planning branches после регресса в `01_New_York/Morning/level_01`, второе вхождение `small_jumps`: бот выбрал `SuperJumpOver` вместо энергоэффективного `JumpOver` перед `collectableEnergetic`.

Правки поведения должны быть смысловыми, без временных обходов:

- не использовать `CriticalEnergy` как отдельный priority bucket;
- не позволять ветке повышать ценность energy pickup через предварительную лишнюю трату энергии;
- сохранить простую модель сравнения веток.

## Target Behavior

Successful branch уже должна быть безопасной: ветки с damage не должны попадать в обычные successful candidates.

Оценка successful branches:

1. `lifeGain desc`
   - life считается только если `lives < 3`, что уже делает `CollectibleValuePolicy`.
2. `majorObjectiveCount desc`
   - `JumpOn` objective;
   - полезный energy pickup;
   - crystal pickup.
3. `totalEnergyCost asc`
4. `coinCount desc`
5. `tapCount asc`

Energy, crystal и jump-on target равны по уровню: важен суммарный count major objectives на ветке, а не отдельный critical mode.

Coin остается низким приоритетом: бесплатные coins улучшают ветку при равной стоимости, но coin не должен выигрывать у более энергоэффективной ветки.

## Implementation Steps

1. Упростить `CollectibleObjectiveValue`
   - убрать `IsCriticalEnergy`;
   - оставить только `Kind`, `EffectiveGain`, `HasValue`.

2. Упростить `CollectibleValuePolicy`
   - energy pickup остается positive, когда есть positive effective gain;
   - больше не выставлять `isCriticalEnergy`.

3. Переписать `PlanningBranchMetrics`
   - убрать `CriticalEnergyCollectibleValue`;
   - добавить/использовать `MajorObjectiveCount`;
   - major objective increments:
     - `+1` за `FulfillsJumpOnObjective`;
     - `+1` за energy collectable;
     - `+1` за crystal collectable;
   - life и coin оставить отдельными buckets.

4. Упростить `PlanEvaluator`
   - branch compare: `CompareObjectivePriority()` -> `TotalEnergyCost` -> `CoinCollectibleValue` -> `TapCount`;
   - score для UI/diagnostics привести к той же семантике без critical multiplier.

5. Обновить code references
   - убрать сравнение `IsCriticalEnergy` в `PlannedAction.IsEquivalentTo`;
   - убрать публичный passthrough `CriticalEnergyCollectibleValue` из `PlanningBranch`, если он больше не нужен.

6. Проверка
   - `dotnet build LostCyberHamster/Assembly-CSharp.csproj`;
   - запустить `01_New_York/Morning/level_01`;
   - дойти до проблемного места и убедиться по BOT log, что вместо `SuperJumpOver` выбран `JumpOver`;
   - уровень дальше проходить не обязательно, если нужный факт уже зафиксирован.

## Risks

- Если где-то внешний код читает `CriticalEnergyCollectibleValue` или `IsCriticalEnergy`, compile покажет это сразу.
- Если ветка с energy pickup теперь выигрывает по major count раньше energy cost, это ожидаемая простая модель; текущий регресс всё равно исправляется, потому что `JumpOver` и `SuperJumpOver` получают одинаковые objective buckets и расходятся по `TotalEnergyCost`.

## Verification Notes

2026-06-20:

- `dotnet build LostCyberHamster\Assembly-CSharp.csproj --no-restore /clp:ErrorsOnly`
  - Result: succeeded.
- `dotnet build LostCyberHamster\Assembly-CSharp-Editor.csproj --no-restore /clp:ErrorsOnly`
  - Result: succeeded.
- `LostCyberHamster\Assembly-CSharp-Editor.csproj` includes `Assets\Editor\Tests\EditMode\PlanEvaluatorTests.cs` after Unity project-file regeneration.
- Added focused EditMode coverage for `PlanEvaluator.SelectBest`:
  - equal major objectives prefer lower `totalEnergyCost`;
  - coin does not justify extra energy cost;
  - free coin wins at equal energy cost.
- Final cleanup removed unused `JumpOnObjectiveCount` after `MajorObjectiveCount` became the only major-objective bucket.
- Both `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` were rebuilt again after the cleanup.
- Unity run:
  - command: `.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/level_01' -TimeoutSeconds 180 -TimeScale 1`;
  - diagnostic log: `LostCyberHamster/EditorLogs/diagnostic_log.txt`.
- Regression spot verification:
  - after `Switch lane before bigNotAlive`, BOT plan became `JumpOver -> PassiveCollect[Energy:30] -> ...`;
  - executed action was `FIRE kind=JumpOver ... desc=Jump over smallNotAliveRoad`;
  - then executed `PassiveCollect[Energy:30] ... collectableEnergetic`.
  - Expected/actual for scoped regression: `JumpOver`, not `SuperJumpOver`.
- Full run continued past the scoped spot and later ended with `[TEST RESULT] FAIL` on the next `small_jumps` fragment:
  - later action: `FIRE kind=SuperJumpOver ... desc=Super jump over bigAlive`;
  - later damage: `obstacle=bigAlive ... lane=top`;
  - this is recorded as separate follow-up risk, not as the original `smallNotAliveRoad` regression.
