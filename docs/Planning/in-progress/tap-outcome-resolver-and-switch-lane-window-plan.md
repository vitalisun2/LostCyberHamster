# Tap outcome resolver and switch-lane window plan

## 1. Цель

Сделать для `Tap` / `SwitchLane` такую же архитектурную опору на runtime-правила, какая уже есть у `Jump` и `SuperJump` через outcome resolver'ы.

Источник истины: runtime-код. Bot planning не должен заново придумывать guard-условия `TapRequest` и не должен запрещать ситуации, которые runtime не запрещает, если это не отдельное явно задокументированное ограничение стратегии.

## 2. Текущее состояние

### 2.1. Runtime tap

Runtime-логика находится в `Assets/Scripts/GameEngine/Mechanics/TapMechanics.cs`.

`TapRequest` сейчас:

- игнорируется, если `HamsterState` не `Run` и не `RoofRun`, при этом `IsDamaged == false`;
- игнорируется, если `IsShifting == true`;
- если принят, вызывает `ShiftTransformAnimatorController.ToggleLane()`;
- сразу меняет логическую линию `IsOnBottomLine`;
- обновляет `IsShifting` из animator transition state.

Важный вывод: runtime не запрещает tap при `IsDamaged == true`. Значит, это не должно быть общим guard-условием в bot logic. Если bot временно не хочет использовать damaged-состояния из-за неполной модели invulnerability window, это должно быть отдельной planning-политикой, а не "runtime tap невозможен".

### 2.2. Runtime collision after tap

У `TapMechanics` нет отдельного исхода вроде `SwitchLaneDamage` или `SwitchLaneSafe`.

После принятого tap логическая линия меняется сразу, а дальнейший урон определяется обычным `CollisionController`:

- если `IsDamaged == true`, trigger-collision игнорируется;
- если obstacle не на той же линии, collision игнорируется;
- collectable обрабатываются раньше damage;
- в ground/run состояниях threat obstacle наносит damage при trigger-contact;
- в `RoofRun` damage наносит любой non-roof obstacle.

То есть runtime "результат перестроения" размазан между `TapMechanics` и будущими trigger-collision проверками.

### 2.3. Bot switch-lane planning

Сейчас расчёт момента tap находится прямо в `SwitchLaneStrategy`.

Стратегия:

- работает для `DecisionPointKind.BlockingGroundObstacle`;
- считает `latestFireShift` перед target obstacle;
- строит unsafe intervals на target lane;
- превращает их в safe intervals;
- выбирает interior point внутри safe interval;
- создаёт `PlannedAction(BotActionKind.Tap)`;
- симулирует итог через `PlanningStateTransition.ApplyLaneSwitch`.

Безопасность результата сейчас проверяется в `SwitchLaneStrategy.CollectSafeFireIntervals()` / `CollectUnsafeFireIntervals()`.

Текущая модель unsafe interval:

- берутся все damaging obstacle на target lane;
- для каждого считается overlap interval с хомяком;
- unsafe window расширяется назад на `SwitchLaneDecisionTravel`;
- tap safe, если в интервале `[fireShift; fireShift + SwitchLaneDecisionTravel]` target-lane obstacle не пересечётся с хомяком.

Это правильно по смыслу, но архитектурно логика живёт внутри стратегии, а не в общем window/resolver слое.

### 2.4. Дублирование в retained action validation

`RetainedActionRevalidator.IsScheduledTapStillValid()` повторно валидирует retained tap через методы `SwitchLaneStrategy`:

- `SwitchLaneStrategy.CanSwitchLane`;
- `SwitchLaneStrategy.TryGetLatestFireShift`;
- `SwitchLaneStrategy.CollectSafeFireIntervals`.

Это явный сигнал, что расчёт окна не должен принадлежать стратегии. Стратегия должна быть thin orchestration layer, а не владельцем низкоуровневой geometry/timing logic.

## 3. Проблемы текущей архитектуры

