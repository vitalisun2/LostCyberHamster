# Publish Build To Telegram Buffer - skill planning

Дата: 2026-07-01

## Цель

Сделать reusable skill для Codex, который по запросу пользователя:

1. Собирает актуальный Unity build проекта LostCyberHamster.
2. Сохраняет артефакты в локальную папку `Builds/...`.
3. Публикует Android APK, а при необходимости и PC build zip, в Telegram чат/канал `Буфер`, чтобы пользователь мог открыть Telegram на телефоне, скачать APK и сразу проверить игру.

Рабочее название skill: `publish-build-to-telegram-buffer`.

## Статус на 2026-07-01

Skill создан локально:

`C:\Users\Vitaly\.codex\skills\publish-build-to-telegram-buffer`

Состав:

- `SKILL.md` - инструкция skill и workflow.
- `agents/openai.yaml` - metadata для Codex.
- `references/lost-cyber-hamster-context.md` - собранный контекст проекта, Unity build и Telegram.
- `scripts/build_unity_player.ps1` - helper для preflight и Unity batchmode build.
- `scripts/send_telegram_document.ps1` - optional Bot API helper.
- `scripts/zip_windows_build.ps1` - optional Windows build zip helper.
- `assets/LostCyberHamsterBuildAutomation.cs` - Unity Editor build template.

Проверено:

- Структура skill прошла ручную проверку, эквивалентную `quick_validate.py`.
- Официальный `quick_validate.py` не запустился только из-за отсутствия `PyYAML` в bundled Python.
- PowerShell-скрипты проходят parser check.
- `build_unity_player.ps1` запускается через `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ...`.
- Безопасный preflight останавливается до запуска Unity на 5 project-owned `UnityEditor` references в `Assets/Scripts`.
- `Start-Process -FilePath "C:\Users\Vitaly\AppData\Roaming\Telegram Desktop\Telegram.exe"` успешно запускает Telegram Desktop; процесс виден как `Telegram`, окно `Telegram`.
- Первый успешный Android APK build выполнен через Unity batchmode:
  - APK: `C:\Main\crystal_wave\LostCyberHamster_2025\Builds\telegram-buffer\2026-07-01_20-24-43_integration_unity-live_fafe477f\LostCyberHamster.apk`
  - Размер: `62124794` bytes.
  - `Building Gradle project` занял `615046 ms` (~10 минут 15 секунд).
  - Первый неуспешный проход был вызван bug в wrapper-е: PowerShell не ждал GUI-процесс `Unity.exe`; helper исправлен на `Start-Process -Wait -PassThru`.

Текущий блокер реального APK build:

- Нужно исправить или закрыть `#if UNITY_EDITOR` ссылки `using UnityEditor;` в runtime scripts:
  - `Assets/Scripts/Common/HelpMethods.cs`
  - `Assets/Scripts/GameEngine/Controllers/TransformAnimatorController.cs`
  - `Assets/Scripts/SharedCore/VibrationManager.cs`
  - `Assets/Scripts/System/ObstacleFactory.cs`
  - `Assets/Scripts/System/UserSettings.cs`

## Текущий локальный контекст

- Репозиторий: `C:\Main\crystal_wave\LostCyberHamster_2025`.
- Unity project: `C:\Main\crystal_wave\LostCyberHamster_2025\LostCyberHamster`.
- Unity version: `6000.2.6f2`.
- Текущая ветка: `integration/unity-live`.
- В working tree уже есть незакоммиченные изменения tutorial-фичи. Skill должен явно фиксировать `git status` в подписи build-а и не требовать clean tree для локального тестового APK, но перед публикацией показывать пользователю, что билд собран из dirty tree.

## Что доступно для Unity build

### Unity и platform modules

Найдено:

- `C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Unity.exe`
- `AndroidPlayer`
- `windowsstandalonesupport`
- `AndroidPlayer\SDK`
- `AndroidPlayer\NDK`
- `AndroidPlayer\OpenJDK`

