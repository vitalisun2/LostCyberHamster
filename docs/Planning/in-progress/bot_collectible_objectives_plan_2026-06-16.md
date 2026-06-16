# Bot collectible objectives plan — 2026-06-16

## Scope

Спроектировать охоту бота за collectables внутри текущего role-based planning graph без отдельного обходного pipeline.

В фокусе:
- `collectableLife`;
- `collectableEnergetic`;
- `collectablePizza`;
- `collectableCoin` и `collectableCrystal` как низший приоритет currency.

Не в фокусе этого плана:
- изменение runtime-правил подбора collectables;
- баланс численных весов экономики;
- исправление текущей семантики `collectableCoin`, который по runtime-spec сейчас вызывает `CrystallCollected(1)`.

## Проверенные факты

- `SnapshotBuilder` уже кладёт active spawned obstacles в `WorldSnapshot`, включая collectables, с `ObstacleType`, lane и collider bounds.
- Runtime collectable types: `collectableEnergetic`, `collectablePizza`, `collectableCrystal`, `collectableLife`, `collectableCoin`.
- `CollisionController` подбирает collectable до damage-ветки, если obstacle на той же линии и хомяк не damaged/dead.
- `collectableEnergetic` и `collectablePizza` вызывают `Hamster.AddEnergy()`; текущий gain — до `30`, с cap `100`.
- `collectableLife` добавляет до `1` жизни с cap `3`.
- В planning уже есть `ObstacleRole.Collectible`, но `ObstacleRoleClassifier` её не назначает.
- `ObstacleChainElement.HasAnyActivePlanningRole` сейчас отбрасывает элементы, у которых единственная роль `Collectible`; значит collectable сам по себе не создаёт `DecisionPoint`.
- `ActionGenerator` строит decision points для текущей и противоположной линии. Для opposite lane он просит `SwitchLaneStrategy` создать entry action.
- `PassiveRoofExitStrategy` уже показывает локальный паттерн no-input strategy: planned action не нажимает кнопку, но симулирует полезный transition.
- `PlanningBranchMetrics` и `PlanEvaluator` сейчас учитывают jump-on objective, energy cost, tap count и action count; collectable value отсутствует.

## Целевое поведение

1. Бот продолжает считать безопасность главным инвариантом: unsafe ветки не выбираются ради collectable.
2. Если collectable достижим на текущей траектории без input, planner может добавить no-input step `PassiveCollect`.
3. Если collectable на другой линии, graph строит ветку через existing `SwitchLane` entry:
   ```text
   SwitchLane -> PassiveCollect
   ```
4. Если collectable достижим через крышу, graph строит ветку существующими действиями:
   ```text
   JumpOnRoof -> PassiveCollect
   ```
   или
   ```text
   JumpOnRoof -> PassiveRoofExit -> PassiveCollect
   ```
   в зависимости от runtime-положения collectable и состояния хомяка.
5. `PlanEvaluator` ранжирует collectables ниже jump-on objective, но выше обычной экономии, если ветки безопасны.

## Архитектурное решение

Не делать три стратегии под life/energy/currency.

Сделать одну стратегию:

```text
PassiveCollectStrategy
```

Она отвечает только за вопрос:

```text
Можно ли безопасно продолжить текущее движение до collectable и подобрать его без input?
```

Ранжирование вынести в отдельные policy/classifier типы:

```text
CollectibleClassifier
CollectibleValuePolicy
CollectibleObjectiveValue
```

Так strategy остаётся механикой подбора, а evaluator решает ценность ветки.

## Приоритеты collectables

Порядок сравнения successful planning branches:

1. JumpOn objective.
2. Life collectable value.
3. Energy collectable value.
4. Currency collectable value.
5. Total energy cost.
6. Tap count.
7. Action count.

Правила value:

- `collectableLife`: value > 0 только если `hamster.Lives < 3`; gain = `min(1, 3 - Lives)`.
- `collectableEnergetic` / `collectablePizza`: value > 0 только если `hamster.Energy < 100`; gain = `min(30, 100 - Energy)`.
- `collectableCoin` / `collectableCrystal`: минимальный positive currency value.
- Если effective gain равен `0`, strategy не должна создавать objective action ради этого collectable.

## Изменения модели данных

### 1. CollectibleKind

Добавить enum рядом с planning/domain-классификаторами:

