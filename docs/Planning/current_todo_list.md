# 📝 Current TODO List

---

## 🔴 TO DO (Требует выполнения)

### Проверить возможность размещения декораций на уровне

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

### Рефакторинг Level Tilemap Editor для работы в текущей сцене
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

### Исправлена регистрация ассетов в Addressables
**Дата:** 2025-11-26

**Проблема:**
Спрайт-шиты и анимации не добавлялись в Addressables из-за неправильных имён групп и label'ов.

**Решение:**
- Добавлены функции `PascalCaseToLowerWithSpaces()` и `PascalCaseToTitleCase()`
- Исправлены имена групп: `"new york obstacles sprites"` и `"New York obstacle animations"`
- Исправлены label'ы для спрайтов и анимаций (используется `locationTitleCase`)
- Спрайт-шиты и анимации теперь корректно регистрируются

---

### Поддержка naming convention с категориями (bigAlive, smallAlive, etc.)
**Дата:** 2025-11-26

**Что сделано:**
- Расширена функция `TryExtractLocation()` для поддержки категорий: bigAlive, smallAlive, bigNotAlive, people, dogs, cats, cars, buses
- Обновлён `ObstacleAnimationPreviewer.GetPrefabPath()` для распознавания новых категорий
- Поддержка обратной совместимости со старым naming convention

**Изменённые файлы:**
- `Assets/Editor/ObstacleAnimationImporter.cs`
- `Assets/Editor/ObstacleAnimationPreviewer.cs`