Вывод: Android APK и Windows player технически можно собирать на этой машине.

### Scenes

В `ProjectSettings/EditorBuildSettings.asset` включены:

- `Assets/Scenes/Bootstrap.unity`
- `Assets/Scenes/Menu.unity`
- `Assets/Scenes/Game.unity`

Это минимальный список сцен, который должен использовать player build.

### Android settings

В `ProjectSettings/ProjectSettings.asset`:

- `productName`: `LostCyberHamster`
- `companyName`: `vues`
- Android package id: `com.vues.LostCyberHamster`
- `AndroidBundleVersionCode`: `1`
- `AndroidMinSdkVersion`: `23`
- `AndroidTargetSdkVersion`: `0` (Unity default)
- `AndroidTargetArchitectures`: `2` (ARM64)
- `AndroidKeystoreName`: пусто
- `AndroidKeyaliasName`: пусто

В `Assets/Settings/Build Profiles/Android™.asset`:

- Android profile есть.
- `m_BuildTarget: 13`
- `m_Development: 0`
- `m_ExportAsGoogleAndroidProject: 0`
- `m_BuildAppBundle: 0`, по отчёту explorer-а, значит целевой формат - APK, не AAB.

Для тестового Telegram build-а можно собирать debug/development APK без release keystore.

### Windows build

Windows playback module установлен, но отдельного Windows Build Profile в проекте нет. Для skill лучше собирать Windows через явный `BuildTarget.StandaloneWindows64` и складывать результат в папку, затем архивировать в `.zip`.

## Что уже есть в проекте

### Asset bundles

Есть `Assets/Editor/AssetBundlesManager.cs`:

- `Tools/Build Assets (Android)`
- `Tools/Build Assets (IOS)`
- `Tools/Build Assets (Editor only)`

Но это только `BuildPipeline.BuildAssetBundles`, не сборка `.apk`/`.exe`. Skill не должен строиться вокруг этого menu item как основного player-build workflow.

### Addressables

Проект активно использует Addressables. По отчёту explorer-а:

- active builder: `BuildScriptPackedMode`
- `m_BuildAddressablesWithPlayerBuild: 0`

Вывод: перед player build нужно отдельным шагом собирать Addressables content, например через editor method с `AddressableAssetSettings.BuildPlayerContent()`.

### Player build entrypoint

Не найден штатный editor entrypoint для:

- `BuildPipeline.BuildPlayer`
- `BuildReport`
- batchmode Android APK build
- batchmode Windows player build

Вывод: для skill нужен небольшой project-specific Editor build script или bundled template, который skill установит/обновит в `Assets/Editor`.

## Потенциальные блокеры первого player build

В runtime-коде найдены прямые `using UnityEditor`:

- `LostCyberHamster/Assets/Scripts/Common/HelpMethods.cs`
- `LostCyberHamster/Assets/Scripts/System/UserSettings.cs`
- `LostCyberHamster/Assets/Scripts/GameEngine/Controllers/TransformAnimatorController.cs`
- `LostCyberHamster/Assets/Scripts/System/ObstacleFactory.cs`
- `LostCyberHamster/Assets/Scripts/SharedCore/VibrationManager.cs`

Это может компилироваться в Editor, но ломать Android/Windows player build. Первый запуск skill должен иметь preflight:

1. Найти `UnityEditor` references вне `Assets/Editor`.
2. Если они не закрыты `#if UNITY_EDITOR`, остановиться и дать список файлов.
3. Исправлять это отдельной задачей, не смешивая с публикацией build-а.

## Что доступно для Telegram delivery

## Выбранный MVP-подход

Для первого рабочего skill выбираем `Telegram Desktop first`.

Причина:

- Telegram Desktop уже установлен на машине пользователя.
- Для desktop-пути не нужны bot token и `chat_id`.
- APK можно отправить как обычный файл даже если он окажется крупнее лимитов cloud Bot API.
- Пользователь прямо согласовал этот маршрут.

