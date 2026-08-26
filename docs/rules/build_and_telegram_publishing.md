# Сборка игры и публикация в Telegram

Владелец темы: процесс сборки локального тестового билда LostCyberHamster и публикации APK в Telegram-канал `LostCyberHamster builds`.

## Коротко

Целевая схема быстрых Android-сборок:

```text
source worktree
  -> warm build sandbox
  -> build manifest
  -> Android development APK
  -> Telegram
```

Главная идея: не переносить изменения между ветками ради билда. Skill берет состояние нужного worktree, синхронизирует source/config в прогретый build sandbox, собирает APK из sandbox и публикует результат в Telegram.

## Источник правды

- Использовать локальный Codex skill `publish-build-to-telegram-buffer`: `%USERPROFILE%\.codex\skills\publish-build-to-telegram-buffer`.
- Перед запуском всегда читать `SKILL.md` и `references/lost-cyber-hamster-context.md` внутри skill: там актуальные пути, preflight, ограничения Telegram и детали инфраструктуры.
- Воспроизводимая build-логика должна жить в репозитории в `tools/build/`; skill должен быть тонким локальным orchestrator-ом.
- Не искать и не печатать Telegram secrets; использовать только уже настроенную локальную конфигурацию или явно переданные пользователем данные.

## Роли

- `SourceWorktree` - рабочая копия, состояние которой нужно собрать: task-worktree, `integration/unity-live` или текущий dirty worktree по явному намерению пользователя.
- `BuildSandbox` - прогретая сборочная копия проекта, например `C:\BuildWorkspaces\LostCyberHamster_Android`.
- `tools/build/build_android_telegram.ps1` - целевой repo entrypoint для подготовки sandbox, manifest и APK. По умолчанию запускает Unity через CLI.
- `publish-build-to-telegram-buffer` - локальный skill, который вызывает repo entrypoint и публикует APK в Telegram.

## Где выполнять

- После feature-задачи или bug fix источником сборки является тот worktree, состояние которого нужно отдать на телефон.
- `integration/unity-live` использовать как общий Unity-стенд для проверки под lock из Task Branch workflow.
- Для тестового APK не переносить изменения в отдельную build-ветку только ради сборки.
- Bug regression / analysis-only workflow сам по себе не запускает сборку и публикацию; это отдельный запрос после доказанного root cause или завершенного fix.

## Дефолт сборки

- Если пользователь просит "собери билд", "собери APK", "отправь APK" или "отправь в Telegram", по умолчанию собирать Android development APK (`-Development`).
- Встроенную Unity Development Console в Player по умолчанию не показывать. Включать её только по явному запросу через `-ShowDevelopmentConsole`; это не отключает проектное DEV-окно.
- Android development APK должны подписываться общим локальным dev keystore; настройка и перенос между ноутбуками описаны в `docs/android_dev_signing.md`.
- Windows build и non-development/release build делать только по явному запросу пользователя.
- Артефакты сохраняются под `Builds/telegram-buffer`.
- Build summary, Telegram caption и финальный ответ должны явно указывать `buildId`, source branch, short commit и dirty-tree state.

## Warm Android Build Sandbox

Постоянный build sandbox:

```text
C:\BuildWorkspaces\LostCyberHamster_Android
```

Это не git-ветка и не source of truth. Это локальная сборочная копия, в которой сохраняются тяжелые кэши:

- `Library/`;
- Unity import state;
- Gradle/Android cache, если он находится внутри сборочной среды;
- прочие generated/cache файлы, которые ускоряют повторную сборку.

## 1. Проверка и создание стенда

Entry point должен проверить, существует ли `BuildSandbox`.

Если sandbox отсутствует:

- создать директорию;
- скопировать в нее source/config из `SourceWorktree`;
- не создавать `.git` как рабочую историю sandbox-а;
- запустить первичный прогрев Unity/Android build pipeline.

Если sandbox существует:

- не удалять `Library/`;
- не удалять кэши;
- использовать его как warm workspace для следующего build snapshot.

## 2. Прогрев

Первый запуск на ноутбуке холодный:

- Unity открывает sandbox в batchmode;
- создает `Library/`;
- импортирует assets;
- проверяет Android build support, SDK, NDK, JDK и Gradle;
- выполняет первую Android-сборку.

Последующие запуски теплые:

- source/config пересинхронизируются поверх sandbox;
- Unity доимпортирует только изменившуюся дельту;
- APK собирается быстрее, чем из холодного worktree.

## 3. Синхронизация source snapshot

Синхронизировать из `SourceWorktree` в `BuildSandbox` только source/config часть проекта:

- `Assets/`;
- `Packages/`;
- `ProjectSettings/`;
- необходимые build/config файлы репозитория;
- `tools/`, если build entrypoint или Unity build helper нужны внутри sandbox.

Не синхронизировать и не удалять в sandbox:

- `Library/`;
- `Temp/`;
- `Logs/`;
- `Builds/`;
- `obj/`;
- `.git/`;
- локальные `.env*`, secrets и machine-specific config.

