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
4. анализ логов и переход к следующему шагу, если нет серьёзной регрессии

## Объём текущей волны

### Step 1. Подготовка артефактов задачи

Статус: `completed`

- Зафиксировать локальный living-документ в `docs/Planning/current/`.
- Удалить устаревшие артефакты предыдущей темы:
  - `LostCyberHamster/refactor_plan.md`
  - `LostCyberHamster/conversation_log_20250925.txt`

### Step 2. Semantic cleanup SwitchLane

Статус: `completed`

- Удалить `SwitchLaneTimingMode.Latest`.
- Убрать двойную генерацию `SwitchLane`-кандидатов в `ActionGenerator`.
- Заменить логику списка окон в `SwitchLaneStrategy` на поиск одного earliest-safe момента.
- Обновить planner tests под новую норму поведения.

Результат текущей реализации:

- `ActionGenerator` строит один канонический `SwitchLane`-кандидат для текущей проблемы.
- `SwitchLaneStrategy` ищет один ближайший допустимый fire moment вместо списка safe windows.
- `Latest` timing-вариант удалён из planner-контракта.
- Planner tests переписаны под норму `single canonical switch candidate`.

### Step 3. Structural cleanup planning pipeline

Статус: `completed`

- Разбить `BranchGenerator.ExploreBranch()` на приватные хелперы.
- Вынести строковые scope-константы.
- Перевести `BranchCandidate` на конструктор и get-only свойства.

### Step 4. Structural cleanup execution pipeline

Статус: `completed`

- Разбить `StepExecutor.TryExecute()` на pending/in-progress paths без смены runtime-логики.

### Step 5. Medium-priority cleanup

Статус: `completed`

- Чистка `BranchSelector`.
- Immutable-style для `ObjectClassifier`.
- SRP-реорганизация `BotOrchestrator` только после стабилизации предыдущих шагов.

### Step 6. Low-priority polish

Статус: `completed`

- Magic numbers
- простые null-guards
- аллокации `StringBuilder` в `BotLogger`

## Валидация

Базовый runtime smoke для этой задачи:

- `launch_test_level`
- `levelAddress = 01_New_York/Morning/test_level`
- `timeScale = 2.0` по умолчанию для всех автопрогонов этой задачи

Базовый подтверждённый результат перед началом работ:

- `testResult = WIN`
- маркер в `EditorLogs/diagnostic_log.txt`: `[TEST RESULT] WIN level=3 stars=3`

Для каждого следующего шага:

1. Если менялись `.cs` файлы, запустить `recompile_scripts`.
2. Запустить autoplay test level.
3. Проверить, что bridge завершился успешно и есть финальный `[TEST RESULT]`.
4. Проверить логи на новые `[BotV3 CONTRACT]`, `Debug.LogError` и явные аномалии planner/runtime sequence.
5. Если серьёзной регрессии нет, коммитить шаг и переходить дальше в автопилоте.

## Рабочие заметки

- `CanSolve` уже отсутствует в текущем `BotV3` и из объёма исключён.
- `Assets/Editor/Tests/EditMode/Bot/*` не подключены к текущему дешёвому CLI-контуру, поэтому обязательная быстрая проверка на каждом шаге строится вокруг Unity automation bridge.
- Основной рабочий branch этой задачи: `task/botv3-refactor-cleanup`.
- По отдельному подтверждению пользователя сессия переведена в режим автопилота: шаги выполняются подряд с commit + Unity smoke после каждого шага.
- Дополнительный follow-up после шага 6: в `LevelController` включён дефолтный `x2`, когда активен `BotV3` и инспекторный `_timeScale` не переопределён вручную.
