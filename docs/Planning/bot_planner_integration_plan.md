# Интеграция BotPlanner как основного мозга бота

## Проблема
BotBrain — портянка if/else на один шаг. Не видит последствий. Пример: прыгает на 1-й smallAlive, отскакивает, врезается во 2-й. Нужен универсальный мозг, который строит карту последствий на N шагов вперёд.

## Что уже есть
| Файл | Состояние | Что делает |
|------|-----------|------------|
| `Planning/BotPlanner.cs` | Готов, НЕ подключён | Дерево решений на N шагов, выбирает лучший первый ход |
| `Planning/SimWorldState.cs` | Заглушка | Снимок мира, Simulate() двигает по X, collision — box ±0.3 |
| `Planning/BotCommands.cs` | Заглушка | JumpCommand просто удаляет ближайшее препятствие |
| `Planning/IBotCommand.cs` | Готов | Интерфейс команды: CanExecute + Execute |
| `Planning/IStateEvaluator.cs` | Готов | DefaultStateEvaluator: lives + energy + coins + position |
| `BotBrain.cs` | Рабочий | Реактивная портянка if/else + HandleUrgentThreat |
| `BotJumpPredictor.cs` | Рабочий | Предсказание исхода прыжка через CollisionUtils |
| `HamsterBot.cs` | Рабочий | Вызывает только BotBrain.Evaluate(), BotPlanner не используется |

## Принцип: честная игра
Бот видит ТОЛЬКО то, что видит игрок — препятствия в зоне камеры. `BotThreatScanner.Scan(scanDistance)` уже ограничивает видимость. scanDistance = 15 юнитов ≈ правый край экрана.

## Принцип: переиспользование существующей логики
НЕ дублировать физику. BotJumpPredictor уже умеет предсказывать исход прыжка через CollisionUtils. Симулятор должен:
1. Спросить у предиктора "что будет, если прыгнуть сейчас?"
2. На основе ответа (JumpOnObstacle, JumpOver, JumpOnRoof, Damage) — обновить SimWorldState
3. Из нового состояния — спросить снова, и так N шагов

## Архитектура

### Flow принятия решений (новый)
```
HamsterBot.Update()
  ├── scanner.Scan() — видимые препятствия
  ├── BotBrain.EvaluateImmediate() — P1-P3 только (dead/damaged/shift/roof/injump)
  │   └── если есть решение → выполнить
  ├── BotPlanner.Plan(simState) — основной мозг
  │   ├── для каждого действия (Jump/SwitchLane/Wait/SuperJump/Ulta):
  │   │   ├── SimWorldState.ApplyAction() — применить действие
  │   │   ├── SimWorldState.Advance() — промотать время до следующей точки решения
  │   │   ├── рекурсивно до depth=3
  │   │   └── evaluate() — оценить конечное состояние
  │   └── вернуть действие с лучшим score
  └── выполнить действие
```

### SimWorldState — что менять
Сейчас `Simulate()` — просто `dx = speed * dt`, примитивная collision box. Нужно:

**Добавить поля:**
- `HamsterPhase` (Running, Jumping, OnRoof, Bouncing, Protected) — текущая фаза
- `float PhaseTimeLeft` — сколько секунд до конца текущей фазы
- `float JumpShiftDistance` — дистанция сдвига за прыжок (из BotJumpPredictor)

**Новый метод `ApplyAction(BotAction action)`:**
```
Jump → phase=Jumping, расход 10 energy
  → для каждого obstacle в пределах jumpShift:
    → вызвать PredictOutcome(obstacle) — переиспользуя логику BotJumpPredictor, но на числах
    → JumpOnObstacle: score+=50, obstacle удаляется, phase=Bouncing, сдвиг=jumpShift
    → JumpOver: score+=10, obstacle удаляется, phase=Running, сдвиг=jumpShift
    → JumpOnRoof: score+=30, phase=OnRoof, сдвиг=jumpShift
    → Damage: score-=100, lives-=1
    → NoHit: phase=Running, сдвиг=jumpShift
SwitchLane → IsOnBottomLine = !IsOnBottomLine
Wait → ничего (мир продвигается в Advance)
```

**Ключевое отличие от текущего:** `PredictOutcome` работает на ЧИСЛАХ SimObstacle (distanceX, type), а не на реальных GameObject. Это чистая арифметика, повторяющая логику BotJumpPredictor но без Unity API.

