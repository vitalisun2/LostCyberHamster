# Agent Tools — проектные инструменты

Каталог инструментов и автоматизаций, доступных в проекте LostCyberHamster. Прочитай перед началом работы, чтобы не тратить итерации на поиск и изобретение того, что уже есть.

---

## 1. Automation Bridge (запуск тестов из-за пределов Unity)

Файловый IPC-механизм для управления Unity Editor извне.

**Файлы:**
- Реализация: `LostCyberHamster/Assets/Editor/TestLevelAutomationBridge.cs`
- PowerShell-обёртка: `invoke_open_unity_test_level.ps1`

**Как вызвать:**

Вариант A — запуск **всех** тестовых уровней разом (рекомендуется для валидации):
```powershell
.\invoke_run_all_test_levels.ps1 -TimeoutSeconds 120
```
Прогоняет все уровни из таблицы `workflow.md` (секция «Тестовые уровни бота»), компилирует один раз, печатает SUMMARY.

Вариант B — запуск одного уровня:
```powershell
.\invoke_open_unity_test_level.ps1 -LevelAddress '01_New_York/Morning/test_threat_small_notalive_road_switchlane' -TimeoutSeconds 120
```
Скрипт сам отправляет `recompile_scripts`, ждёт компиляцию, затем запускает тест.

Вариант B — ручной запрос через JSON:
- Создать файл `EditorLogs/automation/test_level_request.json`:
  ```json
  {"requestId": "<unique_id>", "command": "launch_test_level", "createdAtUtc": "<ISO_datetime>"}
  ```
- Unity подхватит файл и запишет результат в `test_level_response.json`.

**Команды:**
- `launch_test_level` — запуск тестового уровня с ботом.
- `recompile_scripts` — принудительная перекомпиляция скриптов (обязательно после правок .cs-файлов).
- `regenerate_project_files` — пересоздать .csproj/.sln (вызывает `SyncVS.SyncSolution()` через рефлексию). Нужно после добавления или удаления .cs-файлов, чтобы Visual Studio Solution Explorer отразил актуальную структуру. `invoke_open_unity_test_level.ps1` вызывает это автоматически.

**Результат:**
- `test_level_response.json` с полями: `state` (running/completed/failed/busy), `testResult` (WIN/FAIL/UNKNOWN), `diagnosticLogPath`.

**Важно:**
- Automation bridge ищет маркер `[TEST RESULT]` в `diagnostic_log.txt`.
- Это же основной рабочий лог; разделение делается тегами канала в строке.
- При поиске по `EditorLogs` через файловый поиск учитывать игнорируемые пути (`includeIgnoredFiles=true`).
- Если automation bridge завершает без `[TEST RESULT]` и `diagnostic_log.txt` пустой — читать Unity `Editor.log`: сигнал compile/editor-level сбоя, а не runtime-поведения бота.

---

## 2. Test Level Launcher (из Unity Editor)

**Меню:** `Tools → Test Level → Launch <level_name>` (по одному пункту на каждый тестовый уровень)
**Файл:** `LostCyberHamster/Assets/Editor/TestLevelLauncher.cs`

Запускает указанный тестовый уровень с автовключением бота. PlayerPrefs автоматически очищаются при выходе из Play Mode.

---

## 3. Diagnostic Log (единый файл + каналы)

**Путь:**
- `EditorLogs/diagnostic_log.txt`

**Теги каналов в строке:**
- `[CH=STAB]` — стабильность (fail/win, критические сигналы).
- `[CH=BOT]` — событийный поток бота (pipeline/select/execute/result).
- `[CH=ECO]` — экономические события и итог ранa.

**Файл логгера:** `LostCyberHamster/Assets/Scripts/GameEngine/DebugManager.cs`
**Просмотрщик:** `LostCyberHamster/Assets/Editor/DiagnosticLogViewer.cs`

**Меню:**
- `Tools → Diagnostics → View Diagnostic Log` — открыть лог.
- `Tools → Diagnostics → Clear Diagnostic Log` — очистить.
- `Tools → Diagnostics → Open Log Folder` — открыть папку.

**Как читать по задаче:**
- Для проверки прохождения автопрогона: сначала канал `STAB`.
- Для отладки решений бота: канал `BOT`.
- Для эффективности и сравнений прогонов: канал `ECO`.