Bot API остаётся вторым уровнем автоматизации, когда пользователь отдельно настроит bot token и доступ бота к `Буфер`.

Desktop MVP не должен пытаться быть полностью безусловно-автоматическим. Он должен:

1. Собрать APK.
2. Проверить, что Telegram Desktop доступен.
3. Попросить пользователя открыть Telegram вручную, если Computer Use не может запустить или захватить окно.
4. Найти чат/канал `Буфер`.
5. Перед фактической отправкой файла показать пользователю точное действие и дождаться подтверждения.

### Дополнительная проверка Computer Use

Проверено в текущей сессии:

- Computer Use runtime доступен.
- `sky.list_apps()` работает.
- `Telegram` не появляется в `list_apps()`, когда не запущен.
- Попытка `sky.launch_app` для `C:\Users\Vitaly\AppData\Roaming\Telegram Desktop\Telegram.exe` вернула:

```text
GetCursorPos failed: Access is denied. (0x80070005)
```

Вывод для skill:

- Нельзя рассчитывать, что Codex всегда сможет сам открыть Telegram.
- Skill должен иметь fallback-инструкцию: попросить пользователя открыть Telegram Desktop и перейти в чат `Буфер`, затем продолжить через Computer Use с уже открытым окном.
- Если Computer Use не может управлять открытым Telegram, skill должен остановиться после сборки APK и дать пользователю путь к файлу для ручной отправки.

### Telegram Desktop

Найдено:

- Start App: `Telegram`, AppID `Telegram.TelegramDesktop`
- exe: `C:\Users\Vitaly\AppData\Roaming\Telegram Desktop\Telegram.exe`
- Telegram Desktop version по отчёту explorer-а: `6.9.3.0`
- Локальный профиль `tdata` есть.
- Telegram сейчас не запущен.

Computer Use runtime доступен. В пассивном `list_apps()` Telegram не появился, вероятно потому что не запущен. Для GUI fallback skill может запускать `Telegram.exe` явным путём и затем работать через Computer Use.

Ограничение: отправка файла в Telegram через GUI - это внешний side effect и upload файла. Нужна явная action-time confirmation с точным файлом, размером и destination `Буфер`.

### Telegram Bot API

Самый надёжный reusable-вариант - отправлять документ через Telegram Bot API `sendDocument`.

Требования:

- Пользователь заранее создаёт bot token через BotFather.
- Bot добавлен в канал/чат `Буфер` и имеет право отправлять сообщения.
- Skill получает `bot token` и `chat_id` явно от пользователя или из явно названной пользователем переменной/конфиг-файла.
- Skill не ищет и не печатает секреты сам.

Риски:

- Официальный cloud Bot API имеет лимиты на upload размера файла. APK может оказаться больше лимита.
- Если APK больше лимита, варианты:
  - использовать Telegram Desktop GUI fallback;
  - настроить local Telegram Bot API server;
  - отправить ссылку на артефакт из другого хранилища, если оно появится.

### Не найдено / не использовать как базовый путь

- Готовых project config для Telegram Bot API в workspace не найдено.
- `telegram-bot-api`, `tdl`, `telegram-cli` не найдены.
- Не надо сканировать окружение на предмет токенов без явного разрешения пользователя.

## Предлагаемая архитектура skill

### Skill name

`publish-build-to-telegram-buffer`

### Trigger description

Использовать, когда пользователь просит собрать Unity build LostCyberHamster и доставить APK/PC build в Telegram `Буфер`, Telegram-канал, чат или личный буфер для быстрой проверки на телефоне.

### Состав skill

```text
publish-build-to-telegram-buffer/
├── SKILL.md
├── agents/openai.yaml
├── scripts/
│   ├── build_unity_player.ps1
│   ├── send_telegram_document.ps1
│   └── zip_windows_build.ps1
└── assets/
    └── LostCyberHamsterBuildAutomation.cs
```

