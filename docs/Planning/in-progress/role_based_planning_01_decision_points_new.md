# Module plan: DecisionPointsNew

## Назначение

`DecisionPointsNew` заменяет сценарные builders одним role-based detector'ом. Его ответственность: описать текущую planning-ситуацию фактами, а не заранее выбрать сценарий действия.

## Факты по текущему коду

- Старый `DecisionPoint` хранит `Kind`, `IsDecisionRequired`, `FireBeforeObstacle` и compatibility-ссылки на первый obstacle.
- Старый `ObstacleChain` хранит `Obstacles` + `Indices` и содержит методы поиска roof/target под конкретные сценарии.
- `ThreatChainCollector` строит same-lane chain и завершает её при `gap >= hamster.Width`, но дополнительно ограничивает длину `MaxChainLength = 3`.
- `ObstacleClassifier` уже является factual source:
  - roof support: `bigNotAlive`, `mediumNotAlive`;
  - ground damage: `smallAlive`, `bigAlive`, `smallNotAliveRoad`, `smallNotAliveRoadAndRoof`, `bigNotAlive`, `mediumNotAlive`;
  - ground jump-on target: `smallAlive`;
  - roof-exit jump-on target: `smallAlive`, `bigAlive`.
- `PlanningState.FromSnapshot` уже пропускает текущий roof support, если хомяк стоит на этой крыше.
- Старый detector отдельно пропускает passive roof chain через `RoofRunProjection.TryFindLastPassiveRoof`; это roof-specific правило нужно перенести осознанно, а не через общий `required` flag.

## Целевая модель

Папка/namespace: `Assets/Scripts/Bot/Planning/DecisionPointsNew`.

Типы:

- `ObstacleRole` flags: `BlockingThreat`, `RoofSupport`, `Target`, `RoofOccupantHazard`, `Collectible`.
- `ObstacleChainElementNew`: `ObstacleSnapshot Obstacle`, `int WorldIndex`, `ObstacleRole Roles`, helpers `HasRole(...)`, `HasAnyActivePlanningRole`.
- `ObstacleChainNew`: one-line список elements, `FocusBottomLine`, `First`, `FirstIndex`, `LeftX`, `RightX`, role-based lookup helpers.
- `DecisionPointNew`: один `ObstacleChainNew`; без `DecisionPointKind`, `IsDecisionRequired`, `FireBeforeObstacle`.
- `ObstacleRoleClassifierNew`: тонкий adapter над `ObstacleClassifier`, без дублирования type rules.
- `ObstacleChainBuilderNew`: собирает one-line chain по focus lane.
- `DecisionPointDetectorNew`: выбирает focus lane и строит point.

Суффикс `New` временный для side-by-side миграции. После удаления старого path нужно рассмотреть переименование в нейтральные имена.

## Role rules

- `BlockingThreat`: `ObstacleClassifier.DamagesOnGroundContact(type)`.
- `RoofSupport`: `ObstacleClassifier.IsObstacleWithRoof(type)`.
- `Target`: obstacle может быть целью хотя бы одной jump-on стратегии: `CanJumpOnGroundObstacle(type) || CanJumpOnFromRoofObstacle(type)`.
- `RoofOccupantHazard`: obstacle опасен на passive roof path; вычисляется с участием `RoofRunProjection`, потому что зависит от текущего roof state.
- `Collectible`: зарезервировано, пока не участвует в active chain.

Важно: generic `Target` не означает, что любая стратегия сейчас может выполнить jump-on. Стратегия всё равно проверяет состояние хомяка, окно и post-action safety.

## Focus lane rule

Private flow внутри `DecisionPointDetectorNew`:

1. Если нет валидного `PlanningState`, `Hamster` или `WorldSnapshot`, point не строится.
2. Если `CanUseTargetFocus(hamster) == false`, focus lane = текущая projected lane.
3. Если `CanUseTargetFocus(hamster) == true`, detector сканирует обе линии до `VisionRightEdgeX` и ищет ближайший huntable target.
4. Huntable target фильтруется по текущему состоянию:
   - `Run`: `CanJumpOnGroundObstacle`;
   - `RoofRun`: `CanJumpOnFromRoofObstacle`;
   - остальные состояния на первом этапе не включают target scan.
5. Если target не найден, focus lane = текущая lane.
6. Если ближайший target на текущей lane, focus lane = текущая lane.
7. Если ближайший target на другой lane, focus lane = lane target.
8. При равной дистанции current lane выигрывает, чтобы не создавать лишний switch.

`Hamster.IsOnRoof` сам по себе не запрещает target scan: roof state влияет только на то, какие target'ы huntable.

## Target focus predicate

`CanUseTargetFocus(hamster)` - не общий признак "бот выполняет action". Это локальный predicate выбора focus lane для target-hunt.

Правило:

- `hamster != null`;
- `hamster.Energy >= JumpOnObjectiveRules.HighPriorityEnergyThreshold`;
- `hamster.IsShifting == false`;
- `hamster.HamsterState == Run || hamster.HamsterState == RoofRun`.

Роль `IsShifting`: это runtime-факт незавершённой lane transition после tap. В этот момент `IsOnBottomLine` уже мог измениться, но физический переход ещё не завершён, поэтому target-focus не должен выбирать новую off-lane цель поверх текущего перехода.

Важно: `DecisionPointDetectorNew` не должен иметь общего guard'а "если сейчас выполняется action, ничего не делать". Во время in-progress head-action `PlanBuilder` уже строит projected state через `ActionInProgressProjector`; detector должен работать по этому projected state и продолжать строить lookahead.

## Chain rule

- Chain начинается с ближайшего active obstacle на focus lane впереди хомяка, начиная с `PlanningState.NextObstacleIndex`.
- Active obstacle = obstacle с хотя бы одной active planning role, исключая `None` и pure `Collectible`.
- Если focus lane выбрана из-за target, chain всё равно начинается с ближайшего active obstacle этой lane, чтобы pre-target угрозы попали в ветку.
- Chain включает только focus lane.
- Chain расширяется следующими active obstacles той же lane, пока `gap < hamster.Width`.
- При `gap >= hamster.Width` следующий obstacle считается отдельной будущей ситуацией.
- `MaxChainLength = 3` из старого `ThreatChainCollector` не переносим: chain определяется gap-инвариантом и видимым snapshot horizon.

## Что переиспользовать

- `ObstacleClassifier` как единственный source type facts.
- `PlanningState.NextObstacleIndex`.
- `RoofRunProjection` для passive roof continuation и roof occupant hazard.
- `WorldSnapshot.Obstacles` sorting от `SnapshotBuilder`.

## Что не делать

- Не генерировать actions.
- Не выбирать лучшую ветку.
- Не добавлять `required/optional` split.
- Не включать обе линии в chain.
- Не переносить `GroundJumpOnTargetChainComposer` / `JumpOnFromRoofTargetChainComposer`.
- Не добавлять поле `HasRequiredThreat`: необходимость действия определяется наличием point и действиями/leaf-rule graph.

## Валидация будущей реализации

- Без target и energy `< 40`: focus lane = current lane.
- Energy `>= 40`, ближайший huntable target на current lane: focus lane остаётся current lane.
- Energy `>= 40`, ближайший huntable target на другой lane: focus lane = target lane.
- `CanUseTargetFocus == false`: focus lane не переключается ради target.
- Gap `< hamster.Width` объединяет obstacles в chain; `gap >= hamster.Width` останавливает chain.
