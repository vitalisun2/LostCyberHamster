# Конвенции кода и валидация

## Принципы

- **SOLID / GRASP / KISS / DRY** — строго. Presentation-логика не должна быть в domain-классах. Каждый класс — одна ответственность.
- Сначала понять проблему, потом менять код. Не делать больше 2 слепых попыток без нового контекста.
- Не «улучшать» то, что работает, без явного запроса.

## Именование

- При портировании классов между версиями бота (BotV2 → BotV3) сохранять исходные имена. Пространства имён разрешают коллизии.
- Следовать существующим соглашениям проекта (PascalCase для классов/методов, camelCase для локальных переменных).

## Файловая структура

- **csproj:** при создании новых .cs файлов — `<Compile Include="..." />` в `Assembly-CSharp.csproj`. При удалении/переименовании — обновлять/удалять записи.
- **Editor tools** в `Assets/Editor/`.
- **Runtime scripts** в `Assets/Scripts/`.

## Компиляция и warnings

- После изменений проверять warnings компиляции.
- Использовать актуальные API (`FindAnyObjectByType` вместо `FindObjectOfType`).
- Не коммитить код с deprecated API.
- Удалять debug логи перед коммитом, оставлять только `DiagLog` для важной диагностики.

## Логирование

- `DebugManager.DiagLog()` — запись в `EditorLogs/diagnostic_log.txt`.
- Теги каналов: `[CH=STAB]`, `[CH=BOT]`, `[CH=ECO]`.
- Не для production кода — только Editor/Debug.

## Summary и комментарии (проверка перед коммитом)

Перед коммитом проверить каждый затронутый файл (только изменённые, не весь проект):

1. **Summary методов:** XML-summary должен точно отражать текущее поведение метода. Если метод изменился, а summary — нет, обновить summary.
2. **Комментарии в теле метода:** если метод содержит 2+ логические единицы, каждая должна иметь краткий комментарий-заголовок (что делает блок, не как).

**Логическая единица** — группа строк, решающая одну подзадачу метода. Границы между единицами: смена «что делаем», смена объекта работы, получение промежуточного результата для следующего шага. Одна единица может занимать от одной до десятков строк. Если метод — одна единица, комментарий внутри тела не нужен.

## Валидация

- C# скрипты: проверка компиляции.
- Gameplay, бот, тестовые уровни: итерационный цикл из `docs/rules/iteration_cycle.md`.
- EditMode/PlayMode тесты, если есть.
- Addressables, build config: sync/build валидация, если доступна.
- Проверка в Unity Editor — на integration-ветке в основном каталоге, не в task-worktree.
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
- BIG_NOTALIVE: 452×172
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