Опционально вместо PowerShell можно использовать Node.js scripts, но PowerShell проще для Unity/Windows путей.

### `assets/LostCyberHamsterBuildAutomation.cs`

Editor-only C# template, который копируется в:

`LostCyberHamster/Assets/Editor/LostCyberHamsterBuildAutomation.cs`

Ответственность:

- Прочитать аргументы командной строки:
  - `-codexBuildOutput`
  - `-codexBuildDevelopment true|false`
  - `-codexBuildPlatforms android,windows`
- Собрать Addressables content для выбранного target.
- Собрать Android APK через `BuildPipeline.BuildPlayer`.
- Собрать Windows x64 player через `BuildPipeline.BuildPlayer`.
- Вернуть non-zero exit code при ошибке.
- Записать machine-readable summary JSON рядом с build artifacts.

### `scripts/build_unity_player.ps1`

Ответственность:

1. Найти Unity executable.
2. Проверить project path.
3. Проверить enabled scenes.
4. Проверить runtime `UnityEditor` references.
5. При необходимости установить/обновить `LostCyberHamsterBuildAutomation.cs`.
6. Запустить Unity batchmode:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Unity.exe" `
  -batchmode `
  -quit `
  -projectPath "C:\Main\crystal_wave\LostCyberHamster_2025\LostCyberHamster" `
  -buildTarget android `
  -executeMethod LostCyberHamster.Editor.LostCyberHamsterBuildAutomation.BuildAndroidApk `
  -codexBuildOutput "C:\Main\crystal_wave\LostCyberHamster_2025\Builds\telegram-buffer\<stamp>" `
  -logFile "C:\Main\crystal_wave\LostCyberHamster_2025\Builds\telegram-buffer\<stamp>\unity-android.log"
