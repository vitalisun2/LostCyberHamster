# SwitchLane JumpOn Readability

## Цель
Сделать текущую логику `SwitchLane` вокруг подготовки к `JumpOn` понятной из кода, не меняя уже проходящее поведение тестовых уровней. Главная проблема сейчас не в результате planning, а в том, что намерение размазано между `DecisionPoint`, `SwitchLaneStrategy`, `SwitchLaneFireWindowCalculator` и timing-константами.

## Что сохранить
- `SwitchLane` должен строить безопасные окна перестроения с учетом опасностей на целевой линии.
- Для обычного safe-window должен оставаться вариант в середине окна.
- Для сценариев с high-priority `JumpOn` должен оставаться ранний вариант перестроения.
- Safety-модель перестроения должна по-прежнему учитывать дистанцию самого lane transition.
- Planning после tap должен по-прежнему продолжаться без дополнительного post-fire сдвига, если runtime переключает линию сразу при tap.

## Шаги реализации

### 1. Выделить выбор fire shifts внутри SwitchLaneStrategy
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`

**Суть:** добавить приватный метод, который явно описывает, какие точки safe-window нужно пробовать для текущего сценария `SwitchLane`.

**Детали:** вместо пары `selectionRatio` + `includeEarlyFireShift` метод должен возвращать набор ratio. Например: середина safe-window, near-start safe-window, или обе точки. Отдельный класс и value object для плана здесь не нужны: в этой задаче они добавляют структуру без достаточной пользы.

### 2. Подключить выбор sampling ratio в SwitchLaneStrategy
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`

**Суть:** сделать решение о ранней или средней точке отдельным приватным методом рядом с местом использования.

**Детали:** метод должен принимать `PlanningState` и `DecisionPoint`, затем возвращать ratio для safe-window sampling. Внутри нужно сохранить текущее поведение:
- `JumpOnOpportunity` -> ранняя точка safe-window;
- обычная blocking threat -> середина safe-window;
- blocking threat при high-priority JumpOn budget -> ранняя точка + середина.

### 3. Убрать прямой energy threshold из SwitchLaneStrategy
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`

**Суть:** общий `PlanningInterestRules` не нужен; threshold остаётся локальной константой конкретного потребителя.

**Детали:** удалить общий `PlanningInterestRules`; в `SwitchLaneStrategy` threshold должен быть рядом с приватным методом выбора selection ratios.

### 4. Передавать sampling plan в fire-window calculator
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneFireWindowCalculator.cs`

**Суть:** заменить сигнатуру `CollectFireShifts(..., float selectionRatio, bool includeEarlyFireShift)` на `CollectFireShifts(..., IReadOnlyList<float> selectionRatios)`.

**Детали:** calculator должен отвечать только за safe/unsafe intervals и применение уже выбранных ratio к каждому safe interval. Он не должен решать, почему список содержит раннюю точку.

### 5. Убрать дублирование раннего ratio
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneTiming.cs`

**Суть:** заменить неясные ratio-имена на `EarlyWindowSelectionRatio` и `MidWindowSelectionRatio`, а отдельный `JumpOnOpportunitySelectionRatio` удалить как дубль ранней точки.

**Детали:** сценарий `JumpOnOpportunity` должен выбирать `EarlyWindowSelectionRatio` через приватный метод выбора ratio, а не через отдельную числовую константу.

### 6. Развести travel для safety и planning continuation
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneTiming.cs`

**Суть:** текущие имена не показывают, что есть два разных смысла: дистанция визуального/опасного lane transition и сдвиг planning после tap.

**Детали:** переименовать или разделить константы так, чтобы было видно:
- travel, который используется при расчете unsafe intervals на целевой линии;
- travel после tap, который добавляется в `completionWorldShift` / `postFireWorldShift`.

Текущее значение второго travel должно остаться `0f`, потому что planning продолжает строить следующие действия от момента tap.

### 7. Сделать BuildAction явнее про post-tap planning
**Файл:** `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`

**Суть:** в `BuildAction` должно быть видно, что `completionWorldShift` для `SwitchLane` означает "дойти до tap + planning continuation", а не "дождаться конца визуального перестроения".

**Детали:** использовать новую именованную константу из шага 6 и локальные переменные с доменными именами, например `tapFireShift` и `postTapPlanningTravel`.

### 8. Уточнить deadline-термины
**Файлы:** `LostCyberHamster/Assets/Scripts/Bot/Planning/DecisionPoints/DecisionPoint.cs`, `LostCyberHamster/Assets/Scripts/Bot/Strategies/SwitchLane/SwitchLaneStrategy.cs`

**Суть:** `FireBeforeObstacle` плохо объясняет, что это deadline для подготовительного action перед ближайшей blocking threat.

**Детали:** переименовать в более явное имя, например `FireDeadlineObstacle` или `ActionDeadlineObstacle`. Метод `TryClampLatestFireShiftBeforeDeadline` оставить рядом с использованием, но обновить summary и имена параметров.

### 9. Обновить comments и XML summary
**Файлы:** затронутые `.cs` из шагов выше.

**Суть:** comments должны объяснять доменные границы, а не пересказывать строки кода.

**Детали:** особенно важно зафиксировать:
- почему ранняя точка safe-window нужна для подготовки к `JumpOn`;
- почему safety travel и post-tap planning travel не одно и то же;
- что `SwitchLaneFireWindowCalculator` не выбирает стратегию, а только применяет готовый sampling plan.

### 10. Проверить сохранение поведения
**Файлы:** только затронутые `.cs`.

**Суть:** после рефакторинга сравнить diff по логике и убедиться, что менялись имена/структура, а не поведение.

**Детали:** для `.cs`-рефакторинга нужна компиляционная проверка по правилам проекта. Ручной Unity-прогон остается за пользователем; релевантный уровень для поведения `SwitchLane` - `01_New_York/Morning/test_switch_lane`.

## Открытые вопросы
- Нужен ли ранний fire shift для обычной blocking threat при `Energy >= 40`, если текущий `DecisionPoint` не является `JumpOnOpportunity`, или это стоит ограничить только явно найденным `JumpOnOpportunity`.
- Достаточно ли одного named ratio `NearStartSafeWindowRatio = 0.05f`, или для разных сценариев позже понадобятся разные near-start точки.
- Нужно ли переносить смысл `FireDeadlineObstacle` в отдельный объект контекста decision point, если таких deadline-сценариев станет больше.

## Критерий готовности
- `SwitchLaneStrategy` читает decision context, получает selection ratios через приватный метод и строит actions без boolean-флагов сценария.
- `SwitchLaneFireWindowCalculator` не содержит boolean-флагов выбора сценария.
- Timing-константы отдельно называют safety travel и post-tap planning travel.
- Поведение текущих проходящих тестовых уровней сохранено.
