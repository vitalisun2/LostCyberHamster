# Сборка игры и публикация в Telegram

Владелец темы: процесс сборки локального тестового билда LostCyberHamster и публикации APK в Telegram-канал `LostCyberHamster builds`.

## Источник правды

- Использовать локальный Codex skill `publish-build-to-telegram-buffer`: `%USERPROFILE%\.codex\skills\publish-build-to-telegram-buffer`.
- Перед запуском всегда читать `SKILL.md` и `references/lost-cyber-hamster-context.md` внутри skill: там актуальные пути, preflight, ограничения Telegram и детали инфраструктуры.
- Не искать и не печатать Telegram secrets; использовать только уже настроенную локальную конфигурацию или явно переданные пользователем данные.
- Целевая схема быстрых Android-сборок: skill является тонким локальным orchestrator-ом, а воспроизводимая build-логика живет в репозитории в `tools/build/`.

## Где выполнять

- После feature-задачи или bug fix источником сборки является тот worktree, состояние которого нужно отдать на телефон: task-worktree `.worktrees/<slug>`, `integration/unity-live` или текущий dirty worktree по явному намерению пользователя.
- `integration/unity-live` использовать как общий Unity-стенд для проверки под lock из Task Branch workflow. Для тестового APK не переносить изменения в отдельную build-ветку только ради сборки: build pipeline должен уметь собрать snapshot любого source worktree.
- Bug regression / analysis-only workflow сам по себе не запускает сборку и публикацию; это отдельный запрос после доказанного root cause или завершённого fix.

## Дефолт сборки

- Если пользователь просит "собери билд", "собери APK", "отправь APK" или "отправь в Telegram", по умолчанию собирать Android development APK (`-Development`).
- Windows build и non-development/release build делать только по явному запросу пользователя.
- Артефакты сохраняются под `Builds/telegram-buffer`; build summary, Telegram caption и финальный ответ должны явно указывать Git branch, short commit и dirty-tree state того worktree, из которого собран билд.

## Warm Android build sandbox

Цель: быстро собирать APK из любого worktree без cherry-pick/merge в отдельную build-ветку и без холодного пересоздания Unity `Library`.

Постоянный build sandbox:

```text
C:\BuildWorkspaces\LostCyberHamster_Android
```

Это не git-ветка и не source of truth. Это прогретая сборочная копия проекта, в которой сохраняются тяжелые локальные кэши Unity/Android:

- `Library/`;
- Unity-import state;
- Gradle/Android cache, если он находится внутри сборочной среды;
- прочие локальные generated/cache файлы, которые ускоряют повторную сборку.

Skill перед сборкой берет source worktree и синхронизирует в sandbox только source/config часть проекта:

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

Синхронизация source-директорий должна быть зеркальной внутри выбранных директорий, чтобы удаленные из source ассеты/скрипты не оставались ghost-файлами в sandbox. При этом mirror никогда не должен применяться к корню sandbox целиком, иначе будут удалены `Library/` и кэши.

Первый запуск на ноутбуке холодный: создать sandbox, скопировать source/config, открыть Unity batchmode, дать создать `Library` и выполнить первую Android-сборку. Последующие запуски теплые: пересинхронизировать source/config, Unity доимпортирует только дельту и собирает быстрее.

## Build entrypoint

Целевой repo entrypoint:

```text
tools/build/build_android_telegram.ps1
```

Если этого скрипта еще нет, агент, который внедряет warm sandbox pipeline, должен создать его по этому документу. Не дробить pipeline на много скриптов заранее. Helper-скрипты рядом в `tools/build/` добавлять только когда один entrypoint станет реально перегруженным.

Ожидаемый контракт entrypoint:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\build\build_android_telegram.ps1 `
  -SourceWorktree "<path-to-source-worktree>" `
  -SandboxRoot "C:\BuildWorkspaces\LostCyberHamster_Android" `
  -BuildLabel "<short-human-label>" `
  -Development `
  -Json
```

Entry point отвечает за:

1. Проверить prerequisites: Unity, Android module/SDK/NDK/JDK, доступность source worktree.
2. Создать sandbox, если его нет.
3. Синхронизировать source/config из `-SourceWorktree` в sandbox, сохранив `Library/` и кэши.
4. Сгенерировать build manifest.
5. Запустить Unity Android development build из sandbox.
6. Вернуть JSON с путем к APK и build metadata для Telegram skill.

Telegram skill отвечает за публикацию: берет APK и summary от repo entrypoint, затем отправляет файл в `Буфер`/Telegram выбранным настроенным способом.

## Build manifest

Каждый APK должен содержать manifest, по которому потом можно связать device logs с исходным worktree и конкретным состоянием кода. Branch `builds` или sandbox path не являются смысловым идентификатором билда.

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

## Минимальный запуск

Текущий legacy fast path skill-а, пока repo entrypoint `tools/build/build_android_telegram.ps1` не внедрен:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\.codex\skills\publish-build-to-telegram-buffer\scripts\build_unity_player.ps1" -RepositoryRoot "<task-worktree>" -Platform Android -Development
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\.codex\skills\publish-build-to-telegram-buffer\scripts\publish_latest_apk_to_telegram_buffer.ps1" -RepositoryRoot "<task-worktree>"
```

После внедрения warm sandbox pipeline skill должен предпочитать repo entrypoint `tools/build/build_android_telegram.ps1`, а legacy scripts использовать только как fallback или как внутреннюю реализацию, если это явно описано в skill.

## Инфраструктура публикации

- `build_unity_player.ps1` делает preflight, ставит Unity build helper, собирает Addressables и player build.
- Полностью автоматическая публикация идёт через локальный Telegram Bot API, если конфиг уже настроен.
- Telegram Desktop delivery требует подтверждения пользователя прямо перед отправкой файла.
- Если Telegram-публикация недоступна, остановиться после сборки и сообщить локальный путь к артефакту.
