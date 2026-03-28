# BotV3 Refactor Cleanup

## Источник требований

- Канонический источник правды: Google Doc `# Полный отчёт о сессии анализа и рефакторинга BotV3` от `2026-03-28`.
- Дополнительное уточнение пользователя:
  - для `SwitchLane` нужен один ближайший момент безопасного перестроения;
  - модель со множественными safe windows и отдельным `Latest` считается переусложнением;
  - цель задачи — отсечь лишние вычисления и упростить планировщик, даже если внутренний контракт planner-а изменится.

## Цель

Упростить `BotV3` по рекомендациям из отчёта без лишних абстракций, сохранив контролируемый процесс проверки после каждого шага:

1. `recompile_scripts`
2. autoplay test level
3. фиксация результата автопроверки
4. остановка на ручной просмотр пользователя

## Объём текущей волны

### Step 1. Подготовка артефактов задачи

Статус: `completed`

- Зафиксировать локальный living-документ в `docs/Planning/current/`.
- Удалить устаревшие артефакты предыдущей темы:
  - `LostCyberHamster/refactor_plan.md`
  - `LostCyberHamster/conversation_log_20250925.txt`

### Step 2. Semantic cleanup SwitchLane

Статус: `next`

- Удалить `SwitchLaneTimingMode.Latest`.
- Убрать двойную генерацию `SwitchLane`-кандидатов в `ActionGenerator`.
- Заменить логику списка окон в `SwitchLaneStrategy` на поиск одного earliest-safe момента.
- Обновить planner tests под новую норму поведения.

### Step 3. Structural cleanup planning pipeline

Статус: `pending`

- Разбить `BranchGenerator.ExploreBranch()` на приватные хелперы.
- Вынести строковые scope-константы.
- Перевести `BranchCandidate` на конструктор и get-only свойства.

### Step 4. Structural cleanup execution pipeline

Статус: `pending`

- Разбить `StepExecutor.TryExecute()` на pending/in-progress paths без смены runtime-логики.

### Step 5. Medium-priority cleanup

Статус: `pending`

- Чистка `BranchSelector`.
- Immutable-style для `ObjectClassifier`.
- SRP-реорганизация `BotOrchestrator` только после стабилизации предыдущих шагов.

### Step 6. Low-priority polish

Статус: `pending`

- Magic numbers
- простые null-guards
- аллокации `StringBuilder` в `BotLogger`

## Валидация

Базовый runtime smoke для этой задачи:

- `launch_test_level`
- `levelAddress = 01_New_York/Morning/test_level`
- `timeScale = 2.0`

Базовый подтверждённый результат перед началом работ:

- `testResult = WIN`
- маркер в `EditorLogs/diagnostic_log.txt`: `[TEST RESULT] WIN level=3 stars=3`

Для каждого следующего шага:

1. Если менялись `.cs` файлы, запустить `recompile_scripts`.
2. Запустить autoplay test level.
3. Проверить, что bridge завершился успешно и есть финальный `[TEST RESULT]`.
4. Остановиться и передать изменения на ручной просмотр пользователю.

## Рабочие заметки

- `CanSolve` уже отсутствует в текущем `BotV3` и из объёма исключён.
- `Assets/Editor/Tests/EditMode/Bot/*` не подключены к текущему дешёвому CLI-контуру, поэтому обязательная быстрая проверка на каждом шаге строится вокруг Unity automation bridge.
- Основной рабочий branch этой задачи: `task/botv3-refactor-cleanup`.
