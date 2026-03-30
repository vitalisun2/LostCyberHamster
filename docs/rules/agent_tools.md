# Agent Tools — проектные инструменты

Каталог инструментов и автоматизаций, доступных в проекте LostCyberHamster. Прочитай перед началом работы, чтобы не тратить итерации на поиск и изобретение того, что уже есть.

---

## 1. Automation Bridge (запуск тестов из-за пределов Unity)

Файловый IPC-механизм для управления Unity Editor извне.

**Файлы:**
- Реализация: `LostCyberHamster/Assets/Editor/TestLevelAutomationBridge.cs`
- PowerShell-обёртка: `invoke_open_unity_test_level.ps1`

**Как вызвать:**

Вариант A — PowerShell-скрипт (рекомендуется):
```powershell
.\invoke_open_unity_test_level.ps1 -TimeoutSeconds 120 -PollMilliseconds 250
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

---

## 2. Test Level Launcher (из Unity Editor)

**Меню:** `Tools → Test Level → Launch`
**Файл:** `LostCyberHamster/Assets/Editor/TestLevelLauncher.cs`

Запускает тестовый уровень `01_New_York/Morning/test_level` с автовключением бота. PlayerPrefs автоматически очищаются при выходе из Play Mode.

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
**Тестовый уровень:** `LostCyberHamster/Assets/Content/locations/01_New_York/levels/Morning/test_level/test_level.json`

**Структура паттерна:**
```json
{
  "name": "bot_s4_roof_then_small_threat",
  "description": "...",
  "nextObstacleId": 7,
  "obstacles": [
    {"id": 0, "type": 2, "x": 22.2, "y": -2.8}
  ]
}
```

**Координаты:** `y = -1.8` (top lane), `y = -2.8` (bottom lane).
**Именование stage-паттернов:** `bot_s{N}_{описание}`.
**Ссылка из test_level.json:** `{"ref": "имя_паттерна", "spriteSeed": 0, "overrides": []}`.

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