1. `SwitchLaneStrategy` содержит слишком много responsibilities: guard, timing window, safety intervals, action construction, simulation.
2. Runtime guard-логика `TapRequest` продублирована неявно и не полностью совпадает с planning guard'ами.
3. `RetainedActionRevalidator` зависит от внутренней логики стратегии, хотя должен валидировать семантику действия через общий action/window layer.
4. `ActionWindowFinder` уже является местом для поиска fire shift у jump-действий, но switch-lane окно пока находится отдельно.
5. `ObstacleClassifier.DamagesOnGroundContact` живёт в bot namespace, хотя это runtime-dangerous type set и должно быть ближе к runtime/common collision rules.

## 4. Целевая архитектура

### 4.1. `TapOutcomeResolver`

Добавить чистый resolver для tap-семантики, аналогично jump outcome resolver'ам, но проще по модели.

Предлагаемое расположение:

`Assets/Scripts/GameEngine/Mechanics/TapOutcomeResolver.cs`

Базовая ответственность:

- принять snapshot-like context;
- применить ровно runtime guard-условия `TapMechanics`;
- вернуть, будет ли tap принят;
- вернуть target lane при принятом tap;
- вернуть reason/kind при ignored tap.

Пример API по смыслу:

```csharp
public readonly struct TapResolveContext
{
    public HamsterStateEnum HamsterState { get; }
    public bool IsOnBottomLine { get; }
    public bool IsDamaged { get; }
    public bool IsShifting { get; }
}

public readonly struct TapResolveResult
{
    public bool IsAccepted { get; }
    public bool? TargetBottomLine { get; }
    public TapResolveKind Kind { get; }
}
```

`TapResolveKind` достаточно держать минимальным:

- `Accepted`;
- `IgnoredByState`;
- `IgnoredByShifting`.

Runtime-equivalent rule:

```csharp
if (context.IsShifting)
    return IgnoredByShifting;

if (context.HamsterState != HamsterStateEnum.Run
    && context.HamsterState != HamsterStateEnum.RoofRun
    && !context.IsDamaged)
    return IgnoredByState;

return Accepted(targetBottomLine: !context.IsOnBottomLine);
```

### 4.2. Использование resolver в runtime

`TapMechanics.OnTap()` должен использовать `TapOutcomeResolver.ResolveTap(...)` для guard-решения.

Поведение runtime при этом не должно измениться:

- если resolver вернул ignored, выйти;
- если accepted, вызвать `ToggleLane()`;
- обновить `IsOnBottomLine`;
- обновить `IsShifting`.

На первом шаге лучше сохранить поведение чтения итоговой линии из animator (`IsShiftedDown()`), чтобы не менять runtime coupling с `ShiftTransformAnimatorController`. Можно дополнительно debug/assert сверить с `TapResolveResult.TargetBottomLine`, но не обязательно.

### 4.3. Использование resolver в bot execution

`SwitchLaneActionHandler.TryFire()` должен проверять live runtime state через `TapOutcomeResolver` перед `hamster.TapRequest.Invoke()`.

Это решит две вещи:

- handler не будет считать action fired, если runtime всё равно проигнорирует tap;
- handler будет следовать тем же guard-условиям, что и `TapMechanics`.

Важно: `IsDamaged == true` не должен автоматически cancel'ить tap, потому что runtime tap это не запрещает.

### 4.4. Использование resolver в planning

`SwitchLaneStrategy.CanSwitchLane()` должен перестать быть источником runtime guard-логики.

Вместо этого стратегия должна:

- запросить `TapOutcomeResolver.ResolveTap(...)`;
- если tap не accepted, не создавать action;
- взять `TargetBottomLine` из результата;
- отдельно применять только ограничения области ответственности стратегии.

Ограничение "эта стратегия сейчас решает только ground blocking obstacle" можно оставить как strategy scope. Но это не должно называться runtime guard'ом.

