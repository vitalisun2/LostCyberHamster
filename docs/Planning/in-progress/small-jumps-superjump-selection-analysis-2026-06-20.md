# Small Jumps SuperJump Selection Analysis — 2026-06-20

## Scope

Один регресс: `01_New_York/Morning/level_01`, второе вхождение паттерна `small_jumps`.

Expected: в моменте с нижним `smallNotAliveRoad` и верхним `bigNotAlive` бот на нижней линии выбирает энергоэффективный `JumpOver` через `smallNotAliveRoad`.

Actual: бот выбирает `SuperJumpOver`, хотя обычный `JumpOver` должен быть достаточным.

Ограничение расследования: правки поведения пока не делать. Если нужен runtime-прогон, довести уровень только до проблемного места, поставить игру на паузу и читать накопленные логи; дальше уровень не прогонять.

## Sources

- Скриншот пользователя: `C:/Users/Vitaly/AppData/Local/Temp/codex-clipboard-c13f52b0-849d-4899-8432-a9f406590b34.png`.
- Проектные правила: `docs/rules/AGENTS.md`, `docs/rules/agent_tools.md`.
- Уровень: `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/level_01/level_01.json`.
- Паттерны: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.
- Runtime log: `LostCyberHamster/EditorLogs/diagnostic_log.txt`, запуск `2026-06-20 14:21-14:22`.
- Planning:
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanEvaluator.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningBranchMetrics.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/CollectibleValuePolicy.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/JumpOnObjectiveRules.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Planning/ObstacleClassifier.cs`
- Strategies:
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/JumpOver/JumpOverPolicy.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/SuperJumpOver/SuperJumpOverPolicy.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOver/JumpOverChainCalculator.cs`
  - `LostCyberHamster/Assets/Scripts/Bot/Strategies/Shared/JumpOver/JumpOverFireWindowFinder.cs`

## Hypotheses

### H1 — `JumpOver` action вообще не генерируется для целевого `smallNotAliveRoad`

Что подтвердит: в `JumpOverStrategy`/spec/fire-window для ближайшего нижнего `smallNotAliveRoad` есть фильтр или geometry-check, который возвращает no action, а `SuperJumpOver` при этом проходит.

Что опровергнет: код или лог покажет, что `JumpOver` был сгенерирован кандидатом для того же состояния, но проиграл ранжированию.

Статус: rejected. Диагностический лог показал successful branches с первым action `JumpOver`, например `branch#14 first=JumpOver ... chain=JumpOver->PassiveCollect[Energy:30]->JumpOn[JumpOnObjective]...`. Значит обычный jump-over генерируется и проходит safety.

### H2 — оба действия генерируются, но ранжирование предпочитает `SuperJumpOver`

Что подтвердит: `PlanEvaluator` или сортировка candidates выбирает `SuperJumpOver` несмотря на больший energy/tap cost, например из-за objective priority, branch depth, chain result или стабильного порядка.

Что опровергнет: `JumpOver` отсутствует среди candidates или отфильтрован до ранжирования.

Статус: confirmed. Среди successful branches есть и `JumpOver`, и `SuperJumpOver`; `SuperJumpOver` выигрывает из-за objective metrics до сравнения `TotalEnergyCost`.

### H3 — `SuperJumpOver` таргетит не только нижний `smallNotAliveRoad`, а цепочку/следующую угрозу, поэтому выглядит дороже, но считается более полным планом

Что подтвердит: `SuperJumpOver` chain включает несколько obstacles или решает верхний/нижний последующий required threat, тогда как `JumpOver` оставляет immediate unresolved situation.

Что опровергнет: оба действия имеют один и тот же target/chain end и отличаются только типом прыжка.

Статус: rejected for root cause. `SuperJumpOver` не нужен для самого нижнего `smallNotAliveRoad`: `JumpOver`-ветки существуют и тоже продолжают безопасный план. Отличие не в покрытии текущей угрозы, а в том, как последующий energy collectable классифицируется по priority.

### H4 — `JumpOver` отклоняется из-за неверной модели геометрии/окна срабатывания на границе паттерна

Что подтвердит: фактическая позиция/ширина obstacles из `small_jumps` плюс параметры `JumpOver` дают no valid fire window из-за min/max range, retained shift или overlap, хотя runtime-прыжок визуально кажется достаточным.

Что опровергнет: calculation показывает валидное окно обычного `JumpOver`.

Статус: rejected. Runtime-equivalent `JumpOver` branches были построены; значит окно обычного прыжка и resolver подтверждают действие.

## Facts

