# 📝 Current TODO List

---

## 🔴 TO DO (Требует выполнения)

### 1. Обновить spriteName в level JSON файлах на новые имена с bigAlive

**Что нужно сделать:**
Заменить старые имена спрайтов (granny, hipster, etc.) на новые имена с анимациями (bigAlive) в JSON файлах уровней (`Assets/Resources/levels/NewYork/...`).

**Логика замены:**
- Сканировать все level JSON файлы (например, `levels_morning_level_001.json`)
- Для каждого obstacle с `type: 1` (bigAlive):
  - Первое уникальное имя (например, `obstacle_new_york_granny`) → `obstacle_new_york_bigAlive_1_idle`
  - Второе уникальное имя (например, `obstacle_new_york_hipster`) → `obstacle_new_york_bigAlive_2_idle`
  - Если встречается повторно `granny` → использовать то же имя `bigAlive_1_idle`
  - Инкрементировать счётчик для каждого нового уникального имени

**Примеры замены:**
```
obstacle_new_york_granny → obstacle_new_york_bigAlive_1_idle
obstacle_new_york_hipster → obstacle_new_york_bigAlive_2_idle
obstacle_new_york_cool_guy → obstacle_new_york_bigAlive_3_idle
obstacle_new_york_granny (повторно) → obstacle_new_york_bigAlive_1_idle
```

**Файлы для обработки:**
- `Assets/Resources/levels/NewYork/levels_morning_level_*.json`
- `Assets/Resources/levels/NewYork/levels_day_level_*.json`
- `Assets/Resources/levels/NewYork/levels_evening_level_*.json`
- И т.д.

---

### 2. Реализовать режимы редактирования в Level Tilemap Editor

**Проблема:**
Сейчас в Level Tilemap Editor можно редактировать препятствия одинаково во всех локациях. Нужно разграничить возможности редактирования в зависимости от выбранной локации.

**Требуемое поведение:**

**Режим 1: Level Design Templates (Templates локация)**
- Полное редактирование препятствий: добавление, удаление, перемещение, замена
- Это режим для создания паттернов

**Режим 2: Конкретные уровни (New York, Paris, etc.)**
- **Запрещено:** добавлять/удалять/перемещать обычные препятствия
- **Разрешено:**
  - Размещение декораций (добавление/удаление/перемещение `decor` спрайтов)
  - Замена bigAlive препятствий (только замена спрайта, без изменения позиции)

**Что нужно реализовать:**
1. Определять текущий режим по выбранной локации
2. В режиме Templates: оставить всё как есть (полное редактирование)
3. В режиме конкретных уровней:
   - Блокировать добавление/удаление/перемещение обычных препятствий
   - Разрешить размещение только decoration спрайтов
   - При клике на bigAlive препятствие: открывать dropdown для выбора другого bigAlive спрайта (замена)
   - Сохранять изменения декораций и замен в level JSON

**Файлы для изменения:**
- `Assets/Editor/LevelEditor/LevelTilemapEditor.cs` — добавить проверку режима редактирования
- `Assets/Editor/LevelEditor/LevelTilemapUi.cs` — фильтровать доступные спрайты по режиму

---

### 3. Добавить decoration спрайты в папку New York

**Что нужно сделать:**
- Добавить несколько decoration файлов (PNG) в папку `Assets/Content/locations/01_New_York/sprites/`
- Убедиться, что они помечены как Addressable
- Проверить, что установлен правильный label для загрузки в редакторе

**Примечание:**
Это нужно для тестирования отображения декораций в Level Tilemap Editor.

---

### 4. Выяснить, почему decoration спрайты не отображаются в Level Tilemap Editor

**Проблема:**
В проекте уже есть несколько decoration спрайтов, но они не появляются в списке спрайтов Level Tilemap Editor.

**Что проверить:**
1. Как работает фильтрация спрайтов в `LevelTilemapUi.cs` (метод `IsObstacleSprite()`)
   - Проверяет ли он префикс `"decor"`?
2. Какой label используется для загрузки спрайтов из Addressables
   - Ищется ли отдельный label для decoration или тот же, что и для obstacles?
3. Загружаются ли decoration спрайты через `SpriteLoader.LoadSpritesByTag()`
4. Есть ли в Addressables группа/label для decoration спрайтов

**Файлы для анализа:**
- `Assets/Editor/LevelEditor/LevelTilemapUi.cs` — метод `IsObstacleSprite()`, `BuildObstacleLabel()`
- `Assets/Editor/LevelEditor/SpriteLoader.cs` — логика загрузки по tag/label
- Addressables groups — проверить наличие decoration label