```

Для Windows аналогично с `-buildTarget win64`.

Важно: официальный Unity manual подтверждает `-batchmode`, `-executeMethod`, `-buildTarget`, `-activeBuildProfile` и `-build` как supported command-line workflow.

### `scripts/send_telegram_document.ps1`

Ответственность:

- Получить явный `-FilePath`, `-ChatId`, `-BotToken`.
- Не логировать token.
- Проверить размер файла.
- Отправить через Bot API `sendDocument` multipart/form-data.
- Вернуть response JSON и exit code.

Если Bot API недоступен или файл слишком большой:

- Не пытаться обходить лимит молча.
- Вернуть понятную ошибку и предложить GUI fallback.

### GUI fallback через Telegram Desktop

Skill-инструкция, не script:

1. Проверить, запущен ли Telegram Desktop.
2. Если не запущен, сначала попробовать открыть через командную строку:

```powershell
Start-Process -FilePath "C:\Users\Vitaly\AppData\Roaming\Telegram Desktop\Telegram.exe"
```

3. После запуска подождать окно Telegram и попробовать подхватить его через Computer Use.
4. Если запуск через командную строку или захват окна упал, попросить пользователя открыть Telegram Desktop вручную.
5. Через Computer Use найти окно Telegram.
6. Найти чат/канал `Буфер`.
7. Перед upload/send вывести пользователю:
   - имя файла;
   - размер;
   - destination;
   - что будет отправлено.
8. Получить явное подтверждение.
9. Прикрепить файл и отправить.
10. Проверить, что сообщение появилось в `Буфер`.

Причина подтверждения: это upload файла и representational communication to third party.

## Детальный дизайн MVP skill

### Основной сценарий

Пользователь пишет:

```text
Используй $publish-build-to-telegram-buffer: собери Android APK и отправь через Telegram Desktop в Буфер.
```

Skill делает:

1. `PreflightBuildEnvironment`
   - Проверяет Unity path.
   - Проверяет Android module, SDK, NDK, OpenJDK.
   - Проверяет scenes.
   - Проверяет runtime `UnityEditor` references.
   - Проверяет dirty git status и сохраняет его в build summary.

2. `BuildAndroidApk`
   - Создаёт output folder.
   - Собирает Addressables.
   - Собирает APK.
   - Сохраняет Unity log и summary.

3. `PrepareTelegramDesktop`
   - Проверяет наличие `Telegram.exe`.
   - Проверяет, видит ли Computer Use окно Telegram.
   - Если окна нет, пробует открыть `Telegram.exe` через командную строку.
   - Если открыть или захватить окно не удалось, просит пользователя открыть Telegram вручную.

4. `FindBufferChat`
   - Ищет `Буфер` в Telegram.
   - Не логирует список приватных чатов.
   - Если найдено несколько совпадений, просит пользователя выбрать/уточнить.

5. `ConfirmUpload`
   - Показывает:
     - path APK;
     - size;
     - branch;
     - short commit;
     - dirty tree flag;
     - destination `Буфер`.
   - Ждёт явного подтверждения.

6. `SendFile`
   - Прикрепляет APK.
   - Отправляет.
   - Проверяет появление сообщения или хотя бы отсутствие явной ошибки Telegram.

7. `ReportResult`
   - Пишет путь к APK.
   - Пишет, отправлено ли в Telegram.
   - Пишет log path.

### Stop conditions

Skill обязан остановиться и не отправлять файл, если:

- APK не собрался.
- Unity build завершился с ошибкой.
- APK не найден по ожидаемому пути.
- Telegram Desktop не открыт и Computer Use не может его открыть.
- Чат `Буфер` не найден.
- Пользователь не подтвердил upload.
- Telegram показывает login / 2FA / code prompt. Это должен делать пользователь.

### Что спрашивать у пользователя

Для desktop MVP креды обычно не нужны.

Спрашивать только если возникает конкретный блокер:

- `Открой Telegram Desktop и перейди в чат Буфер, потом напиши "готово".`
- `Telegram просит код/2FA. Введи его вручную, потом напиши "готово".`
- `Я вижу несколько совпадений "Буфер". Какой выбрать?`
- `Подтверди отправку файла <path>, размер <size>, в Telegram Буфер.`

Для Bot API режима спрашивать отдельно:

- `bot token`
- `chat_id`
- подтверждение, что bot добавлен в `Буфер` и имеет право отправлять сообщения.

### Что не делать

- Не искать токены/пароли в окружении или файлах без явного указания пользователя.
- Не печатать bot token в лог.
- Не отправлять файл без action-time confirmation.
- Не сканировать и не пересказывать содержимое приватных Telegram чатов.
- Не пытаться обходить Windows access errors через небезопасные UI-хаки.

## Предлагаемое содержимое `SKILL.md`

Frontmatter:

```yaml
---
name: publish-build-to-telegram-buffer
description: Build and publish local Unity LostCyberHamster test builds to the user's Telegram Buffer chat. Use when the user asks Codex to create an Android APK or Windows build, save it under Builds, and send or prepare it for sending through Telegram Desktop or Telegram Bot API to the Буфер chat/channel for phone testing.
---
```

Body skeleton:

```markdown
# Publish Build To Telegram Buffer

Use this skill to produce local LostCyberHamster test builds and deliver them to Telegram `Буфер`.

## Workflow

1. Preflight git, Unity, scenes, Android modules, Addressables, and player-build blockers.
2. Build Android APK first. Build Windows only if requested.
3. Verify artifacts exist and record size/branch/commit/dirty state.
4. Prefer Telegram Desktop for MVP delivery.
5. Ask for confirmation before attaching/sending any file.
6. Fall back to giving the local APK path if Telegram automation is unavailable.

## Build

Use `scripts/build_unity_player.ps1`. If the project lacks `Assets/Editor/LostCyberHamsterBuildAutomation.cs`, install the bundled asset template first.

## Telegram Desktop

Use Computer Use. If Telegram is not visible or cannot be launched because of access errors, ask the user to open Telegram Desktop manually and navigate to `Буфер`.