```csharp
internal enum CollectibleKind
{
    None,
    Life,
    Energy,
    Currency
}
```

### 2. CollectibleObjectiveValue

Добавить value object:

```csharp
internal readonly struct CollectibleObjectiveValue
{
    public CollectibleKind Kind { get; }
    public int EffectiveGain { get; }
}
```

Смысл `EffectiveGain`:
- для life — число реально добавляемых жизней;
- для energy — число реально добавляемой энергии;
- для currency — условная ценность pickup.

### 3. PlannedAction

Расширить `PlannedAction` полями:

```text
CollectibleKind CollectibleKind
int CollectibleValue
bool FulfillsCollectibleObjective
```

Альтернатива: один nullable value object `CollectibleObjectiveValue?`.

Предпочтительно не перегружать `FulfillsJumpOnObjective`: это другой objective layer.

### 4. BotActionKind

Добавить:

```text
PassiveCollect
```

`BotActionKindRules.ConsumesTap(PassiveCollect)` должен возвращать `false`.

## Классификация obstacles

### 1. ObstacleClassifier

Добавить методы:

```text
IsCollectible(obstacleType)
IsEnergyCollectible(obstacleType)
IsLifeCollectible(obstacleType)
IsCurrencyCollectible(obstacleType)
```

### 2. ObstacleRoleClassifier

Назначать `ObstacleRole.Collectible` для всех collectable obstacle types.

### 3. Active planning role

Текущий `HasAnyActivePlanningRole` отбрасывает pure `Collectible`.

Нужно заменить это на более явное правило:

```text
Collectible становится active planning role только если CollectibleValuePolicy считает его полезным в текущем hamster state.
```

Варианты реализации:

- простой: считать все collectables active, а `PassiveCollectStrategy` отфильтрует value=0;
- более чистый: добавить `ObstacleRole.EnergyCollectible`, `LifeCollectible`, `CurrencyCollectible` и active проверку по policy.

Предпочтительный первый шаг: все collectables active, потому что это минимально и позволит opposite-lane `SwitchLane` entry работать. Если появится шум в planning, сузить active-role через policy.

## PassiveCollectStrategy

### Назначение

Добавляет no-input action, если collectable можно подобрать продолжением текущего движения без нового input.

### Applicability

Strategy applicable, если:
- в `DecisionPoint.Chain` есть collectable на текущей focus lane;
- collectable имеет positive `CollectibleObjectiveValue`;
- collectable находится впереди хомяка;
- хомяк в состоянии, где runtime pickup возможен или ожидаемо возможен без input:
  - `Run`;
  - `RoofRun`;
  - возможно `RunFromRoof`, если safety-модель подтвердит интервал;
- до точки pickup нет same-lane damaging obstacle, который пересекает хомяка раньше collectable;
- collectable не `IsRemovedInPlanning`.

### Не applicable

Не создавать action, если:
- collectable на другой линии. Для этого уже должен быть `SwitchLane` entry, после которого strategy сработает на новой линии;
- collectable требует прыжка, спуска или другого input до pickup;
- effective gain равен `0`;
- pickup находится за blocking threat, который ещё не решён;
- хомяк damaged/dead в planning state.

### Модель pickup

Нужен `PassiveCollectModel`:

```text
TargetCollectible
TargetCollectibleIndex
CompletionWorldShift
CollectibleObjectiveValue
```

`CompletionWorldShift` считать до момента, когда collider хомяка пересечёт collider collectable. Для первого приближения достаточно:

```text
max(0, collectible.LeftX - hamster.HamsterRightX)
```

Если нужно точнее под runtime trigger, можно использовать overlap по X:

```text
pickupShift = max(0, collectible.LeftX - hamster.HamsterRightX + smallEpsilon)
```

### Safety

Перед созданием action проверить участок `[0, CompletionWorldShift]`:

- same-lane damaging obstacles не должны пересекать хомяка до pickup;
- в `RoofRun` non-roof obstacle на той же линии до collectable должен считаться опасным, кроме самого collectable;
- collectable itself не dangerous.

Для первого этапа safety можно вынести в helper:

```text
PassiveCollectSafety.IsSafeUntilPickup(...)
```

Не использовать dead-end report для skipped collectables. Недостижимый collectable — это потерянная награда, а не dead-end уровня.

## PassiveCollectSimulator

После successful passive collect:

1. Увеличить projected hamster resource:
   - life: `min(3, Lives + gain)`;
   - energy: `min(100, Energy + gain)`;
   - currency: planning state gameplay-параметры не меняет.
2. Добавить target collectible instance id в `RemovedObstacleInstanceIds`.
3. Продвинуть `ProjectionWorldShift` на `CompletionWorldShift`.
4. Пересчитать `NextObstacleIndex` через существующий transition helper или новый `AdvanceAfterCollectiblePickup`.

Нужен отдельный transition helper:

```text
PlanningStateTransition.AdvanceAfterCollectiblePickup(...)
```

Причина: это похоже на target removal, но меняет ресурсы хомяка не через action energy cost, а через collectable gain.

## PassiveCollectExecutor

Executor no-input:

### TryFire

- Проверяет `BotActionKind.PassiveCollect`.
- Проверяет, что target collectible id есть.
- Логирует start/wait.
- Возвращает `Fired`.
- Не вызывает `JumpRequest`, `TapRequest` или другие input events.

### IsCompleted

Завершать action, если:
- target collectible исчез из `ObstacleSpawner.SpawnedObstacles`;
- или хомяк прошёл правую границу pickup window;
- или runtime state стал damaged/dead/cancel-like.

Если collectible исчез, это считается успешным pickup. Если пройден pickup window, action должен завершиться/отмениться, чтобы план не завис.

Важно: фактический resource gain делает runtime `CollisionController` / `CollectCoinsOrBonusAction`; executor не должен сам менять runtime energy/lives.

## ActionGenerator и opposite lane

После того как collectable станет active planning role:

- `DecisionPointDetector` сможет построить `oppositeDecisionPoint`, даже если на другой линии только collectable.
- `ActionGenerator.CollectSwitchLaneEntryAction` вызовет `SwitchLaneStrategy`.
- `SwitchLaneStrategy` создаст entry action перед первым объектом opposite chain.
- После симуляции `SwitchLane` хомяк окажется на collectable lane.
- На следующем узле `PassiveCollectStrategy` создаст pickup action.

Это сохраняет уже существующий паттерн JumpOn target hunting.

## Metrics и evaluator

### PlanningBranchMetrics

Добавить агрегаты:

```text
LifeCollectibleValue
EnergyCollectibleValue
CurrencyCollectibleValue
FirstCollectibleObjectiveTargetIndex
```

`Append(action)` должен добавлять value из `PassiveCollect` action.

Dominance check (`IsCheaperOrEquivalentTo`) должен учитывать collectable value до стоимости:

```text
JumpOn objective priority
Life value descending
Energy value descending
Currency value descending
Energy cost ascending
Tap count ascending
Action count ascending
```

### PlanEvaluator

`CompareBranches` должен использовать тот же порядок.

`Score` обновить только как вторичный diagnostic score. Главный выбор сейчас идёт через `CompareBranches`.

## Взаимодействие с jump-on objective

JumpOn objective остаётся выше collectables.

Пример:

```text
Branch A: SwitchLane -> JumpOn(smallAlive)
Branch B: SwitchLane -> PassiveCollect(Energy)
```

Если обе ветки безопасны, Branch A выигрывает при active jump-on objective.

Если jump-on objective не активен или target action unsafe, energy collectable branch может выиграть.

## Взаимодействие с energy threshold

Для energy collectables не применять `JumpOnObjectiveRules.HighPriorityEnergyThreshold`.

Причина: energy collectable восстанавливает ресурс, поэтому при низкой энергии он особенно полезен.

Но action-cost всё равно должен участвовать:

- Если ради `+30` energy нужно потратить `20`, ветка может оставаться выгодной.
- Если effective gain маленький, cost/taps могут проиграть более простой safe ветке.

Точный net scoring оставить на отдельную balance-итерацию. В первой реализации достаточно strict priority по kind/value, затем обычные cost/tap tie-breakers.

## Roof collectables

Текущий snapshot видит lane и bounds, но не хранит явный flag "collectable on roof".

На первом этапе не вводить отдельный roof flag в snapshot. Использовать runtime-факт:

- collectable подбирается по same-line trigger;
- `RoofRun` тоже может подобрать collectable, потому что `CollisionController` обрабатывает collectable до damage.

Safety для `RoofRun` должна учитывать:

- collectable safe;
- non-roof hazards на roof path unsafe;
- roof support/passive continuation остаются ответственностью существующих roof strategies.

