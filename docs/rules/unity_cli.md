# Unity CLI в LostCyberHamster

Актуально на 2026-08-26.

## Решение

Unity CLI и `com.unity.pipeline` внедряем поэтапно.

- Текущий файловый `TestLevelAutomationBridge` и build pipeline остаются рабочими.
- CLI сначала используем для диагностики, коротких Editor-команд и AI/MCP-интеграции.
- Стабильные повторяемые операции оформляем как `[CliCommand]`.
- Миграцию существующей automation делаем только после проверки равного поведения.

## Состав инструмента

Unity CLI состоит из двух слоёв:

- `unity` — отдельная программа. Управляет Editor-версиями, модулями, проектами, авторизацией, сборками и тестами.
- `com.unity.pipeline` — пакет проекта. Запускает локальный HTTP API в Editor или development Player и принимает команды CLI.

Ключевые возможности:

- `unity status` — состояние подключённых Editor-процессов.
- `unity command` — список и запуск встроенных или проектных команд.
- `unity run --command <name>` — запуск команды в headless Editor.
- `unity command eval` и `unity command eval_file` — выполнение C# в живом Editor/Player без project recompile и domain reload.
- `unity mcp` — публикация Editor-команд как MCP tools для AI-агента.
- `--format json` — стабильный машинный результат; `--format ndjson` — прогресс долгих операций.

## Состояние проекта

- Editor: `6000.2.6f2`.
- Pipeline требует Unity 6.0 или новее: текущая версия совместима.
- Unity CLI: `1.0.0-beta.6`, установлен в `PATH`; авторизация активна.
- `com.unity.pipeline`: `0.5.0-exp.1`, зафиксирован в `Packages/manifest.json`.
- Pipeline server запускается автоматически; `status`, `editor_status` и read-only `eval` проверены.
- Для Codex установлены Unity CLI skill и проектная MCP-конфигурация. MCP активируется после перезапуска Codex.
- Проектные команды `lch_*` доступны через терминал; test-level команды используют текущую очередь bridge.
- Unity CLI и Pipeline имеют experimental/beta-статус. Команды и API могут меняться.

Обновление Editor для CLI не требуется. Отдельно нужно запланировать проверяемый переход на Unity 6.3 LTS: ветка 6.2 больше не поддерживается. Этот переход не блокирует пилот CLI.

## Первичная установка на Windows

```powershell
winget install Unity.CLI
unity --version
unity auth login

Set-Location .\LostCyberHamster
unity pipeline install
unity pipeline list
unity status
unity command --format json
unity skill install codex
unity mcp configure codex --project-path (Get-Location)
```

После `unity pipeline install` дождаться Unity recompile. Изменения `Packages/manifest.json`, `Packages/packages-lock.json` и Unity-generated файлов проверить и коммитить вместе.

После изменения MCP-конфигурации перезапустить Codex.

`unity --help` и `unity <command> --help` — источник правды для установленной версии CLI.

## Рабочий порядок агента

Перед изменением сцен, prefab или assets:

1. Выполнить `unity status --format json` из `LostCyberHamster/`.
2. При состоянии `ready` найти команду через `unity command --query <term> --format json`.
3. Изменить объект через CLI и сохранить через `save_scene` или `save_all`.
4. Если Editor недоступен, выполнить `unity pipeline list --format json` и проверить Safe Mode.

При доступном Editor `.unity`, `.prefab` и `.asset` YAML менять через Unity CLI, не вручную.
При нескольких Editor-процессах всегда передавать `--project-path`.

## Где применять в проекте

### 1. Диагностика живого Editor

Использовать для:

- проверки compile, Play Mode и Editor status;
- чтения состояния сцены, объектов, коллайдеров и runtime-сервисов;
- быстрого подтверждения гипотезы через `eval`;
- получения структурированного результата без разбора Unity UI.

`eval` применять для разовой диагностики. Повторяемый `eval` превращать в именованную `[CliCommand]`.

### 2. Проектные команды

