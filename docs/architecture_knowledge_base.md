# Architecture Knowledge Base

## 📋 Оглавление
- [Naming Conventions](#naming-conventions)
- [Addressables System](#addressables-system)
- [Obstacle Animation System](#obstacle-animation-system)
- [Level Tilemap Editor](#level-tilemap-editor)
- [Data Flow](#data-flow)
- [Important Constants](#important-constants)
- [Lessons Learned](#lessons-learned)

---

## Naming Conventions

### Универсальный стандарт: snake_case
**Везде используется snake_case** для идентификаторов, путей, ключей.

**Формат имён препятствий:**
```
obstacle_{location}_{category}_{id}_{animType}
```

**Примеры:**
- `obstacle_new_york_big_alive_1_idle`
- `obstacle_new_york_big_alive_7_idle`
- `obstacle_new_york_manhole`
- `obstacle_new_york_car`

**Фреймы анимаций:**
```
obstacle_new_york_big_alive_1_idle-1.png
obstacle_new_york_big_alive_1_idle-2.png
...
obstacle_new_york_big_alive_1_idle-18.png
```

**Категории препятствий:**
- `big_alive` — стоящие персонажи (100×212px)
- `small_alive` — движущиеся персонажи, собаки (152×108px)
- `big_not_alive` — машины, автобусы (452×172px)
- `small_not_alive_road` — мелкие препятствия на дороге (140×108px)

**Типы анимаций:**
- `_idle` — статичные/циклические анимации
- `_walk` — анимации с движением (пока не используются)

### Почему snake_case?
1. **Консистентность** с существующими паттернами (`new_york`, `level_design_templates`)
2. **Предсказуемый парсинг** через `split('_')`
3. **Кроссплатформенность** (нет проблем с регистром на разных ОС)
4. **Расширяемость** для будущих локаций (Paris, London, Moscow, Berlin)

---

## Addressables System

### Основные принципы
- **Case-sensitive lookup** — ключи чувствительны к регистру
- **NO transformations** — никаких `.ToLower()`, `.ToUpper()`, `.Replace()` при загрузке
- **Exact match** — ключ в JSON должен точно совпадать с адресом в Addressables

### Структура групп
```
New York obstacle animations
├─ obstacle_new_york_big_alive_1_idle (AnimationClip)
├─ obstacle_new_york_big_alive_2_idle (AnimationClip)
└─ ...

new york obstacles sprites
├─ obstacle_new_york_big_alive_1_idle (Sprite)
├─ obstacle_new_york_big_alive_2_idle (Sprite)
└─ ...
```

### Labels (метки)
- **Title Case** для локаций: `"New York"`, `"Paris"`, `"London"`
- Используются для фильтрации ассетов по локации

### SpriteLoader.cs
```csharp
// ✅ ПРАВИЛЬНО — используем точное имя
var sprite = Addressables.LoadAssetAsync<Sprite>(spriteName);

// ❌ НЕПРАВИЛЬНО — не трансформируем
var sprite = Addressables.LoadAssetAsync<Sprite>(spriteName.ToLower());
```

**Кеширование:**
- `_spriteCache` — Dictionary для переиспользования загруженных спрайтов
- `ReleaseSpritesAndClearCache()` — очистка при смене уровня/локации

---

## Obstacle Animation System

### Workflow художника
1. Создать анимацию в **Procreate** с правильными размерами
2. Экспортировать **PNG sequence** в Dropbox
3. В Unity: **Tools → Obstacle Animations → Import From Dropbox**
4. Выбрать папку с PNG-последовательностью
5. Preview: **ПКМ на спрайт-шите → Obstacle Animations → Preview Selected Animation**

### ObstacleAnimationImporter.cs
**Что делает:**
1. Сканирует PNG-последовательности в Dropbox
2. Валидирует размеры (должны делиться на 4)
3. Создаёт **sprite sheets** (объединённые текстуры)
4. Генерирует **AnimationClips** с правильным framerate
5. Регистрирует в **Addressables** с точными адресами

**Важные методы:**
- `TryExtractLocation()` — определяет локацию из имени файла
- `ValidateAndResizeSpriteSize()` — проверяет размеры, делает smart resize
- `RegisterInAddressables()` — добавляет в группы Addressables

**Smart Resize правила:**
- Только **downscale** (запрет upscaling)
- Сохранение **aspect ratio**
- Чистые **integer ratios** (2x, 4x, 8x)
- Результат **делится на 4** (ETC2 компрессия)

### ObstacleAnimationPreviewer.cs
**Что делает:**
1. Инстанцирует подходящий префаб (BigCitizenPrefab, SmallCitizenPrefab)
2. Загружает AnimationClip из Addressables
3. Использует **AnimationMode API** для preview в Editor
4. Автоматически центрирует камеру на объекте

**Структура префабов:**
```
BigCitizenPrefab (root)
└─ BigCitizenSprite (child)
   ├─ Animator
   └─ SpriteRenderer
```

**Важно:** Pivot = Bottom Center

---

## Level Tilemap Editor

### Архитектура
- Работает с **активной сценой** (не создаёт отдельные сцены)
- Использует **Tilemap** для визуализации препятствий
- Паттерны хранятся в JSON-файлах
- **Два режима работы:** Templates и Locations

### Режимы работы редактора

#### **Templates режим** (`level_design_templates`)
- ✅ Показывает **obstacles** (patterns) на Tilemap
- ✅ **Можно редактировать** obstacles (add, delete, move)
- ❌ **Decorations запрещены** — будет предупреждение
- 💾 При сохранении обновляются **patterns** с obstacles

#### **Locations режим** (New York, Paris, etc.)
- ✅ Показывает **obstacles (read-only)** — видны на Tilemap, но нельзя изменять
- ✅ **Можно редактировать decorations** (add, delete, move)
- ✅ Decorations размещаются **поверх obstacles**
- ❌ Попытка редактировать obstacles → предупреждение "Obstacles are read-only"
- 💾 При сохранении обновляются **decorationPatterns**

### Workflow редактора
1. Выбрать **Location** (01_New_York, level_design_templates)
2. Выбрать **Daypart** (Morning, Day, Evening, Night) — если не templates
3. Выбрать **Level file** (level_01.json, level_02.json)
4. **Templates:** Выбрать Pattern из списка, редактировать obstacles
5. **Locations:** Видеть obstacles (read-only), добавлять decorations поверх

### Важные компоненты
- **LevelTilemapEditor.cs** — главное окно редактора
- **SceneCreator.cs** — создание сцены с тайлмапом и фоном
- **LevelDataManager.cs** — загрузка/сохранение JSON

### SceneCreator.CleanupOldSceneObjects()
**КРИТИЧЕСКИ ВАЖНО:** При смене локации/уровня удаляет старые объекты:
- Все `Grid` (содержат Tilemap)
- Все `BackgroundSegment_*`

**Почему это нужно:**
Редактор больше не создаёт отдельные сцены, работает с активной сценой. Без очистки объекты накапливаются и наслаиваются друг на друга.

### Decoration System

**Naming Convention:**
```
decor_bush_1
decor_tree_1
decor_{type}_{id}
```

**Addressables Structure:**
```
decor sprites
├─ decor_bush_1 (Sprite)
├─ decor_tree_1 (Sprite)
└─ ...

Label: "New York decor sprites"
```

**LevelInfo.json Structure:**
```json
{
  "decorationPatterns": [
    {
      "decorationTiles": [
        { "name": "decor_bush_1", "xPos": 10, "yPos": 2 },
        { "name": "decor_tree_1", "xPos": 15, "yPos": 3 }
      ]
    }
  ],
  "patterns": [ ... ]
}
```

**Важно:**
- Decorations НЕ очищают Tilemap при загрузке — добавляются поверх obstacles
- `ProcessTileChange()` блокирует редактирование obstacles в Locations режиме
- `UpdateCurrentLevelInfoFromTilemap()` пропускает обновление patterns для Locations

---

## Data Flow

### Создание препятствия в игре
```
1. JSON (level_01.json)
   ↓ spriteName: "obstacle_new_york_big_alive_1_idle"
   ↓ type: 1 (ObstacleTypeEnum)

2. ObstacleSpriteTypeMappingsManager
   ↓ LoadBindings("01_New_York")
   ↓ Читает obstacle_sprite_to_type_mappings.json

3. ObstacleFactory
   ↓ CreateObstacle(spriteName, type, position)
   ↓ Определяет префаб по категории

4. Addressables
   ↓ LoadAssetAsync<AnimationClip>(spriteName)
   ↓ LoadAssetAsync<Sprite>(spriteName)

5. Инстанцирование
   ↓ PrefabUtility.InstantiatePrefab()
   ↓ animator.runtimeAnimatorController = animatorController
   ↓ Применение AnimationClip
```

### Универсальный подход (Type 1 → big_alive)
**Старый подход (специфичные персонажи):**
```json
{
  "spriteName": "obstacle_new_york_granny",
  "type": 1
}
```

**Новый подход (универсальные категории):**
```json
{
  "spriteName": "obstacle_new_york_big_alive_3_idle",
  "type": 1
}
```

**Преимущества:**
- Шаблоны паттернов работают для **всех локаций**
- Легко добавлять новые локации (Paris, London, Moscow)
- Художник создаёт набор `big_alive_X_idle` для каждой локации
- Геймдизайнер использует одни и те же паттерны

---

## Important Constants

### Consts.cs

**Размеры спрайтов:**
```csharp
SMALL_ALIVE = new Vector2(152, 108);        // Люди, собаки
BIG_ALIVE = new Vector2(100, 212);          // Стоящие персонажи
BIG_NOTALIVE = new Vector2(452, 172);       // Машины, автобусы
SMALL_NOTALIVE = new Vector2(140, 108);     // Мелкие препятствия
```

**ВАЖНО:** Все размеры должны делиться на 4 (требование ETC2 компрессии)

**Пути:**
```csharp
LocationsPath = "Assets/Content/locations";
TemplatesLocationName = "level_design_templates";
```

**Игровые константы:**
```csharp
BackgroundYPos = 0f;                        // Y-позиция фона
ScrollSpeed = 3.8f;                         // Скорость движения при ScrollSpeed=1
```

---

## Lessons Learned

### ❌ НИКОГДА не делать

**1. НЕ использовать .ToLower() для Addressables**
```csharp
// ❌ НЕПРАВИЛЬНО
var sprite = Addressables.LoadAssetAsync<Sprite>(spriteName.ToLower());

// ✅ ПРАВИЛЬНО
var sprite = Addressables.LoadAssetAsync<Sprite>(spriteName);
```
**Почему:** Addressables case-sensitive, трансформации ломают lookup.

**2. НЕ забывать очищать сцену при смене локации**
```csharp
// ✅ В SceneCreator.CreateSceneWithTilemap()
CleanupOldSceneObjects(scene); // Удаляем Grid, Background перед созданием новых
```
**Почему:** Редактор работает с активной сценой, объекты накапливаются.

**3. НЕ использовать animator.Play() + animator.Update() в Editor**
```csharp
// ❌ НЕПРАВИЛЬНО
animator.Play("AnimationName");
animator.Update(Time.deltaTime);

// ✅ ПРАВИЛЬНО
AnimationMode.StartAnimationMode();
AnimationMode.SampleAnimationClip(gameObject, clip, time);
```

**4. НЕ вызывать FixObstacleTypes для Locations**
```csharp
// ❌ НЕПРАВИЛЬНО (сотни ошибок "No mapping")
FixObstacleTypesInLevelInfoAndSaveToJson(levelInfo, filePath);

// ✅ ПРАВИЛЬНО
if (isTemplateLocation)
{
    FixObstacleTypesInLevelInfoAndSaveToJson(levelInfo, filePath);
}
```
**Почему:** FixObstacleTypes валидирует obstacles через mappings manager, который не знает про decorations. В Locations patterns read-only.

**5. НЕ очищать Tilemap при загрузке decorations**
```csharp
// ❌ НЕПРАВИЛЬНО
tilemap.ClearAllTiles();
LoadDecorationsToTilemap();

// ✅ ПРАВИЛЬНО
LoadDecorationsToTilemap(); // Добавляет decorations ПОВЕРХ obstacles
```
**Почему:** В Locations режиме obstacles показываются read-only для контекста. Очистка Tilemap удалит их.

**6. НЕ инстанцировать префаб для получения размеров спрайта**
```csharp
// ❌ НЕПРАВИЛЬНО
var instance = Instantiate(prefab);
var bounds = instance.GetComponent<SpriteRenderer>().sprite.bounds;

// ✅ ПРАВИЛЬНО
var curve = AnimationUtility.GetObjectReferenceCurve(clip, spriteBinding);
var sprite = curve[0].value as Sprite;
var bounds = sprite.bounds;
```
**Почему:** Префаб может содержать дефолтный/пустой спрайт.

### ✅ Best Practices

**1. Проверять Architecture Knowledge Base ПЕРВЫМ**
- Перед началом сложной задачи читать этот документ
- Экономит время и предотвращает ошибки

**2. Использовать DebugManager.DiagLog() для отладки**
```csharp
DebugManager.DiagLog($"[Component] Important data: {value}");
```
- Автоматическая запись в `EditorLogs/diagnostic_log.txt`
- Удобно для анализа последовательности событий
- Удалить временные логи перед коммитом

**3. Читать Unity API документацию ПЕРЕД использованием**
- Особенно для Editor API (AnimationMode, SceneView)
- Unity API обширный, не изобретать велосипед

**4. Тестировать инкрементально**
- Не делать 10 изменений сразу
- После каждого изменения — компиляция + проверка
- Использовать `get_errors` для валидации

**5. Использовать два Addressables лейбла для разделения типов спрайтов**
```csharp
// Obstacles
"New York obstacles sprites"

// Decorations
"New York decor sprites"
```
- Загружать оба лейбла через `Addressables.LoadAssetsAsync`
- Фильтровать через `IsObstacleSprite()` (префиксы "obstacle" / "decor")
- Labels **регистрозависимы** — не трансформировать строки

**6. ProcessTileChange() для режим-специфичной валидации**
- Проверять `isTemplateLocation` перед редактированием
- Блокировать редактирование obstacles в Locations
- Показывать понятные Debug.LogWarning для пользователя
- Возвращать `false` при нарушении правил

---

## Future Locations

### Структура для новой локации (например, Paris)

**1. Создать папку:**
```
Assets/Content/locations/02_Paris/
├─ sprites/
├─ levels/
│  └─ Morning/
│     └─ level_01/
│        └─ level_01.json
└─ obstacle_sprite_to_type_mappings.json
```

**2. Создать анимации:**
```
obstacle_paris_big_alive_1_idle-1.png ... -18.png
obstacle_paris_big_alive_2_idle-1.png ... -24.png
...
obstacle_paris_big_alive_7_idle-1.png ... -18.png
```

**3. Импортировать в Unity:**
- Tools → Obstacle Animations → Import From Dropbox
- Выбрать папку с анимациями Paris

**4. Использовать существующие паттерны:**
- Скопировать `PatternsCollection.json`
- Заменить `new_york` на `paris` глобально
- Все паттерны будут работать с новой локацией!

**Это главное преимущество универсального подхода `big_alive_X_idle`.**

---

## Обновление документации

Этот файл должен обновляться при:
- Изменении архитектурных решений
- Обнаружении важных багов и их решений
- Добавлении новых систем/компонентов
- Изменении workflow художников/геймдизайнеров

**Формат обновлений:**
```markdown
## [Дата] Название изменения
**Что изменилось:**
- Краткое описание

**Почему:**
- Объяснение причин

**Как использовать:**
- Примеры кода / команды
```
