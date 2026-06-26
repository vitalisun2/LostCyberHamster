# Конвенции кода и валидация

## Принципы

- **SOLID / GRASP / KISS / DRY** — строго. Presentation-логика не должна быть в domain-классах. Каждый класс — одна ответственность.
- Сначала понять проблему, потом менять код. Не делать больше 2 слепых попыток без нового контекста.
- При рефакторинге сначала восстановить полный смысл метода или алгоритма целиком: назначение всех границ, условий, промежуточных значений и их связей. Запрещено удалять, упрощать или объединять отдельные части метода по локальной гипотезе, пока не доказано, как правка изменит поведение всего метода.
- Если полный смысл метода ещё не собран, рефакторинг запрещён: разрешены только чтение кода, формулировка гипотезы и локальная проверка этой гипотезы без смысловой правки.
- Перед реализацией игровой механики изучить runtime: коллизии, state transitions, animation events. Не копировать логику вслепую.
- При изменении физических констант (JumpFireDist, swept zones) сначала проверить использование в game engine, не подбирать эмпирически.
- Не «улучшать» то, что работает, без явного запроса.

## Именование

- Не добавлять в актуальные классы и тесты суффиксы версий бота (`BotV2`, `BotV3`). Использовать нейтральные имена и убирать устаревшую версионность при ближайшем релевантном изменении.
- Следовать существующим соглашениям проекта (PascalCase для классов/методов, camelCase для локальных переменных).

## Файловая структура

- При создании, удалении или переименовании `.cs`-файлов проверять соответствующие `<Compile Include="..." />` записи в `.csproj`.
- Для новых файлов и папок под `Assets/` не писать `.meta` вручную. Дать Unity Editor сгенерировать `.meta`, затем коммитить именно сгенерированные файлы.
- **Editor tools** в `Assets/Editor/`.
- **Runtime scripts** в `Assets/Scripts/`.

## Компиляция и warnings

- Не использовать `global::` квалификатор в коде — вместо этого добавлять нужный `using` в начало файла.
- Использовать актуальные API (`FindAnyObjectByType` вместо `FindObjectOfType`).
- Не оставлять код с deprecated API.
- Для code-edit/fix задач удалять временные debug/diagnostic логи перед завершением правок; оставлять только устойчивую диагностику, оформленную через Diagnostic Log инфраструктуру из секции «Логирование».
- Для analysis-only/root cause задач временную диагностику не удалять после доказательства причины; сразу сообщить root cause, рекомендацию и где остались временные логи.

## Логирование

- Для runtime/bot диагностики использовать Diagnostic Log инфраструктуру, а не ручные `Debug.Log`, `Console.WriteLine`, запись файлов или ad-hoc logging helpers.
- Центральный gate bot diagnostics: `Assets.Scripts.Bot.Diagnostics.BotDiagnostics` (`LostCyberHamster/Assets/Scripts/Bot/Diagnostics/BotDiagnostics.cs`). Новые bot-сообщения добавлять через профильные helpers в `LostCyberHamster/Assets/Scripts/Bot/Diagnostics/`: `BotExecutionDiagnostics`, `BotReplanDiagnostics`, `BotStrategyDiagnostics`, `BotRuntimeEventDiagnostics` и т.д.
- Если нужного метода логирования нет, добавить его в соответствующий diagnostics-класс с правильными `BotDiagnosticCategory`/`BotDiagnosticLevel`, затем вызывать этот метод из runtime-кода.
- `DebugManager` (`LostCyberHamster/Assets/Scripts/GameEngine/DebugManager.cs`) — низкоуровневый transport/sink diagnostic file: `DiagLog`, `DiagLogVerbose`, `DiagChannel`, путь `EditorLogs/diagnostic_log.txt`.
- Теги каналов: `[CH=STAB]`, `[CH=BOT]`, `[CH=ECO]`.
- `Debug.LogWarning`/`Debug.LogError` допустимы для editor/user-facing предупреждений и исключительных ошибок; не использовать их как способ сбора regression facts.
- Не для production кода — только Editor/Debug.

## Данные и миграции

- При добавлении обязательных полей в JSON-данные сначала мигрировать существующие файлы. `JsonUtility.FromJson` для отсутствующих полей подставляет default-значения (`0`, `false`, `null`), поэтому ошибка может проявиться как валидные, но неверные данные.

## Summary и комментарии

Для code-edit/fix задач перед завершением правок проверить каждый затронутый файл (только изменённые, не весь проект):

1. XML-summary должен точно отражать текущее поведение метода и быть на русском языке.
2. Если метод содержит 2+ логические единицы, каждая должна иметь краткий комментарий-заголовок: что делает блок, не как.

## Валидация

- Этот раздел относится к задачам с правками. В analysis-only/root cause задачах после доказательства причины сразу отвечать пользователю и не запускать дополнительные проверки без отдельного запроса.
- Документация и планы без изменений Unity-проекта: compile/recompile не требуются.
- Не писать и не расширять тесты логики: unit, EditMode, PlayMode и editor-only тестовые harness'ы. Редакторские тесты на проекте не пишутся.
- После каждого шага рефакторинга `.cs`-файлов обязательно проверять компиляцию в Visual Studio / Roslyn и исправлять связанные ошибки до следующего шага.
- Для прочих изменений `.cs`-файлов вне рефакторинга: проверка компиляции по явному запросу пользователя.
- После изменений .cs файлов запускать `recompile_scripts` через automation bridge и дождаться `state: completed` — только по явному запросу пользователя.
- При явном запросе пользователя можно запускать или править уже существующие тесты.
- Addressables, build config: sync/build валидация, если доступна.
- Не считать задачу выполненной, если валидация не пройдена.

## Unity Editor API

- `AnimationMode.StartAnimationMode()` + `SampleAnimationClip()` для анимаций в Editor mode (не `animator.Play()`).
- `EditorApplication.timeSinceStartup` для времени в Editor (не `Time.deltaTime`).
- `SceneView.Frame(bounds, false)` для центрирования.
- `PrefabUtility.InstantiatePrefab()` для создания префабов в Editor.
- `GetComponentInChildren<T>()` вместо поиска по имени.
- Незнакомый API — проверить документацию перед использованием.

## Препятствия и ассеты

### Константы размеров (Consts.cs)
- SMALL_ALIVE: 152×108
- BIG_ALIVE: 100×212
- BIG_NOTALIVE: 388×172
- SMALL_NOTALIVE: 140×108
- Все размеры делятся на 4 (ETC2 компрессия).

### Smart Resize
- Только downscale (запрет upscaling).
- Сохранение aspect ratio.
- Чистые integer ratios (2x, 4x, 8x).
- Результат делится на 4.

### Именование анимаций
- `obstacle_{location}_{category}_{id}_{animType}-{frame}.png`
- Типы: `_idle` (статичные), `_walk` (с движением).

### Структура префабов
- Корневой объект: `BigCitizenPrefab` / `SmallCitizenPrefab` / `MediumOrBigNotAlivePrefab`.
- Дочерний с Animator/SpriteRenderer: `*Sprite`.
- Pivot: Bottom Center.
