# BotV3 Post-Review Refactoring Plan

Пошаговый план рефакторинга по результатам code review BotV3.
Цель: устранить мёртвый код, магические числа, дублирование, нарушения SOLID/KISS.
Правило: поведение бота не должно измениться.

## Steps

### Step 1 — Clean dead code in SwitchLaneSafety
- Удалить `IsImmediatelySafe(BotSceneSnapshot)` — нигде не вызывается
- Удалить `IsHazard(ObstacleInfo)` — использовался только snapshot-перегрузкой
- Удалить `LaneSwitchTravel` — объявлена, но не участвует в расчётах
- Обновить xml-doc класса (убрать ложное упоминание BranchGenerator)

### Step 2 — Remove unused ConsumedObjectIds
- Удалить поле `ConsumedObjectIds` из `StepProjectionResult` — никогда не заполняется и не читается

### Step 3 — Implement safety check in StateProjector.Project
- `StateProjector.Project` всегда возвращает `IsSafe = true` — `BranchGenerator` проверяет это поле, но оно бесполезно
- Реализовать проверку: после проекции шага проверить, не попадёт ли хомяк в threat на целевой линии
- Использовать ту же логику overlap что в `SwitchLaneSafety.WouldHitDuringTargetPhase`

### Step 4 — Extract shared physics constants
- Создать `BotPhysicsConsts` (static class) с общими константами:
  - `ReturnControlDuration = 0.47f` (дублируется в SwitchLaneSafety и StateProjector)
  - `JumpLandingOffset = 3.8f` (дублируется в ActionGenerator и StateProjector)
  - `GameSpeedBase` ссылка для удобства
- Заменить дубликаты ссылками на единый source

### Step 5 — Name inline magic numbers
- `StepExecutor:58` → `-0.3f` → именованная константа `TooLateThreshold`
- `ObjectClassifier:30` → `-0.2f` → именованная константа `BehindHamsterThreshold`
- `BotOrchestrator:62` → `0.5f` → именованная константа `InitRetryInterval`
- `StepExecutor:118` → `0.1f` → именованная константа `SwitchLaneMinElapsed`

### Step 6 — Fix BranchGenerator allocations
- Заменить мутацию + копирование `stepsSoFar` на передачу immutable snapshot
- Использовать паттерн: передавать массив фиксированного размера + depth index, или stackalloc-like approach

### Step 7 — Move IsOnSameLane to BotSceneSnapshot
- Перенести `ActionGenerator.IsOnSameLane` в `BotSceneSnapshot` как instance-метод
- Обновить все call-sites: `ActionGenerator`, `BranchGenerator`

### Step 8 — Add IDisposable to GameEventTracker
- Добавить `IDisposable` интерфейс — метод `Dispose()` уже есть, нужно только объявить интерфейс
- В Unity `IDisposable` актуален для non-MonoBehaviour классов с подписками

### Step 9 — Unify BotSceneSnapshot and PlannerState
- Извлечь общие поля в base class или struct `HamsterState`
- `PlannerState` наследует/содержит `HamsterState` + `RemainingObjects`
- `BotSceneSnapshot` наследует/содержит `HamsterState` + `VisibleObjects` + `SnapshotTime`
- Убрать дублирование `FromSnapshot`/`ToSnapshot` конвертеров

### Step 10 — Remove business logic from BotLogger
- `BotLogger.LogActionCandidates` дублирует проверку `dist < 1.5f`
- Передавать причину отказа SwitchLane из `ActionGenerator` как параметр
- Логгер только форматирует, не вычисляет

### Step 11 — Low-priority cleanups
- `BotSceneSnapshot`: public fields → properties с public get / internal set
- `BranchOutcome.AllStepsSafe = true` default → явная инициализация при создании
- `CurrentPlan.RemoveCompletedFromHead`: оставить as-is (max 3 шага, O(n) ок)

### Step 12 — Run autoplay test, verify logs, commit & push
