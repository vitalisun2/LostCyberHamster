# Agent Tools

Каталог проектных инструментов LostCyberHamster. Читать только когда задача требует Unity automation, логов, test-level validation или editor tools.

## Automation Bridge

Файловый IPC для управления Unity Editor извне.

Файлы:
- `LostCyberHamster/Assets/Editor/TestLevelAutomationBridge.cs`
- `tools/invoke_open_unity_test_level.ps1`
- `tools/invoke_run_all_test_levels.ps1`

Основные команды:

```powershell
.\tools\invoke_run_all_test_levels.ps1 -TimeoutSeconds 120
.\tools\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_switch_lane' -TimeoutSeconds 120
```

Bridge-команды:
- `launch_test_level` — запуск тестового уровня с ботом.
- `recompile_scripts` — перекомпиляция скриптов по явному запросу пользователя.
- `regenerate_project_files` — пересоздание `.csproj`/`.sln`; `invoke_open_unity_test_level.ps1` вызывает автоматически.

Результат: `EditorLogs/automation/test_level_response.json` со `state`, `testResult`, `diagnosticLogPath`.

Диагностика bridge:
- `[TEST RESULT]` в `diagnostic_log.txt` — маркер завершения test level.
- Если bridge завершился без `[TEST RESULT]` и лог пустой — читать Unity `Editor.log`: вероятен compile/editor-level сбой.
- Если Unity не видит новый `.cs`, проверить `Assembly-CSharp.csproj`.
- Для новых script assets под `Assets/` не создавать `.meta` вручную; дать Unity сгенерировать их.
- При "зависшем" запуске проверить, что request/response paths находятся под реальным Unity project root: `LostCyberHamster/EditorLogs/automation`.

## Diagnostic Log

Путь: `EditorLogs/diagnostic_log.txt`.

Каналы:
- `[CH=STAB]` — fail/win и критические сигналы.
- `[CH=BOT]` — bot pipeline/select/execute/result.
- `[CH=ECO]` — экономика и итог забега.

Чтение:

```powershell
./tools/read_log_channel.ps1 -Channel STAB -Tail 120
./tools/read_log_channel.ps1 -Channel BOT -Event "EXECUTE|RESULT" -Tail 200
./tools/read_log_channel.ps1 -Channel ECO -Tail 200
./tools/read_log_channel.ps1 -Channel ALL -SummaryOnly
```

Рекомендуемый порядок анализа: `STAB` → `BOT` → `ECO`.

## Test Levels

Паттерны: `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`.

Источник правды по тестовым уровням: все `test*.json` под `LostCyberHamster/Assets/Content/locations/**/levels/**`.

| Уровень | Адрес |
|---|---|
| SwitchLane | `01_New_York/Morning/test_switch_lane` |
| Jump Over | `01_New_York/Morning/test_jump_over` |
| SuperJump Over | `01_New_York/Morning/test_superjump_over` |
| Jump On Roof | `01_New_York/Morning/test_jump_on_roof` |
| Super Jump On Roof | `01_New_York/Morning/test_super_jump_on_roof` |

Правила:
- Имена test-паттернов: `test_{action}_{NN}_{description}`.
- Линии: `y = -1.8` top, `y = -2.8` bottom.
- Ссылка из level JSON: `{"ref": "имя_паттерна", "spriteSeed": 0, "overrides": []}`.
- `WIN`/`FAIL`/звёзды не являются критерием регрессии. Проверять фактические actions по `description`: `should <action>` обязателен, `should not <action>` запрещён.
- Helper-паттерны без `should` / `should not`, например `relief`, не считать проверяемыми сценариями.

## Unity Editor Tools

- `Tools → Test Level → Launch...` — ручной запуск test level.
- `Tools → Diagnostics → View/Clear/Open Log` — просмотр и очистка diagnostic log.
- `Tools → Level Tilemap Editor` — визуальный редактор уровней.
- `Tools → Obstacle Animations → Import From Dropbox` — импорт PNG-последовательностей из `C:\Dropbox\exchange\crystal_wave\sprites\sprites_for_animation`.
- `Tools → Build Assets (Editor only / Android / IOS)` — сборка asset bundles.
- `Tools → Levels → Sync Level Assets` — синхронизация Addressables.
- `Tools → Player Data → Export JSON / Reset` — экспорт и сброс данных игрока.
- `Tools → Batch Rename` — массовое переименование ассетов.
- `Tools → Check Missing References in Prefabs` — поиск битых ссылок.
- `Tools → Update prefabs in the scene` — откат prefab instances к шаблону.
- `Tools → Check OnDestroy in Scripts` — поиск утечек подписок без `OnDestroy`.
- `Tools → Export/Import Tilemap to JSON` — экспорт/импорт tilemap.

## PowerShell Scripts

| Скрипт | Назначение |
|---|---|
| `tools/invoke_open_unity_test_level.ps1` | Рекомпиляция, запуск одного теста, получение результата |
| `tools/invoke_run_all_test_levels.ps1` | Запуск всех `test*.json` уровней |
| `tools/read_log_channel.ps1` | Чтение `STAB`/`BOT`/`ECO` каналов |
| `tools/migrate_levels.ps1` | Миграция уровней в reference-based формат |
| `tools/rename_to_snake_case.ps1` | Переименование анимационных файлов перед импортом |
| `tools/cleanup_old_logs.ps1` | Очистка логов старше 3 дней |