## 5. Switch-lane fire shift в `ActionWindowFinder`

Не нужен отдельный `LaneSwitchWindowFinder`. Чтобы не плодить сущности, switch-lane поиск стоит добавить в существующий `ActionWindowFinder`.

Предлагаемый публичный метод:

```csharp
public static bool TryFindSwitchLaneFireShift(
    PlanningState planningState,
    WorldSnapshot projectedWorldSnapshot,
    ObstacleSnapshot targetObstacle,
    int targetObstacleIndex,
    out float fireShift)
```

Задачи метода:

1. Проверить tap через `TapOutcomeResolver`.
2. Получить target lane из `TapResolveResult`.
3. Посчитать search/latest window для ухода от current-lane target obstacle.
4. Проверить safety результата перестроения на target lane.
5. Выбрать robust interior fire shift с учётом `RuntimeFireDelayBudget`.

### 5.1. Что делать с текущими методами `SwitchLaneStrategy`

Перенести из `SwitchLaneStrategy` в `ActionWindowFinder`:

- `TryGetLatestFireShift`;
- `CollectSafeFireIntervals`;
- `CollectUnsafeFireIntervals`;
- `TrySelectInteriorFireShift` для switch-lane.

После этого `SwitchLaneStrategy.CollectActions()` станет тонким:

```csharp
if (!ActionWindowFinder.TryFindSwitchLaneFireShift(..., out float fireShift))
    return;

AddTapCandidate(...);
```

### 5.2. Safety результата через resolver

Для архитектурной симметрии с jump-действиями `ActionWindowFinder.TryFindSwitchLaneFireShift()` должен не просто пересчитать интервалы, а проверять candidate shift через tap resolver/safety API.

Минимальный вариант без лишних классов:

```csharp
TapSwitchResolveResult result = TapOutcomeResolver.ResolveSwitchLaneAtShift(
    tapContext,
    obstacleData,
    fireShift,
    switchLaneTravel);

if (result.Kind == TapSwitchResolveKind.Safe)
    ...
```

Где `ResolveSwitchLaneAtShift` отвечает за прогноз на конкретном `fireShift`:

- tap принят или ignored;
- какая target lane;
- будет ли damaging obstacle на target lane пересекаться с хомяком в окне `[fireShift; fireShift + switchLaneTravel]`;
- какой obstacle делает результат unsafe, если такой есть.

Так `ActionWindowFinder` остаётся владельцем поиска окна, а `TapOutcomeResolver` становится владельцем семантики "что произойдёт, если tap будет сделан в этой точке".

### 5.3. Сканирование или интервалы

Есть два допустимых варианта реализации:

1. Сохранить текущую interval-математику и перенести её в `ActionWindowFinder`.
2. Сделать switch-lane поиск похожим на jump-over поиск: сканировать окно с `SearchStep` и вызывать `TapOutcomeResolver.ResolveSwitchLaneAtShift(...)`.

Практичный вариант: начать с переноса interval-математики, потому что она уже работает и точнее выражает split windows. При этом финальную проверку выбранного `fireShift` всё равно прогонять через `TapOutcomeResolver.ResolveSwitchLaneAtShift(...)`.

Если позже понадобится полная симметрия с jump window search, можно перейти на сканирование без изменения API стратегии.

## 6. Runtime collision rules для resolver

`TapOutcomeResolver.ResolveSwitchLaneAtShift(...)` должен опираться на runtime collision semantics, а не на произвольные bot-only правила.

Нужно вынести или переиспользовать общий classifier для obstacle types:

- collectables не damage;
- `decor` не damage;
- `smallAlive`, `bigAlive`, `smallNotAliveRoad`, `smallNotAliveRoadAndRoof`, `bigNotAlive`, `mediumNotAlive` damage on ground contact;
- roof-run отдельная ветка: damage от non-roof obstacle.