Before sending, confirm file path, size, branch/commit, dirty flag, and destination.

## Bot API

Use only when the user explicitly provides token and chat id or points to a safe source. Never search for secrets.
```

## `agents/openai.yaml` дизайн

```yaml
interface:
  display_name: "Publish Build To Telegram"
  short_description: "Build APK and send it to Telegram Buffer"
  default_prompt: "Use $publish-build-to-telegram-buffer to build an Android APK and send it to Telegram Буфер."
policy:
  allow_implicit_invocation: true
```

## Предлагаемые milestones

### Milestone 1 - skill skeleton

- Создать skill в `C:\Users\Vitaly\.codex\skills\publish-build-to-telegram-buffer`.
- Добавить `SKILL.md`.
- Добавить `agents/openai.yaml`.
- Добавить scripts/assets placeholders.
- Прогнать `quick_validate.py`.

### Milestone 2 - build-only

- Реализовать `LostCyberHamsterBuildAutomation.cs`.
- Реализовать `build_unity_player.ps1`.
- Добиться появления APK в `Builds/telegram-buffer/...`.
- Не трогать Telegram.

### Milestone 3 - Telegram Desktop delivery

- Реализовать Computer Use workflow в skill-инструкции.
- Проверить сценарий с уже открытым Telegram.
- Отправлять только после подтверждения.

### Milestone 4 - Bot API optional

- Добавить `send_telegram_document.ps1`.
- Работать только с явно предоставленными token/chat id.
- Использовать как preferred path для небольших APK.

## Рекомендуемый workflow skill

1. **Preflight**
   - Проверить `git status --short --branch`.
   - Проверить Unity version и modules.
   - Проверить scenes.
   - Проверить Addressables settings.
   - Проверить runtime `UnityEditor` references.
   - Проверить наличие/путь Telegram Desktop.

2. **Build**
   - Создать папку:
     `Builds/telegram-buffer/YYYY-MM-DD_HH-mm_<branch>_<shortsha>/`
   - Собрать Addressables.
   - Собрать Android APK.
   - Опционально собрать Windows x64 player и zip.
   - Сохранить логи Unity.
   - Сохранить `build-summary.json`.

3. **Verify artifacts**
   - Проверить, что APK существует.
   - Проверить размер файла.
   - Для Windows проверить zip.
   - Проверить, что Unity process завершился с code `0`.

4. **Publish**
   - Предпочесть Bot API, если пользователь явно предоставил token/chat.
   - Иначе использовать Telegram Desktop GUI fallback.
   - Перед фактической отправкой получить подтверждение.

5. **Report**
   - Путь к APK.
   - Размер.
   - Git branch/sha/dirty flag.
   - Куда отправлено.
   - Unity log path.

## Как пользователь будет использовать skill

После установки skill можно писать:

```text
Используй $publish-build-to-telegram-buffer: собери Android APK текущей ветки и отправь в Telegram Буфер.
```

Для Android + Windows:

```text
Используй $publish-build-to-telegram-buffer: собери Android APK и Windows build, сохрани в Builds и отправь APK в Буфер.
```

Если настроен Bot API:

```text
Используй $publish-build-to-telegram-buffer: собери APK и отправь через Telegram Bot API в chat_id <...>. Token возьми из <явно указанного источника>.
```

Если Bot API не настроен:

```text
Используй $publish-build-to-telegram-buffer: собери APK, открой Telegram Desktop и отправь в чат Буфер после моего подтверждения.
```

## Что нужно сделать перед реальным созданием skill

1. Решить, где хранить skill:
   - default: `%USERPROFILE%\.codex\skills\publish-build-to-telegram-buffer`
2. Создать skill через `skill-creator` / `init_skill.py`.
3. Добавить `SKILL.md`, `agents/openai.yaml`, scripts и C# template.
4. Протестировать только preflight без отправки.
5. Протестировать Android build до появления APK.
6. Протестировать Telegram Desktop fallback с ручным подтверждением.
7. Если нужен полностью автоматический путь - настроить bot token и chat id.

## Источники

- Unity Manual: Editor command line arguments reference - https://docs.unity3d.com/Manual/EditorCommandLineArguments.html
- Unity Manual: Build Profiles window reference - https://docs.unity3d.com/Manual/build-profiles-reference.html
- Telegram Bot API: `sendDocument` / file upload - https://core.telegram.org/bots/api#senddocument

## Status update 2026-07-01 - Bot API channel configured

- Telegram destination changed from draft `Buffer` naming to channel `LostCyberHamster builds`.
- Local secret config is saved outside the repository:
  `%USERPROFILE%\.codex\telegram-buffer.local.json`.
- Stored destination chat id: `-1004318695637`.
- Bot `LostCyberHamsterPublisher_bot` is visible through Bot API.
- `getChatMember` confirms bot status `administrator`.
- Bot channel permissions confirmed:
  - `can_post_messages: true`
  - `can_edit_messages: true`
  - `can_delete_messages: true`
- Skill scripts updated:
  - `test_telegram_buffer_config.ps1` handles private channels without `username`.
  - `get_telegram_chat_candidates.ps1` can discover channel ids from forwarded channel posts.
  - `send_telegram_document.ps1` fails early for cloud Bot API uploads over 50 MB.
- Current latest APK:
  `C:\Main\crystal_wave\LostCyberHamster_2025\Builds\telegram-buffer\2026-07-01_20-43-09_integration_unity-live_fafe477f\LostCyberHamster.apk`
- Current latest APK size: `59.25 MB`.
- Current blocker for full automatic Bot API publishing:
  cloud Telegram Bot API `sendDocument` limit is 50 MB, so the current APK cannot be sent through `https://api.telegram.org`.