Статический метод с `[CliCommand]` — тонкий адаптер над существующей проектной логикой. Команда принимает явные параметры и возвращает простой сериализуемый результат.

Доступные команды:

- `lch_editor_status` — версия Editor, compile/play state, project path;
- `lch_project_regenerate_files` — текущая логика `regenerate_project_files`;
- `lch_test_level_launch` — запуск одного test level;
- `lch_test_level_status` — состояние и итог текущего прогона;
- `lch_diagnostics_summary` — краткий итог каналов `STAB`, `BOT`, `ECO`.

```powershell
unity command --query lch --detail compact --format json
unity command lch_editor_status --format json
unity command lch_diagnostics_summary --format json
unity command lch_test_level_launch --level_address '01_New_York/Morning/test_switch_lane' --format json
unity command lch_test_level_status --format json
```

Следующие кандидаты:

- `lch_level_assets_sync` — вызов существующей Addressables-синхронизации;
- `lch_skins_validate` — вызов существующего валидатора skin assets.

Долгий test-level прогон сначала оставить на `TestLevelAutomationBridge`. Переносить после проверки Play Mode transitions, domain reload, timeout и сохранения `[TEST RESULT]` semantics.

### 3. AI-агенты

`unity mcp` открывает встроенные и `[CliCommand]` команды как MCP tools. Агент получает имя, описание и схему аргументов.

Рекомендуемый порядок:

1. Подключить Pipeline к открытому Editor.
2. Проверить команды через `unity command --format json`.
3. Подключить `unity mcp` к поддерживаемому AI-клиенту.
4. Открывать агенту узкие проектные команды.
5. Оставить `eval` диагностическим fallback.

Команду настройки клиента брать из `unity mcp configure --help`: список поддерживаемых клиентов зависит от версии CLI.

### 4. CI и headless automation

Подходящие случаи:

- `unity run --command <name>` для одной проектной операции;
- `unity test` для явно запрошенного Edit/Play Mode запуска;
- `unity build` как возможный нижний слой repo build entrypoint;
- JSON/NDJSON output и exit codes для PowerShell-скриптов.

Проектные правила сохраняются:

- финальный C# gate остаётся `regenerate_project_files` и `dotnet build --no-restore`;
- Unity recompile, Test Runner и дополнительные проверки запускаются только по явному запросу;
- Android build идёт через `tools/build/build_android_telegram.ps1` и warm sandbox;
- CLI не заменяет build manifest, source snapshot, signing и Telegram delivery.

## Правила безопасности

- Pipeline Player включать только в development/QA build. Сервер локальный и выключен по умолчанию.
- `eval` выполняет произвольный C# на main thread. Использовать security token и только доверенный локальный код.
- Для изменения assets, сцен и project settings предпочитать именованные `[CliCommand]` с проверкой входных данных.
- При нескольких Editor-процессах указывать `--project-path`.
- В automation использовать `--format json` или `--format ndjson`, проверять exit code и `stderr`.
- Версию CLI и Pipeline фиксировать в diagnostic output. Перед обновлением читать release notes.

## Следующие шаги

1. После перезапуска Codex проверить `lch_editor_status` через MCP.
2. Сравнить CLI и файловый запуск на тех же test levels.
3. Добавить `lch_level_assets_sync` и `lch_skins_validate` по реальной потребности.
4. Удалять старый bridge только отдельным решением после стабильного периода.

## Официальные источники

- [Unity CLI](https://docs.unity.com/en-us/unity-cli)
- [Unity CLI reference](https://docs.unity.com/en-us/unity-cli/unity-cli-reference)
- [Unity Pipeline package](https://docs.unity.com/en-us/unity-production-pipeline/local-tools-cli/unity-pipeline-package)
- [Unity CLI announcement](https://unity.com/blog/meet-the-unity-cli)
- [Unity CLI release notes](https://docs.unity.com/en-us/hub/release-notes)
- [Unity 6 release support](https://unity.com/releases/unity-6/support)