Сейчас похожее знание есть в `ObstacleClassifier.DamagesOnGroundContact`, но он находится в bot namespace. Лучше перенести runtime-dangerous classification ближе к runtime/common слою, например в `CollisionUtils`:

```csharp
CollisionUtils.IsCollectableObstacle(type)
CollisionUtils.DamagesOnGroundContact(type)
```

А `ObstacleClassifier` либо делегирует туда, либо перестаёт дублировать этот метод.

## 7. План внедрения

### Шаг 1. Добавить tap outcome resolver

- Создать `TapOutcomeResolver`.
- Добавить минимальные модели результата/context.
- Покрыть runtime guard-условия из `TapMechanics`.
- Не добавлять bot-specific restrictions в resolver.

### Шаг 2. Подключить resolver к runtime

- Обновить `TapMechanics.OnTap()`.
- Сохранить текущее поведение `ToggleLane()` и обновления `IsOnBottomLine`.
- Проверить, что damaged tap остаётся допустимым, как сейчас.

### Шаг 3. Подключить resolver к execution

- Обновить `SwitchLaneActionHandler.TryFire()`.
- Перед `TapRequest.Invoke()` проверять, что resolver принимает tap и target lane совпадает с `PlannedAction.TargetBottomLine`.
- Если tap временно невозможен из-за `IsShifting`, возвращать `Waiting`.
- Если tap невозможен по state/target mismatch, возвращать `Cancelled`.

### Шаг 4. Перенести switch-lane window в `ActionWindowFinder`

- Добавить `TryFindSwitchLaneFireShift(...)`.
- Перенести туда search/latest window и safe interval calculation.
- Оставить `SwitchLaneStrategy` только сборщиком candidate action.
- Не создавать отдельный `LaneSwitchWindowFinder` на этом этапе.

### Шаг 5. Добавить safety API для конкретного fireShift

- Добавить в `TapOutcomeResolver` метод прогнозирования switch-lane результата на конкретном `fireShift`.
- Использовать его внутри `ActionWindowFinder` для финальной проверки выбранной точки.
- При необходимости позже заменить interval selection на resolver-based scan.

### Шаг 6. Обновить retained validation

- `RetainedActionRevalidator.IsScheduledTapStillValid()` должен перестать ссылаться на `SwitchLaneStrategy`.
- Для retained tap использовать `ActionWindowFinder` / `TapOutcomeResolver`.
- Цель: стратегия больше не является dependency для validation слоя.

### Шаг 7. Уточнить planning constants

- `SwitchLaneDecisionDuration = 0.45f` сейчас bot-side модель времени перестроения.
- Нужно проверить, можно ли получить эту длительность из runtime animator/transition данных или вынести в общий named constant.
- Если оставить hardcoded, явно задокументировать, что это planning horizon для safety, а не runtime guard.

### Шаг 8. Проверка

- EditMode/unit tests для `TapOutcomeResolver.ResolveTap()`:
  - `Run` accepted;
  - `RoofRun` accepted;
  - `IsShifting` ignored;
  - non-run state ignored when `IsDamaged == false`;
  - non-run state accepted when `IsDamaged == true`, как в runtime.
- Тонкие tests для `TryFindSwitchLaneFireShift()` на split safe windows.
- Ручной test level прогон для текущих switch-lane сценариев, чтобы подтвердить отсутствие регрессии.

## 8. Ожидаемый результат

После рефакторинга:

- runtime и bot используют один источник правды для принятия/игнорирования tap;
- `SwitchLaneStrategy` становится простой стратегией, а не владельцем geometry/timing logic;
- `ActionWindowFinder` становится единым местом поиска fire shift для `Jump`, `SuperJump`, `SwitchLane`;
- retained action validation больше не зависит от внутренностей стратегии;
- damaged-state поведение перестаёт расходиться с runtime;
- архитектура остаётся простой: один новый resolver и расширение существующего `ActionWindowFinder`, без лишнего отдельного window-finder класса.