Если после реализации обнаружится, что roof/ground collectables требуют отличать Y-позицию, тогда расширить `ObstacleSnapshot` или level model отдельным признаком. Не делать это заранее.

## Implementation steps

### Step 1. Domain classification

- Добавить `CollectibleKind`.
- Добавить `CollectibleClassifier` или методы в `ObstacleClassifier`.
- Добавить `CollectibleValuePolicy`.
- Покрыть policy простыми EditMode tests, если в проекте уже есть подходящий тестовый слой для planner value logic.

### Step 2. Planning action data

- Добавить collectible objective fields в `PlannedAction`.
- Обновить `IsEquivalentTo`.
- Убедиться, что copy/projection helpers в `RuntimeBotController` сохраняют новые поля.

### Step 3. Active role wiring

- Назначать `ObstacleRole.Collectible`.
- Сделать collectable active для `ObstacleChainBuilder`.
- Проверить, что opposite-lane collectable создаёт `DecisionPoint` и позволяет `SwitchLane` entry.

### Step 4. Passive collect strategy family

Добавить папку:

```text
Assets/Scripts/Bot/Strategies/PassiveCollect/
```

Классы:

- `PassiveCollectStrategy`;
- `PassiveCollectPlanner`;
- `PassiveCollectModel`;
- `PassiveCollectSimulator`;
- `PassiveCollectExecutor`;
- возможно `PassiveCollectSafety`.

Зарегистрировать strategy в `RuntimeBotController` рядом с `PassiveRoofExitStrategy`.

### Step 5. Transition simulation

- Добавить `PlanningStateTransition.AdvanceAfterCollectiblePickup`.
- В simulator удалять collectable из projected branch через `RemovedObstacleInstanceIds`.
- Применять projected resource gain.

### Step 6. Metrics/evaluator

- Расширить `PlanningBranchMetrics`.
- Обновить dominance и evaluator compare.
- Сохранить jump-on objective как верхний приоритет.

### Step 7. Diagnostics

Добавить компактные BOT logs:

```text
[Bot PLAN] PassiveCollect candidate kind=Energy value=30 target=collectablePizza ...
[Bot EXEC] FIRE kind=PassiveCollect ...
[Bot EXEC] COMPLETE kind=PassiveCollect ...
```

Не логировать skipped value=0 collectables шумно.

### Step 8. Validation levels

Добавить или обновить representative test levels:

1. Current lane energy collectable, no threats:
   - ожидается `PassiveCollect`.
2. Opposite lane energy collectable, current lane empty:
   - ожидается `SwitchLane -> PassiveCollect`.
3. Opposite lane life collectable vs current lane energy collectable:
   - ожидается ветка к life, если обе safe.
4. Energy collectable at energy 100:
   - не должен выигрывать ради нулевого gain.
5. Energy collectable за threat:
   - не должен создаваться unsafe passive collect.
6. Roof collectable on active roof path:
   - ожидается passive collect без лишнего input, если safe.
7. JumpOn target vs energy collectable:
   - при active jump-on objective target должен иметь priority.

Валидация по процессу бота: ручной прогон пользователя и чтение `STAB`, `BOT`, `ECO` логов после фидбэка.

## Риски и открытые вопросы

- Pure collectable active role может увеличить branching. Если станет шумно, сузить detector через value-policy или ограничить scan ближайшим valuable collectable.
- Нужен аккуратный dominance: две ветки с одинаковым state, но разным collected value не должны схлопываться в пользу более дешёвой ветки до сравнения value.
- Runtime pickup подтверждается исчезновением obstacle, но исчезновение может быть вызвано out-of-bounds. Executor должен различать success/cancel хотя бы диагностически.
- Для roof collectables может понадобиться Y/roof placement semantics, если lane-only pickup окажется недостаточным.
- Currency collectables сейчас имеют неоднозначную runtime-семантику для coin; план намеренно не исправляет это.

## Definition of Done

- Energy/life/currency collectables классифицируются и получают value с учетом caps.
- Valuable collectable на другой линии создаёт ветку через `SwitchLane`.
- `PassiveCollect` безопасно симулирует pickup и resource gain.
- Evaluator выбирает life > energy > currency после jump-on objective.
- Unsafe или нулевые collectables не ухудшают survival planning и не создают dead-end уровня.
- Representative manual bot validation пройдена без регрессии safety.