---

## 4. Тестовые паттерны и уровни

**Коллекция паттернов:** `LostCyberHamster/Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`

**Тестовые уровни (полный список в `workflow.md`, секция «Тестовые уровни бота»):**

| Уровень | Адрес |
|---|---|
| Threat smallNotAliveRoad + SwitchLane | `01_New_York/Morning/test_threat_small_notalive_road_switchlane` |
| Threat smallNotAliveRoad + Jump | `01_New_York/Morning/test_threat_small_notalive_road_jump` |

**Именование тестовых паттернов:** `test_threat_{obstacle_type}_{action}_{NN}_{description}`.
**Координаты:** `y = -1.8` (top lane), `y = -2.8` (bottom lane).
**Ссылка из level JSON:** `{"ref": "имя_паттерна", "spriteSeed": 0, "overrides": []}`.

---

## 5. Level Tilemap Editor

**Меню:** `Tools → Level Tilemap Editor`
**Файл:** `LostCyberHamster/Assets/Editor/LevelEditor/LevelTilemapEditor.cs`

Визуальный редактор уровней: размещение препятствий на tilemap, поиск/фильтрация паттернов, управление sprite overrides.

---

## 6. Obstacle Animation Tools

**Импорт:** `Tools → Obstacle Animations → Import From Dropbox`
**Превью:** `Tools → Obstacle Animations → Preview Selected Animation`
**Очистка:** `Tools → Obstacle Animations → Clear Preview`

Импорт PNG-последовательностей из Dropbox (`C:\Dropbox\exchange\crystal_wave\sprites\sprites_for_animation`), дедупликация кадров, генерация AnimationClip (5 FPS).

**Именование файлов:** `obstacle_{location}_{category}_{id}_{animType}-{frame}.png`

---

## 7. Asset Management

- `Tools → Build Assets (Editor only / Android / IOS)` — сборка asset bundles.
- `Tools → Levels → Sync Level Assets` — синхронизация Addressables с файловой структурой уровней.

---

## 8. Player Data

- `Tools → Player Data → Export JSON` — экспорт расшифрованных данных игрока в `LostCyberHamster/Temp/PlayerData.json`.
- `Tools → Player Data → Reset` — сброс прогресса к дефолтам.

---

## 9. Утилиты

- `Tools → Batch Rename` — массовое переименование ассетов (replace/prefix/postfix).
- `Tools → Check Missing References in Prefabs` — поиск битых ссылок.
- `Tools → Update prefabs in the scene` — откат prefab instances к шаблону.
- `Tools → Check OnDestroy in Scripts` — поиск утечек подписок без OnDestroy.
- `Tools → Export/Import Tilemap to JSON` — экспорт/импорт tilemap.

---

## 10. PowerShell-скрипты

| Скрипт | Назначение |
|--------|-----------|
| `invoke_open_unity_test_level.ps1` | Полный цикл: рекомпиляция → запуск теста → получение результата |
| `migrate_levels.ps1` | Миграция уровней из copy-paste формата в reference-based |
| `rename_to_snake_case.ps1` | Переименование анимационных файлов в snake_case перед импортом |
| `read_log_channel.ps1` | Унифицированное чтение логов по каналу (STAB/BOT/ECO) с фильтрацией и tail |

### 10.1 Log Channel Reader (для любого агента)

Скрипт `read_log_channel.ps1` задаёт единый способ чтения логов для GitHub Copilot / Codex / Claude Code.

Он работает в двух режимах:
- **tagged**: читает `diagnostic_log.txt` и фильтрует по тегам `[CH=STAB|BOT|ECO]`.

Базовые примеры:

```powershell
./read_log_channel.ps1 -Channel STAB -Tail 120
./read_log_channel.ps1 -Channel BOT -Event "EXECUTE|RESULT" -Tail 200
./read_log_channel.ps1 -Channel ECO -Tail 200
./read_log_channel.ps1 -Channel ALL -SummaryOnly
```

Рекомендация для процесса:
1. Сначала `STAB`.
2. Затем `BOT`.
3. Затем `ECO`.

Это уменьшает шум и стабилизирует анализ логов между разными агентами.
