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

### 3. Проверить возможность размещения декораций на уровне

**Что нужно сделать:**
- Проверить, загружаются ли decoration sprites в Level Tilemap Editor
- Убедиться, что их можно разместить на tilemap
- Проверить сохранение в level JSON
- Проверить корректное отображение при загрузке уровня в игре

**Возможные проблемы:**
- Decoration sprites могут иметь другой label в Addressables
- Может требоваться отдельная логика фильтрации
- Может потребоваться специальный тип `ObstacleTypeEnum.decor`

---

## ✅ DONE (Выполнено)

### 1. Рефакторинг Level Tilemap Editor для работы в текущей сцене
**Дата:** 2025-11-26

**Что сделано:**
- Level Tilemap Editor теперь работает в текущей открытой сцене
- При открытии: скрываются все существующие объекты
- При закрытии: сцена перезагружается без сохранения изменений
- Подход идентичен `ObstacleAnimationPreviewer`

**Изменённые файлы:**
- `Assets/Editor/LevelEditor/SceneCreator.cs`
- `Assets/Editor/LevelEditor/LevelTilemapEditor.cs`

---

### 2. Исправлена регистрация ассетов в Addressables
**Дата:** 2025-11-26

**Проблема:**
Спрайт-шиты и анимации не добавлялись в Addressables из-за неправильных имён групп и label'ов.

**Решение:**
- Добавлены функции `PascalCaseToLowerWithSpaces()` и `PascalCaseToTitleCase()`
- Исправлены имена групп: `"new york obstacles sprites"` и `"New York obstacle animations"`
- Исправлены label'ы для спрайтов и анимаций (используется `locationTitleCase`)
- Спрайт-шиты и анимации теперь корректно регистрируются

---

### 3. Поддержка naming convention с категориями (bigAlive, smallAlive, etc.)
**Дата:** 2025-11-26

**Что сделано:**
- Расширена функция `TryExtractLocation()` для поддержки категорий: bigAlive, smallAlive, bigNotAlive, people, dogs, cats, cars, buses
- Обновлён `ObstacleAnimationPreviewer.GetPrefabPath()` для распознавания новых категорий
- Поддержка обратной совместимости со старым naming convention

**Изменённые файлы:**
- `Assets/Editor/ObstacleAnimationImporter.cs`
- `Assets/Editor/ObstacleAnimationPreviewer.cs`
