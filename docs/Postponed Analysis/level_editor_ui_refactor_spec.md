# Level Tilemap Editor — UI Refactor: Техническое задание

## Обзор

Рефакторинг UI Level Tilemap Editor для чёткого разделения двух режимов работы: **Templates** (редактирование паттернов) и **Level** (сборка уровня из паттернов). Удаление избыточных элементов, добавление новых фич.

---

## Текущая архитектура

### Файлы редактора
- `Assets/Editor/LevelEditor/LevelTilemapEditor.cs` — главное окно
- `Assets/Editor/LevelEditor/LevelTilemapEditor.uxml` — UI layout
- `Assets/Editor/LevelEditor/LevelTilemapEditor.uss` — стили
- `Assets/Editor/LevelEditor/LevelTilemapUi.cs` — UI controller
- `Assets/Editor/LevelEditor/PatternSequencePanel.cs` — двойной список (Available / Level Sequence)
- `Assets/Editor/LevelEditor/SpriteOverridePanel.cs` — override спрайтов
- `Assets/Editor/LevelEditor/SceneCreator.cs` — создание сцены (Grid, Tilemap, фон, дорога)
- `Assets/Editor/LevelEditor/LevelDataManager.cs` — загрузка/сохранение JSON

### Модели данных
- `LevelInfo` — resolved уровень (patterns с конкретными спрайтами)
- `LevelInfoRef` — ссылочный уровень (patternSequence с ref'ами + overrides)
- `PatternsCollection` — единый файл всех шаблонов паттернов (`PatternsCollection.json`)
- `PatternTemplate` — шаблон паттерна (name, description, obstacles с id/type/x/y)
- `LocationTheme` — маппинг типов на доступные спрайты для локации

### Два режима
- **Templates** (`level_design_templates`): Редактирование паттернов-шаблонов через тайлмап. Один файл `PatternsCollection.json`.
- **Level** (New York, Paris...): Сборка уровня из паттернов, декорации, override спрайтов.

### Текущий UI (UXML)
```
location-dropdown
background-dropdown          ← УДАЛИТЬ
create-level-btn
template-level-name          ← только Templates
daypart-radio-group          ← только Level
files-list-view              ← УДАЛИТЬ для Templates
patternDuration              ← оставить
patterns section:
  selected-pattern-name
  selected-pattern-description
  patterns-list-view
  add/remove/duplicate/up/down buttons
obstacle-type-dropdown
IsCollectableOnRoofToggle
sprites scrollview
save-btn / reset-btn
```

PatternSequencePanel и SpriteOverridePanel добавляются программно.

---

## Задача 1: Удалить Background Texture Dropdown

### Обоснование
Фон определяется автоматически по naming convention `bg_{location}_{daypart}`. Рантайм (`LevelDataProvider.LoadBackgroundSpriteWithFallback`) уже имеет fallback на эту конвенцию. Ручной выбор из dropdown избыточен.

### Что делать

1. **UXML**: Удалить `<ui:DropdownField label="background texture" name="background-dropdown" />`

2. **LevelTilemapUi.cs**: Удалить `UpdateBackgroundDropdown()`, ссылку на `_backgroundDropdown`, обработчик

3. **LevelTilemapEditor.cs**: Удалить `HandleBackgroundSelected()`, вызовы `UpdateBackgroundDropdown()`

4. **LevelInfo / LevelInfoRef**: Поле `backgroundTexture` **удалить** из обеих моделей. Аналогично поля `skyTexture`, `background2Texture`, `roadTexture` — они все определяются naming convention

5. **SceneCreator.cs**: Вместо `currentLevelInfo.backgroundTexture` — принимать параметры `locationName` и `daypart`, строить ключ `bg_{locationSlug}_{daypart}`. Аналогично для road: `rd_{locationSlug}_{daypart}`

6. **LevelDataProvider.cs** (рантайм): Убрать загрузку `backgroundTexture` из JSON, всегда строить ключ по naming convention. Оставить warning если спрайт не найден

7. **LevelResolver.cs**: Убрать копирование `skyTexture`, `backgroundTexture`, `background2Texture`, `roadTexture` из `LevelInfoRef` в `LevelInfo`

8. **ResolveTemplatesForDisplay()**: Убрать хардкод `backgroundTexture = "bg_new_york_morning"` — теперь ключ строится динамически

### Naming Convention для фонов
```
bg_{location_slug}_{daypart}      → bg_new_york_morning
bg_2_{location_slug}_{daypart}    → bg_2_new_york_morning  
rd_{location_slug}_{daypart}      → rd_new_york_morning
sky_{location_slug}_{daypart}     → sky_new_york_morning
```

### Миграция существующих JSON
- Написать Editor-скрипт или PowerShell для удаления полей `backgroundTexture`, `skyTexture`, `background2Texture`, `roadTexture` из всех существующих JSON-файлов уровней
- Убедиться, что все существующие спрайты в Addressables соответствуют naming convention

### Проверка
- Локальная проверка: построение ключа фона по location + daypart.
- Проверка: загрузка каждой комбинации location + daypart даёт валидный спрайт.

---

## Задача 2: Удалить Files List для Templates

### Обоснование
В режиме Templates используется единственный файл `PatternsCollection.json`. File list здесь не нужен, поскольку файл всегда один.

### Что делать

1. **LevelTilemapEditor.cs**: В Templates mode — автоматически загружать `PatternsCollection.json` при выборе локации `level_design_templates`. Не показывать file list.

2. **LevelTilemapUi.cs**: Скрывать `files-list-view` в Templates mode (`DisplayStyle.None`). Показывать только в Level mode.

3. **HandleLocationChanged()**: При выборе `level_design_templates` — сразу вызывать загрузку `PatternsCollection`, без ожидания выбора файла из списка.

---

## Задача 3: Убрать Up/Down из Templates

### Обоснование
В Templates mode паттерны — это master-список шаблонов, их порядок не влияет на геймплей. Порядок важен только в Level Sequence конкретного уровня.

### Что делать

1. **LevelTilemapEditor.cs**: Скрывать `move-up-btn` и `move-down-btn` в Templates mode

2. **UXML**: Кнопки остаются в разметке, но скрываются программно (`DisplayStyle.None`)

---

## Задача 4: Поле поиска в Patterns (Templates mode)

### Что делать

1. **UXML**: Добавить `<ui:TextField label="Search" name="pattern-search-field" />` после `selected-pattern-description`

2. **LevelTilemapUi.cs**: Реализовать фильтрацию patterns-list-view по подстроке (case-insensitive)

3. **Логика**: 
   - При вводе текста — фильтровать список паттернов
   - Пустое поле — показывать все
   - Если текущий выбранный паттерн проходит фильтр — сохранять выбор
   - Если не проходит — выбирать первый из отфильтрованных

---

## Задача 5: Разделение UI по режимам

### Принцип
UI редактора радикально различается в зависимости от режима. Элементы, не нужные в текущем режиме, **скрываются** (не удаляются из DOM).

### Templates mode — отображаемые элементы
| Элемент | Описание |
|---------|----------|
| `location-dropdown` | Выбор локации |
| `template-level-name` | Имя шаблона (если есть) |
| `patternDuration` | Длительность паттерна |
| **Patterns section** | Основной рабочий инструмент |
| ├ `selected-pattern-name` | Имя выбранного паттерна |
| ├ `selected-pattern-description` | Описание |
| ├ `pattern-search-field` | Поиск (новый) |
| ├ `patterns-list-view` | Список паттернов |
| └ `add-pattern-btn`, `remove-pattern-btn`, `duplicate-pattern-btn` | Управление (без Up/Down) |
| `obstacle-type-dropdown` | Тип препятствия |
| `IsCollectableOnRoofToggle` | Флаг collectible on roof |
| `sprites` scrollview | Спрайты для размещения на тайлмапе |
| `save-btn`, `reset-btn` | Сохранение/сброс |

### Templates mode — скрытые элементы
- `background-dropdown` (удалён)
- `daypart-radio-group`
- `files-list-view`
- PatternSequencePanel
- SpriteOverridePanel
- `move-up-btn`, `move-down-btn`

### Level mode — отображаемые элементы
| Элемент | Описание |
|---------|----------|
| `location-dropdown` | Выбор локации |
| `daypart-radio-group` | Время суток |
| `files-list-view` | Список уровней (level_01, level_02...) |
| **Level Builder section** | Единый блок управления уровнем |
| ├ Заголовок «Level Builder» | Визуальное разделение |
| ├ Available Patterns (лево) | Список доступных паттернов с фильтром |
| ├ Level Sequence (право) | Упорядоченный список с reorderable drag-and-drop |
| ├ `+ Add` кнопка | Добавить паттерн из Available в Sequence |
| ├ `Remove` кнопка | Удалить из Sequence |
| ├ Seed + Randomize | Для выбранного паттерна в Sequence |
| SpriteOverridePanel | При выделении препятствия в сцене |
| Decoration sprites | Спрайты декораций для размещения |
| `save-btn`, `reset-btn` | Сохранение/сброс |

### Level mode — скрытые элементы
- `background-dropdown` (удалён)
- `template-level-name`
- Patterns section (name, description, search, patterns list, add/remove/duplicate)
- `obstacle-type-dropdown`
- `IsCollectableOnRoofToggle`
- Obstacle sprites scrollview (в Level mode размещаются только декорации)

### Реализация
Метод `ApplyModeUI(bool isTemplateMode)` в `LevelTilemapUi.cs`:
```csharp
public void ApplyModeUI(bool isTemplateMode)
{
    // Templates-only elements
    SetVisible(_templateLevelNameParent, isTemplateMode);
    SetVisible(_patternsSection, isTemplateMode);
    SetVisible(_obstacleTypeDropdown, isTemplateMode);
    SetVisible(_isCollectableOnRoofToggle, isTemplateMode);
    SetVisible(_obstacleSpritesSection, isTemplateMode);
    
    // Level-only elements
    SetVisible(_daypartRadioGroup, !isTemplateMode);
    SetVisible(_filesListSection, !isTemplateMode);
    // PatternSequencePanel управляет видимостью сам (Show/Hide)
}
```

---

## Задача 6: Reorderable Level Sequence (drag-and-drop)

### Обоснование
Вместо кнопок Up/Down — встроенный drag-and-drop для реорганизации паттернов в Level Sequence.

### Реализация

1. **PatternSequencePanel.cs**: Установить `_sequenceList.reorderable = true` (встроенная фича UI Toolkit ListView)

2. Обработать `_sequenceList.itemsRemoved` и `_sequenceList.itemsAdded` (или `itemIndexChanged`) для синхронизации данных после перетаскивания

3. После reorder — вызывать `OnSequenceChanged?.Invoke()` для пересчёта уровня через `LevelResolver.Resolve()`

4. Удалить кнопки `Up` и `Down` из PatternSequencePanel (не путать с кнопками в Patterns section, которые скрываются в Templates mode)

5. **Альтернатива для добавления**: Двойной клик по паттерну в Available list → добавление в Sequence. Кнопка `+ Add` остаётся как основной способ.

---

## Задача 7: Последовательная отрисовка всех паттернов уровня

### Текущее поведение
Показывается один паттерн за раз. Переключение через patterns-list-view → `AddTilesToTilemap()` → clear + redraw.

### Новое поведение (Level mode)
Все паттерны из Level Sequence отрисовываются последовательно на тайлмапе **слева направо** (как в игре: скролл идёт справа налево, хомяк бежит слева направо).

### Формула позиционирования
```
singlePatternWidth = patternDurationMinutes * 60 * 3.8  (world units)

Pattern[0]: offset = 0
Pattern[1]: offset = singlePatternWidth
Pattern[2]: offset = 2 * singlePatternWidth
...
Pattern[N]: offset = N * singlePatternWidth
```

Каждый obstacle в паттерне получает X-координату: `obstacle.x + patternOffset`

### Реализация
Новый метод `RenderAllPatternsToTilemap()` в `LevelTilemapEditor.cs`:
```csharp
private void RenderAllPatternsToTilemap()
{
    _isTilemapBulkOperation = true;
    _tipeMapInScene.ClearAllTiles();
    
    float singlePatternWidth = _patternDurationMinutes * 60f * 3.8f;
    var positions = new List<Vector3Int>();
    var tiles = new List<TileBase>();
    
    for (int p = 0; p < _currentLevelInfo.patterns.Count; p++)
    {
        var pattern = _currentLevelInfo.patterns[p];
        float patternOffset = p * singlePatternWidth;
        
        foreach (var obstacle in pattern.obstacles)
        {
            var sprite = SpriteLoader.LoadSpriteSync(obstacle.spriteName);
            if (sprite == null) continue;
            
            var tile = CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.name = obstacle.spriteName;
            
            var worldPos = new Vector3(obstacle.x + patternOffset, obstacle.y, 0f);
            positions.Add(_tipeMapInScene.WorldToCell(worldPos));
            tiles.Add(tile);
        }
    }
    
    _tipeMapInScene.SetTiles(positions.ToArray(), tiles.ToArray());
    
    // Decorations on top
    LoadDecorationsToTilemap();
    
    EditorUtility.SetDirty(_tipeMapInScene.gameObject);
    _isTilemapBulkOperation = false;
}
```

### Когда вызывать
- При загрузке уровня в Level mode (вместо `AddTilesToTilemap()`)
- При изменении Level Sequence (add/remove/reorder)
- При изменении override'а
- При изменении seed

### Визуальные разделители между паттернами
Использовать `Handles.DrawLine` в `OnSceneGUI` или отдельные GameObjects-маркеры для отрисовки вертикальных линий на границах паттернов. Каждая линия на X = `patternIndex * singlePatternWidth`.

### Templates mode
Без изменений — показывается один паттерн за раз (как сейчас).

---

## Задача 8: Zoom к выбранному паттерну (Level mode)

### Фича
При клике на паттерн в Level Sequence — SceneView зумится к области этого паттерна.

### Реализация
```csharp
private void ZoomToPattern(int patternIndex)
{
    float singlePatternWidth = _patternDurationMinutes * 60f * 3.8f;
    float xCenter = patternIndex * singlePatternWidth + singlePatternWidth / 2f;
    
    // Bounds covering the pattern area
    var bounds = new Bounds(
        new Vector3(xCenter, 0f, 0f),
        new Vector3(singlePatternWidth, 10f, 0f)  // Y=10 — достаточно для видимости всех Y-позиций
    );
    
    var sceneView = SceneView.lastActiveSceneView;
    if (sceneView != null)
    {
        EditorApplication.delayCall += () =>
        {
            sceneView.Frame(bounds, false);
            sceneView.Repaint();
        };
    }
}
```

### Связка с Level Sequence
При `_sequenceList.selectionChanged` → вызывать `ZoomToPattern(selectedIndex)`.

---

## Задача 9: Override в контексте полной отрисовки

### Проблема
Сейчас `SpriteOverridePanel.Show()` принимает `patternIndex`. При полной отрисовке нужно определить, к какому паттерну относится кликнутый obstacle.

### Решение
Хранить маппинг `cellPosition → (patternIndex, obstacleSlotId)` при рендере:

```csharp
// Заполняется при RenderAllPatternsToTilemap()
private Dictionary<Vector3Int, (int patternIndex, int obstacleId)> _cellToPatternMap = new();
```

При клике на тайл в сцене — lookup по cell position, получить `patternIndex` и `obstacleId`, показать `SpriteOverridePanel`.

При применении override — обновить `_currentLevelRef.patternSequence[patternIndex].overrides`, re-resolve через `LevelResolver`, перерисовать.

---

## Seed / Randomize — пояснение механизма

### Как работает Seed
Seed — это число, определяющее **детерминированную рандомизацию спрайтов** для каждого паттерна в Level Sequence.

**Приоритет выбора спрайта:**
1. **Manual override** — если задан `SpriteOverride` для конкретного `obstacleId` → используется override
2. **Seed-based random** — если `spriteSeed != 0` и для типа препятствия доступно >1 спрайта в `LocationTheme` → `new Random(spriteSeed + obstacleId).Next(sprites.Count)` — выбирает случайный спрайт
3. **Theme default** — если seed = 0 → используется `@default` спрайт из маппинга
4. **First in list** — fallback на первый спрайт
5. **Universal name** — для collectables (coin, crystal, pizza)

**Зачем:** Один и тот же паттерн `easy_run` может появляться в уровне несколько раз. Seed позволяет каждому вхождению выглядеть по-разному (разные персонажи, разные машины), сохраняя **детерминированность** (одинаковый seed = одинаковый результат).

**Пример:**
```json
{ "ref": "easy_run", "spriteSeed": 0, "overrides": [] }    → все default спрайты
{ "ref": "easy_run", "spriteSeed": 42, "overrides": [] }   → случайные спрайты (seed=42)
{ "ref": "easy_run", "spriteSeed": 999, "overrides": [] }  → другие случайные (seed=999)
```

**Кнопка Randomize**: Генерирует `Random.Range(1, int.MaxValue)` и записывает в `spriteSeed`.

---

## Порядок реализации

### Фаза 1 — Удаление избыточного (низкая сложность)
1. Задача 1: Удалить Background Texture Dropdown + поля из моделей
2. Задача 2: Удалить Files List для Templates
3. Задача 3: Убрать Up/Down из Templates

### Фаза 2 — UI рефакторинг (средняя сложность)
4. Задача 5: Разделение UI по режимам (ApplyModeUI)
5. Задача 4: Поле поиска в Patterns
6. Задача 6: Reorderable Level Sequence

### Фаза 3 — Scene rendering (высокая сложность)
7. Задача 7: Последовательная отрисовка всех паттернов
8. Задача 8: Zoom к выбранному паттерну
9. Задача 9: Override в контексте полной отрисовки

---

## Затрагиваемые файлы

| Файл | Задачи |
|------|--------|
| `LevelTilemapEditor.uxml` | 1, 3, 4 |
| `LevelTilemapEditor.cs` | 1, 2, 3, 5, 7, 8, 9 |
| `LevelTilemapUi.cs` | 1, 2, 3, 4, 5 |
| `PatternSequencePanel.cs` | 6, 8 |
| `SpriteOverridePanel.cs` | 9 |
| `SceneCreator.cs` | 1 |
| `LevelInfo.cs` | 1 |
| `LevelInfoRef.cs` | 1 |
| `LevelResolver.cs` | 1 |
| `LevelDataProvider.cs` | 1 |
| `LocationAssetFallback.cs` | 1 (возможно) |

---

## Риски и edge cases

1. **Миграция JSON**: Существующие level JSON содержат `backgroundTexture`. Нужен миграционный скрипт.
2. **SceneCreator без backgroundTexture**: Метод `CreateSceneWithTilemap` должен принимать location + daypart вместо LevelInfo.
3. **Templates mode background**: Для превью в Templates используется `bg_new_york_morning` (хардкод). После удаления поля — нужно передавать fallback-значение.
4. **Reorderable ListView**: Убедиться, что `itemIndexChanged` event корректно синхронизирует `patternSequence`.
5. **Полная отрисовка**: Большие уровни (10+ паттернов) могут создать огромный тайлмап. Возможно, потребуется ленивая загрузка или viewport clipping.
6. **Cell-to-pattern mapping**: При overlap'е паттернов (если obstacle.x + offset попадает в ту же cell) — нужна коллизия.