Синхронизация выбранных source-директорий должна быть зеркальной внутри этих директорий, чтобы удаленные из source ассеты/скрипты не оставались ghost-файлами в sandbox.

Важно: mirror никогда не должен применяться к корню sandbox целиком, иначе будут удалены `Library/` и кэши.

## 4. Build manifest

Каждый APK должен содержать manifest, по которому device logs связываются с исходным worktree и конкретным состоянием кода. Branch `builds` или sandbox path не являются смысловым идентификатором билда.

Минимальные поля manifest:

```json
{
  "buildId": "2026-07-04_2015_android_deadend-fix_a1b2c3_dirty",
  "buildLabel": "deadend-fix",
  "sourceWorktree": "C:/Main/crystal_wave/LostCyberHamster_2025/.worktrees/deadend-fix",
  "sourceBranch": "task/deadend-fix",
  "sourceCommit": "a1b2c3d",
  "sourceDirty": true,
  "sourceDiffHash": "7f91...",
  "sandboxRoot": "C:/BuildWorkspaces/LostCyberHamster_Android",
  "builtAtUtc": "2026-07-04T17:15:00Z",
  "platform": "Android",
  "development": true
}
```

Manifest должен попасть в билд как runtime-readable resource и затем добавляться в Android device log metadata. Тогда collector/read API сможет фильтровать логи по `buildId`, `sourceBranch`, `sourceCommit` и `buildLabel`.

Если source worktree dirty, сборка разрешена для dev APK, но это обязательно отражается в manifest, Telegram caption и финальном ответе агента.

## 5. Сборка APK

Целевой repo entrypoint:

```text
tools/build/build_android_telegram.ps1
```

Entry point создаёт/актуализирует warm sandbox, генерирует build manifest, обновляет sandbox-only `Resources/Diagnostics/device_log_settings.json`, запускает repo-owned `LostCyberHamsterBuildAutomation` и возвращает JSON с APK/metadata. Telegram-публикация остаётся ответственностью skill-а.

Параметр `-UnityLauncher`:

- `Auto` — Unity CLI, direct Editor fallback;
- `Cli` — только Unity CLI;
- `Editor` — прямой запуск Unity Editor.

`-PreflightOnly` проверяет Unity, Android modules, signing config и выбранный launcher без сборки.

Не дробить pipeline на много скриптов заранее. Helper-скрипты рядом в `tools/build/` добавлять только когда один entrypoint станет реально перегруженным.

Ожидаемый контракт:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build_android_telegram.ps1 `
  -SourceWorktree "<path-to-source-worktree>" `
  -SandboxRoot "C:\BuildWorkspaces\LostCyberHamster_Android" `
  -BuildLabel "<short-human-label>" `
  -UnityLauncher Auto `
  -Development `
  -Json
```

Entry point отвечает за:

1. Проверить prerequisites: Unity, Android module/SDK/NDK/JDK, доступность source worktree.
2. Создать sandbox, если его нет.
3. Синхронизировать source/config из `SourceWorktree` в sandbox, сохранив `Library/` и кэши.
4. Сгенерировать build manifest.
5. Запустить Unity Android development build из sandbox через выбранный launcher.
6. Вернуть JSON с путём к APK, launcher и build metadata для Telegram skill.

## 6. Публикация в Telegram

Telegram skill отвечает за публикацию:

- берет APK и summary от repo entrypoint;
- предпочитает Bot API delivery, если локальная конфигурация уже настроена;
- для Telegram Desktop delivery требует подтверждения пользователя прямо перед отправкой файла;
- если Telegram-публикация недоступна, останавливается после сборки и сообщает локальный путь к APK.

## Процесс для агента

Когда пользователь просит прогреть сборочный стенд, собрать APK или отправить билд в Telegram:

1. Прочитать этот документ и skill `publish-build-to-telegram-buffer`.
2. Определить `SourceWorktree`: текущий workspace, task-worktree или путь, явно указанный пользователем.
3. Запустить/создать warm sandbox через `tools/build/build_android_telegram.ps1`.
4. Убедиться, что JSON результата содержит APK path, `buildId`, source branch/commit и dirty state.
5. Опубликовать APK через Telegram skill.
6. В ответе пользователю указать APK path, `buildId`, source branch/commit, dirty state и способ доставки.

Агент не должен:

- переносить изменения между ветками только ради сборки;
- создавать постоянную build-ветку как source of truth;
- удалять `Library/` в warm sandbox без явной причины;
- коммитить sandbox, `Library`, APK artifacts, local secrets или Telegram config.

## Инфраструктура публикации

- `build_unity_player.ps1` делает preflight, ставит Unity build helper, собирает Addressables и player build.
- Полностью автоматическая публикация идет через локальный Telegram Bot API, если конфиг уже настроен.
- Telegram Desktop delivery требует подтверждения пользователя прямо перед отправкой файла.
- Если Telegram-публикация недоступна, остановиться после сборки и сообщить локальный путь к артефакту.