- `level_01.json` содержит два последовательных вхождения `small_jumps`; скрин с подписью `small_jumps 2` соответствует второму вхождению.
- На скриншоте runtime HUD показывает выбранный action: `SuperJumpOver, isDamaged: False`.
- В `small_jumps` проблемный участок: нижний `smallNotAliveRoad` at x≈28.8, верхний `bigNotAlive` at x≈30.2, сразу после нижнего obstacle стоит `collectableEnergetic` at x≈33.0.
- `JumpOverPolicy.EnergyCost = 10`; `SuperJumpOverPolicy.EnergyCost = 20`.
- `ObstacleClassifier.CanJumpOverOnGround()` разрешает `smallNotAliveRoad`; `CanSuperJumpOverOnGround()` тоже разрешает `smallNotAliveRoad`.
- `PlanEvaluator.CompareBranches()` сначала сравнивает `Metrics.CompareObjectivePriority()`, и только потом `TotalEnergyCost` и `TapCount`.
- `PlanningBranchMetrics.CompareObjectivePriority()` сравнивает `CriticalEnergyCollectibleValue` раньше обычного `EnergyCollectibleValue` и раньше стоимости энергии.
- `CollectibleValuePolicy.TryGetEnergyValue()` помечает energy collectable как critical, если projected `hamster.Energy <= JumpOnObjectiveRules.HighPriorityEnergyThreshold`; threshold = `40`.
- Runtime log before diagnostic confirmed the visible regression path:
  - `[Bot PLAN] SwitchLane -> SuperJumpOver -> PassiveCollect[Energy:30] ...`
  - after switch completion: `[Bot PLAN] SuperJumpOver -> PassiveCollect[Energy:30] -> SuperJumpOn ...`
  - `[Bot EXEC] FIRE kind=SuperJumpOver ... desc=Super jump over smallNotAliveRoad`.
- Temporary diagnostic run was paused before completing the level. It logged successful competing branches for the upcoming tail:
  - `branch#14 first=JumpOver energy=20 ... critEnergy=0 energyValue=60 chain=JumpOver->PassiveCollect[Energy:30]->JumpOn[JumpOnObjective]...`
  - `branch#49 first=SuperJumpOver energy=30 ... critEnergy=30 energyValue=60 chain=SuperJumpOver->PassiveCollect[Energy:30]->JumpOn[JumpOnObjective]...`
  - Since `critEnergy=30` is compared before total energy cost, the super branch wins even though it spends more energy.
- Temporary diagnostic code was removed after collecting the facts; no behavior fix was left in source.

## Root Cause

Root cause: critical-energy priority is calculated from projected energy after the candidate action has already spent energy.

At the problematic point, both actions safely solve the same lower-lane `smallNotAliveRoad`:

- `JumpOver` spends 10 energy.
- `SuperJumpOver` spends 20 energy.

The next `collectableEnergetic` is then evaluated in the projected branch. Because `SuperJumpOver` spends an extra 10 energy first, it can push projected energy to or below the `<= 40` critical threshold. The same energy pickup becomes `CriticalEnergyCollectibleValue=30` in the super branch, while it remains ordinary `EnergyCollectibleValue` in the normal jump branch.

`PlanEvaluator` treats critical energy as a higher objective than energy cost. So the bot is not choosing `SuperJumpOver` because it needs the super jump to cross the `smallNotAliveRoad`; it chooses it because the extra energy spend makes the following energy pickup look more important.

In simple terms: the planner rewards the bot for wasting energy right before an energy can, because that waste turns the can into a "critical" pickup.

## Proposed Solution

Do not let an action manufacture critical-energy priority by spending extra energy immediately before the pickup.

Preferred fix direction:

- Evaluate energy collectable criticality against a stable baseline energy for the branch segment, not against energy after avoidable action costs.
- For an energy pickup, separate "effective energy gain" from "critical pickup urgency":
  - effective gain can still use projected energy after costs, because it answers how much energy will actually be restored;
  - critical urgency should use energy before the current decision action, or another monotonic baseline that cannot be lowered by choosing a more expensive equivalent action.
- Then `JumpOver + Energy` and `SuperJumpOver + Energy` will have the same urgency for the same can, and existing `TotalEnergyCost` tie-breaker will choose `JumpOver`.

Alternative local fix, less general:

- Add a dominance/preference rule for same-target over actions: if `JumpOver` and `SuperJumpOver` solve the same blocking threat and do not improve required safety/objective coverage, prefer `JumpOver` before branch-level objective comparison.
- This would fix this symptom, but it leaves the broader "spend energy to make energy critical" scoring bug for other action pairs.

## Verification

- Ran a diagnostic launch of `01_New_York/Morning/level_01`.
- The run was intentionally paused at the relevant planner decision and ended by automation timeout; no full-level result was collected.
- Diagnostic facts confirmed root cause; no behavior fix was implemented in this investigation.
