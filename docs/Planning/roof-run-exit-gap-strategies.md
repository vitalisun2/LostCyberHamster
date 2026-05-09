# Roof Run Exit Gap Strategies

## Purpose

Документ фиксирует planning-модель для ситуаций, когда хомяк находится в `RoofRun`, runtime может автоматически продолжить бег по крышам или сбежать с крыши, а бот должен решить, требуется ли действие до схода.

Базовое правило архитектуры: финальная валидность любого action проверяется через runtime resolver. Геометрические расчеты ниже используются только для классификации ситуации и выбора candidate fire window.

## Runtime Facts

- `RoofRunMechanics` проверяет следующую roof, когда правая грань хомяка проходит правый край текущей roof на `hamster.Width * 0.7f`.
- Если в этот момент следующая roof пересекается с хомяком, runtime продолжает `RoofRun` на ней.
- Если следующей passive roof нет, runtime переводит хомяка в `RunFromRoof`.
- `RunFromRoof` длится по клипу `transform_run_from_roof`; его horizontal travel должен считаться через тот же подход, что остальные action travel: clip duration * `Consts.GameSpeedBase`.

## Terms

`lastRoof` - последняя roof, до которой runtime может дойти автоматически из текущего `RoofRun`.

`nextObstacle` - первый relevant obstacle после `lastRoof` на той же lane.

`gap`:

```text
gap = nextObstacle.LeftX - lastRoof.RightX
```

`passiveRoofGap`:

```text
passiveRoofGap = hamster.Width * 0.7f
```

Этот порог применяется только к roof-to-roof passive continuation.

`runFromRoofTravel`:

```text
runFromRoofTravel = world shift of transform_run_from_roof
```

Этот travel применяется для оценки, успеет ли runtime завершить сход с крыши до контакта со следующим obstacle.

## Decision Point Model

`DecisionPoint.Chain` остается обычной цепочкой blocking obstacles после последней passive roof.

Крыши, которые runtime проходит пассивно, не входят в `ObstacleChain`. Для них нужен небольшой helper в `RoofRunProjection`, который находит только `lastRoof`, а не создает отдельную модель списка крыш.

Ожидаемый helper:

```csharp
TryFindLastPassiveRoof(
    PlanningState planningState,
    WorldSnapshot projectedWorldSnapshot,
    out ObstacleSnapshot lastRoof,
    out int lastRoofIndex)
```

После этого detector ищет обычный `DecisionPoint` начиная после `lastRoofIndex`.

## Situations

### 1. Passive Roof-To-Roof Gap

Conditions:

- `nextObstacle` is roof: `bigNotAlive` or `mediumNotAlive`;
- `gap <= passiveRoofGap`.

Meaning:

Runtime continues `RoofRun` on the next roof without bot action.

Planning:

No strategy action. `RoofRunProjection` treats this roof as passive continuation and continues searching farther.

### 2. Dangerous Roof-To-Roof Gap

Conditions:

- `nextObstacle` is roof;
- `gap > passiveRoofGap`;
- `gap < runFromRoofTravel`.

Meaning:

Runtime cannot passively continue `RoofRun`, but simple `RunFromRoof` may collide with the next roof before the descent finishes.

Planning:

Use roof-to-roof jump strategies:

- `JumpFromRoofOnRoofStrategy`;
- `SuperJumpFromRoofOnRoofStrategy`.

These strategies choose a candidate fire shift before automatic descent and validate it through the runtime roof jump resolver. Expected outcome: landing on the target roof, not damage and not wrong target.

### 3. Safe Roof-To-Ground Gap Before Next Roof

Conditions:

- `nextObstacle` is roof;
- `gap >= runFromRoofTravel`.

Meaning:

Runtime can finish `RunFromRoof` before the next roof becomes dangerous.

Planning:

No roof-from action. After the hamster returns to `Run`, ordinary ground/roof-on-ground planning handles the next obstacle.

### 4. Dangerous Gap To Non-Roof Obstacle

Conditions:

- `nextObstacle` is not roof;
- `nextObstacle` is dangerous for ground contact;
- `gap < runFromRoofTravel`.

Meaning:

Runtime may collide with the road obstacle during `RunFromRoof`, before ordinary ground strategies can act.

Planning:

Use roof-to-road jump strategies:

- `JumpFromRoofStrategy`;
- `SuperJumpFromRoofStrategy`.

These strategies use the ordinary `DecisionPoint.Chain` built after `lastRoof` and decide whether one roof jump can clear one or more obstacles. Final action validity must be checked through the runtime roof jump resolver.

### 5. Safe Gap To Non-Roof Obstacle

Conditions:

- `nextObstacle` is not roof;
- `gap >= runFromRoofTravel`.

Meaning:

Runtime can finish `RunFromRoof` and return to `Run` before the obstacle becomes dangerous.

Planning:

No roof-from action. The obstacle chain is handled later by ordinary ground strategies such as `JumpOver`, `SuperJumpOver`, `SwitchLane`, or future ground strategies.

## Implementation Notes

- Do not add safety margins until there is a concrete runtime mismatch that requires one.
- Do not mix passive roof continuation and blocking obstacle chain into one list.
- `passiveRoofGap` is not a general danger distance. It is only the roof-to-roof passive continuation threshold.
- `runFromRoofTravel` is the distance used to decide whether simple descent can finish before contact with `nextObstacle`.
- Distance checks only decide whether a strategy should be considered. A planned action is valid only when the runtime resolver confirms the expected outcome.
