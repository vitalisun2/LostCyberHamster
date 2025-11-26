# Obstacle Animations - TODO List

## Проблемы, требующие решения

### 1. ✅ Новые спрайт-шиты не видны в Level Tilemap Editor
**Статус:** ИСПРАВЛЕНО

**Проблема:**
После импорта анимаций через `ObstacleAnimationImporter` созданные спрайт-шиты не отображались в списке спрайтов Level Tilemap Editor.

**Причина:**
Несоответствие label'ов в Addressables:
- Устанавливался label: `"NewYork obstacles sprites"` (PascalCase, слитно)
- Editor искал label: `"New York obstacles sprites"` (Title Case, с пробелами)

**Решение:**
- Исправлены строки 446 и 472 в `ObstacleAnimationImporter.cs`
- Теперь используется `locationTitleCase` вместо `locationPascal` для label'ов
- Label'ы теперь правильные: `"New York obstacles sprites"` и `"New York obstacle animations"`

**Требуется:**
- Переимпортировать анимации, чтобы обновить label'ы в Addressables
- Проверить, что спрайты теперь видны в Level Tilemap Editor

---

## Текущий TODO List

### 3. Проверить возможность размещения декораций на уровне
**Статус:** Требует проверки

**Описание:**
Необходимо убедиться, что в Level Tilemap Editor можно размещать decoration sprites (декоративные спрайты) на уровне.

**Что проверить:**
- Загружаются ли decoration sprites в список доступных спрайтов редактора
- Можно ли их разместить на tilemap
- Правильно ли они сохраняются в level JSON
- Корректно ли они отображаются при загрузке уровня в игре

**Возможные проблемы:**
- Decoration sprites могут иметь другой label в Addressables
- Возможно, требуется отдельная логика фильтрации для декораций
- Может потребоваться специальный тип препятствия (`ObstacleTypeEnum.decor`)

---

## Новые задачи

### 2. ✅ Рефакторинг Level Tilemap Editor для работы в текущей сцене
**Статус:** ВЫПОЛНЕНО

**Проблема:**
Level Tilemap Editor создавал новую сцену и выгружал текущую, что было неудобно.

**Решение:**
Реализован подход, аналогичный `ObstacleAnimationPreviewer`:
- `OnEnable()`: сохраняет путь текущей сцены, скрывает все root объекты
- `CreateSceneWithTilemap()`: работает в текущей активной сцене (`SceneManager.GetActiveScene()`)
- `OnDisable()`: перезагружает исходную сцену без сохранения (`EditorSceneManager.OpenScene`)
- Удалена статическая переменная `_tilemapScene` и методы `FindOrCreateScene()`, `ClearScene()`

**Изменённые файлы:**
- `Assets/Editor/LevelEditor/SceneCreator.cs` — упрощена логика, удалено создание/выгрузка сцены
- `Assets/Editor/LevelEditor/LevelTilemapEditor.cs` — добавлено управление видимостью объектов и восстановление сцены

**Требуется:**
- Протестировать открытие/закрытие редактора в различных сценах

---

## Выполненные задачи

### ✅ Исправлена регистрация ассетов в Addressables
- Добавлены функции `PascalCaseToLowerWithSpaces()` и `PascalCaseToTitleCase()`
- Исправлены имена групп: `"new york obstacles sprites"` и `"New York obstacle animations"`
- Спрайт-шиты и анимации теперь корректно добавляются в Addressables