- Next options:
  - reduce APK below 50 MB;
  - configure a local Telegram Bot API server and set `apiBaseUrl` in the local config;
  - use Telegram Desktop/manual upload fallback for this APK.

## Status update 2026-07-01 - Local Bot API server working

- Local Telegram Bot API server configured for large APK uploads.
- Local API credentials are stored outside the repository:
  `%USERPROFILE%\.codex\telegram-bot-api.local.json`.
- Telegram Buffer bot/channel config remains outside the repository:
  `%USERPROFILE%\.codex\telegram-buffer.local.json`.
- Active Bot API endpoint:
  `http://127.0.0.1:8081`.
- Docker image:
  `aiogram/telegram-bot-api:latest`.
- Docker container:
  `lostcyberhamster-telegram-bot-api`.
- Docker restart policy:
  `unless-stopped`.
- Host port binding:
  `127.0.0.1:8081->8081`.
- Container is started with local mode:
  `--local`.
- Cloud Bot API `logOut` was called before switching the bot to local Bot API.
- Local Bot API checks passed:
  - `getMe` returned `LostCyberHamsterPublisher_bot`.
  - `getChat` returned channel `LostCyberHamster builds`.
- Latest APK was sent successfully through local Bot API:
  - APK: `C:\Main\crystal_wave\LostCyberHamster_2025\Builds\telegram-buffer\2026-07-01_20-43-09_integration_unity-live_fafe477f\LostCyberHamster.apk`
  - Telegram `message_id`: `4`
  - File size: `62124786` bytes.
- Current fully automatic publish command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\Vitaly\.codex\skills\publish-build-to-telegram-buffer\scripts\publish_latest_apk_to_telegram_buffer.ps1" -RepositoryRoot "C:\Main\crystal_wave\LostCyberHamster_2025"
```

- Current build-and-publish command:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\Vitaly\.codex\skills\publish-build-to-telegram-buffer\scripts\publish_latest_apk_to_telegram_buffer.ps1" -RepositoryRoot "C:\Main\crystal_wave\LostCyberHamster_2025" -BuildFirst
```