### SimObstacle — добавить поля
```csharp
public float Width;      // ширина коллайдера (для расчёта overlap)
public float Height;     // высота (для bigAlive Y-проверки)
```

### BotCommands — переписать
Убрать `RemoveNearestOnCurrentLane`. Каждая команда вызывает `SimWorldState.ApplyAction()`.

### BotBrain — упростить
Убрать:
- `HandleUrgentThreat` (вся портянка)
- `FindConsecutiveSmallAlive` (костыль)
- `CheckJumpSafe` (предиктор теперь внутри симуляции)
- Priority 5-8 (проактивные прыжки на бонусы — планнер сам найдёт)
- Все helper методы для поиска угроз

Оставить:
- Priority 1: Dead/Damaged/Shifting → DoNothing (хомяк не может действовать)
- Priority 2: RoofRun → простая логика (прыгнуть если smallNotAliveRoadAndRoof)
- Priority 3: InJump → SuperJump для bigAlive
- Purchases → оставить (не связано с планированием)
- DecisionTrail → перенести в планнер

### HamsterBot — подключить планнер
```csharp
// В TryInitAndEnable():
_planner = new BotPlanner(styleConfig);

// В Update():
var immediate = _brain.EvaluateImmediate(hamster, ...);
if (immediate.Action != BotAction.None) { Execute(immediate); return; }

var simState = SimWorldState.FromCurrent(hamster, scanner.AllThreats, jumpPredictor);
var planned = _planner.Plan(simState);
if (planned.Action != BotAction.None) { Execute(planned); }
```

### PredictOutcome — чистая арифметика для симуляции
Новый статический класс `SimJumpPredictor` в Planning/:
```csharp
static JumpPrediction Predict(SimWorldState state, SimObstacle obstacle)
{
    // Повторяет логику BotJumpPredictor, но на числах:
    // hamsterLeft/Right = фиксированные (HamsterXPos ± width/2)
    // obstacleLeft/Right = obstacle.DistanceX сдвинутый на jumpShift
    // Проверки: IsCenter inside? IsOverlap? IsJumpOver?
    // Без Unity API — чистый float math
}
```

## Порядок реализации

### Шаг 1: SimJumpPredictor (новый файл)
Чистые float-функции, повторяющие CollisionUtils на числах. Тестируемо без Unity.

### Шаг 2: SimWorldState.ApplyAction + SimObstacle расширение
Добавить Width/Height в SimObstacle. FromCurrent заполняет из Obstacle.ColliderWidth/Height.
ApplyAction использует SimJumpPredictor для каждого препятствия в range.

### Шаг 3: BotCommands — упростить
Каждая команда просто вызывает state.ApplyAction(Action).

### Шаг 4: BotBrain → BotBrain (slim)
Удалить HandleUrgentThreat и всю реактивную портянку. Оставить EvaluateImmediate.

### Шаг 5: HamsterBot — подключить
Создать BotPlanner в TryInitAndEnable, вызывать Plan() в Update.

### Шаг 6: Тест + коммит

## Физические константы (из кода)
- `Consts.GameSpeedBase = 3.8f` юнитов/сек
- `Consts.HamsterXPos = -3.78f` (X позиция хомяка, фиксирована)
- `jumpClipWorldShift` ≈ 3.8 * (frameCount / 60) — точное значение из BotJumpPredictor при init
- `hamsterWidth`, `hamsterHeight` — из Hamster.ColliderWidth/Height
- `RIGHT_EDGE_TOL_RATIO = 0.2f` — допуск для JumpOnObstacle
- Энергия: Jump=10, SuperJump=20, restore=1/sec, max=100
- CameraSize = 3.1, scanDistance = 15 (≈ правый край экрана)

## Scoring (DefaultStateEvaluator)
Текущие веса подходят, но добавить:
- `+50` за JumpOnObstacle (smallAlive бонус)
- `+30` за JumpOnRoof (безопасность)
- `+10` за JumpOver (избежание)
- `-100` за Damage
- `-1000` за смерть (lives=0)
- `-5` за потраченную энергию без результата (Wait когда можно было прыгнуть)

## Риски
1. **SimJumpPredictor может не совпасть с реальным** — нужно валидировать через DiagLog
2. **Performance** — depth=3, branch=5 = 125 нод макс. При чистой арифметике это <0.1ms
3. **Тайминг "когда прыгать"** — планнер даёт "Jump сейчас", но optimal timing (подождать 0.1s) — это уровень BotBrain. Решение: планнер запускается каждый кадр, если еще рано — вернёт Wait
