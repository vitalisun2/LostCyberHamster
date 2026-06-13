# Architecture Knowledge Base

## 📋 Оглавление
- [Naming Conventions](#naming-conventions)
- [Addressables System](#addressables-system)
- [Obstacle Animation System](#obstacle-animation-system)
- [Level Tilemap Editor](#level-tilemap-editor)
- [Data Flow](#data-flow)
- [Important Constants](#important-constants)
- [Lessons Learned](#lessons-learned)
- [Bot Architecture](#bot-architecture-pipeline)

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
**Старый подход (специфичные персонажи, удалён):**
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

**7. При генерации medium .anim клипов — НЕ переименовывать события**
```yaml
# ❌ НЕПРАВИЛЬНО (events data получают medium_ префикс)
data: transform_medium_jump_on_roof_end

# ✅ ПРАВИЛЬНО (события ДОЛЖНЫ совпадать с оригиналом)
data: transform_jump_on_roof_end
```
**Почему:** `HamsterAnimationEventsMechanics.OnEvent()` слушает только оригинальные имена событий
(например, `transform_jump_on_roof_end`, `transform_roof_jump_end`). Если medium-клип
стреляет `transform_medium_jump_on_roof_end`, обработчик его не распознаёт — хомяк
навсегда застревает в промежуточном состоянии (`JumpOnRoof` вместо `RoofRun`).
Механизм `SwapRoofClips` уже подменяет клипы в Animator — названия событий должны оставаться одинаковыми.

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

## Reference-Based Level Assembly (New Architecture)

### Трёхслойная модель

Уровни теперь не хранят копии паттернов, а ссылаются на них:

1. **PatternsCollection** (шаблоны) → `PatternTemplate` с `ObstacleSlot` (id, type, x, y — без spriteName)
2. **LocationTheme** (`obstacle_sprite_to_type_mappings.json`) → маппинг type → список спрайтов + default
3. **LevelInfoRef** (level JSON) → `patternSequence` со ссылками `PatternRef` ({ref, spriteSeed, overrides})

### Resolution chain (LevelResolver.Resolve)

Для каждого obstacle спрайт выбирается по приоритету:
1. **Manual override** → `PatternRef.overrides[obstacleId]`
2. **Seed-based random** → `new System.Random(spriteSeed + obstacleId)` → случайный из theme.sprites
3. **Theme default** → `SpriteTypeMapping.@default`
4. **Universal name** → для collectables: `"collectable_{type}"`

### Ключевые файлы

- `Assets/Scripts/Common/Models/` — ObstacleSlot, PatternTemplate, PatternsCollection, PatternRef, SpriteOverride, LevelInfoRef, LocationTheme
- `Assets/Scripts/System/LevelManagement/LevelResolver.cs` — статический Resolve()
- `Assets/Editor/Migration/LevelFormatMigration.cs` — миграция старого формата
- `Assets/Editor/LevelEditor/PatternSequencePanel.cs` — UI для последовательности паттернов
- `Assets/Editor/LevelEditor/SpriteOverridePanel.cs` — UI для override спрайтов

### Runtime flow

```
LevelDataProvider.LoadLevelInfo()
  → JsonUtility.FromJson<LevelInfoRef>(json)
  → LoadPatternsCollectionAsync() → "PatternsCollection" (Addressable)
  → LoadLocationThemeAsync(location) → "{location}/obstacle_sprite_to_type_mappings" (Addressable)
  → LevelResolver.Resolve(levelRef, patterns, theme)
  → LevelInfo (fully resolved, с spriteName для каждого obstacle)
```

ObstacleFactory / ObstacleSpawner работают с resolved LevelInfo — без изменений.

### Editor flow (Location Level mode)

```
HandleFileSelected()
  → LevelDataManager.LoadLevelRef(filePath)
  → LevelDataManager.LoadPatternsCollection()
  → LevelDataManager.LoadLocationTheme(location)
  → LevelResolver.Resolve() → для отображения на tilemap
HandleSaveLevelClicked()
  → LevelDataManager.SaveLevelRef(_currentLevelRef) → сохраняется LevelInfoRef (ссылки)
```

### Миграция

Menu: `Tools/Migration/` — 3 шага:
1. Migrate PatternsCollection (удаляет spriteName, добавляет id)
2. Migrate Location Themes (добавляет default)
3. Migrate Level Files (из LevelInfo → LevelInfoRef с overrides)

---

## Bot Domain Knowledge

Устойчивые выводы по механикам бота, перенесённые из workflow lessons.

### Семантика безопасности и планирования

- `safe` означает безопасность шага в окне исполнения; будущие угрозы — следующий пересчёт или chain stages.
- Для `SwitchLane` считать окно по прогнозу освобождения target-линии (динамический `execAt`), а не отбрасывать если линия unsafe сейчас.
- `SwitchLane`: моделировать все safe-windows, включая split windows (`safe → unsafe → safe`); иначе — искусственные zigzag-цепочки.
- `ThreatSafety` `SwitchLane`: target obstacle — ближайшая same-lane угроза; executor сверяет live distance именно с target.
- Chain-этапы: межлинейность — свойство запланированной последовательности шагов (zigzag 2-step), не отдельная смена линии.
- `ObjectCategory.Threat` ≠ полное множество runtime-опасных объектов. Классификатор может помечать часть как `Target`. Safety проверки сверять с runtime-dangerous type set.
- `HamsterState` — источник истины для прыжков: `JumpOver` = перепрыгивание.
- Chain-stage тесты: все объекты цепочки должны попадать в один initial snapshot (в пределах `scanRange`).
- Runtime bot planning работает event-driven: plan rebuild запрашивается на `LevelStart`, `BotEnabled`, `SpawnPattern`, `ActionCompleted` и `ActionCancelled`; таймерного rolling rebuild нет.
- Snapshot horizon для бота — все active `ObstacleSpawner.SpawnedObstacles`, без искусственной правой границы видимости.
- Planner no-action/dead-end reasons — это diagnosis, а не доказанный level dead-end. Подтвержденный dead-end для валидатора уровня фиксируется только после runtime-потери жизни (`LivesLost`); без сохраненной diagnosis loss-of-life не должен создавать bot dead-end report.
- Для jump-стратегий нехватка энергии после подтвержденной применимости state/type/lane — это dead-end diagnosis, а не `NotApplicable`; `Specification` не должна скрывать такую причину.
- `PlanningDeadEndReport.Depth` указывает на первый zero-candidate узел, а не обязательно на ближайшую runtime-угрозу; отсутствие strategy в causes не доказывает, что она не создавала action раньше в ветке.
- Если успешных leaf-веток нет, planner использует dead-end branch fallback: возвращает максимально дальний безопасный prefix вместе с diagnosis, чтобы validator подтвердил потерю жизни ближе к реальному непроходимому участку.
- Во время ожидания trigger или выполнения head-action committed prefix удерживает до двух действий: текущую head-action и следующий action для immediate handoff; при replan пересчитывается только хвост после этого prefix. Runtime fire/cancel остаётся ответственностью executor/gate.
- Execution handoff: если после завершения/освобождения head-action в retained хвосте уже есть следующий шаг, executor должен сначала дать ему шанс стартовать, а rebuild должен пересчитывать только хвост после нового in-progress шага.
- Для actions с ранним handoff-событием (`JumpOn` уничтожает target на `transform_jumped_on`, но возвращается в `Run` позже) completion для следующего bot action нельзя сводить только к финальному `Run`.
- Для timed jump-on objective при равных target/стоимости/тапах выбирать ветку с более ранним первым trigger, чтобы не сужать runtime fire-window первого action.
- Для target-bound jump-on post-action safety должен участвовать в выборе fire shift внутри окна; нельзя отбрасывать весь action только потому, что первый runtime-valid timing возвращает хомяка в unsafe `Run`, если поздний timing того же окна safe.

### ActionGenerator

- Ограничения — семантический инвариант, не подстройка под тест-кейс. `SwitchLaneTargetMinFireDist` кодирует «SwitchLane к Target нужно место для Jump».
- `IsSwitchLaneSafeAtDistance`: только transit (0.3с), не post-transit. Post-transit = минимальная дистанция fire + chain-проекция. Если хомяк врезается ПОСЛЕ transit — причина не в safety check.
- `TryComputeSwitchLaneExecuteDistance`: учитывает все target-lane threats (включая далёкие). Для Target-категории минимум выше чем для Threat/Collectible.

### SwitchLane механика

- `IsOnBottomLine` переключается мгновенно при `TapRequest` (`TapMechanics.OnTap`). Source-lane препятствия не могут навредить после `TapRequest`.
- `SwitchLane` planning после fire должен учитывать `DecisionTravel`: линия меняется сразу, но следующий tap runtime не принимает, пока `Hamster.IsShifting`.
- `SwitchLane` из `RoofRun` безопасен только если в момент tap на target-линии под хомяком есть roof support; иначе runtime принудительно уходит в `RunFromRoof`, а same-line non-roof obstacles могут сразу нанести damage.
- Не добавлять фильтры по типу препятствия для обхода safety-логики. Проблема в модели, а не в типе.
- Planning-проекция проверяет конечную позицию по snapshot-данным — грубая оценка. Runtime execution handlers проверяют live-дистанцию/окно срабатывания; если live-check не проходит до deadline — шаг отменяется.

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

---

## Decoration System (Runtime)

### Архитектура
Декор (кусты, деревья) — чисто визуальный слой, не участвующий в геймплее. Обрабатывается отдельно от obstacles.

### Ключевые компоненты
- **`DecorationSpawner.cs`** — MonoBehaviour, Singleton. Создаёт GO из `decorationPatterns`, скроллит с RoadScrollSpeed, активирует/деактивирует по видимости.
- **`InitDecorationsLoadingTask.cs`** — загрузочная задача, вызывает `DecorationSpawner.Init()` после инициализации дороги.

### Координаты декора
- В JSON хранятся **cell-координаты** (int `xPos`, `yPos`) из тайлмапа редактора
- Конвертация в world: `worldX = xPos * GridSnapStep (0.2)`, `worldY = yPos * GridSnapStep (0.2)`
- Координаты **абсолютные** для всего уровня (не привязаны к паттернам)

### Sorting Layer "Decor"
- Между Road и UpperSprites: Sky → Background2 → Background → Road → **Decor** → UpperSprites → Hamster → LowerSprites
- Y-based sortingOrder: `sortingOrder = RoundToInt(-worldY * 100)` — нижние элементы перекрывают верхние

### Отличия от Obstacles
| | Obstacles | Decorations |
|---|---|---|
| Координаты | локальные (внутри паттерна) | абсолютные (для всего уровня) |
| Спавн | пачками, с dynamic offset | потоком, по мере приближения к экрану |
| Коллизии | да (BoxCollider2D) | нет |
| Компонент | Obstacle (MonoBehaviour) | просто SpriteRenderer |
| Sorting | UpperSprites / LowerSprites | Decor (Y-based order) |

### EnvironmentRoot
Содержит `DecorationsContainer` — дочерний Transform для хранения декор-объектов.

---

## Bot Architecture Pipeline

**Папка:** `Assets/Scripts/Bot/`
**Оркестратор:** `RuntimeBotController.cs` (MonoBehaviour, `IGameStartListener`, `IGameLateUpdateListener`)

### Pipeline

```
RuntimeBotController
  → SnapshotBuilder
  → PlanExecutor
  → PlanBuilder
    → PlanningGraphBuilder
      → DecisionPointDetector
    → ActionGenerator
    → TransitionSimulator
    → PlanEvaluator
```

1. **`SnapshotBuilder`** (`Perception/`) — собирает `WorldSnapshot` из живых Unity-объектов, включая все active spawned obstacles.
2. **`PlanExecutor`** (`Execution/`) — исполняет только head-action текущего `BotPlan` и возвращает `PlanExecutionTickResult`.
3. **`PlanBuilder`** (`Planning/`) — строит новый `BotPlan` с нуля по live snapshot.
4. **`PlanningGraphBuilder`** (`Planning/`) — раскрывает дерево решений до `MaxSearchDepth = 6`.
5. **`ActionGenerator`** (`Planning/`) — опрашивает `IPlanningStrategy`, возвращает role-based кандидаты для текущей точки решения.
6. **`TransitionSimulator`** (`Planning/`) — симулирует действие через simulator соответствующей strategy.
7. **`PlanEvaluator`** (`Planning/`) — выбирает лучшую ветку по objective, energy cost, tap count и progression.

**Триггеры пересчёта** (в `RuntimeBotController`):
- `LevelStart` — первичное построение plan после старта gameplay;
- `BotEnabled` — построение plan после включения бота;
- `SpawnPattern` — пересборка после добавления нового pattern в `ObstacleSpawner.SpawnedObstacles`;
- `PlanExecutionTickResult.Completed` — head-action завершён, следующий plan строится от live snapshot или от новой in-progress head после immediate handoff;
- `PlanExecutionTickResult.Cancelled` — head-action отменён, следующий plan строится от live snapshot;
- `PlanExecutionTickResult.Fired` и `None` сами по себе дерево решений не перестраивают.

### Классификация объектов

| Тип | Категория | Условие |
|---|---|---|
| `smallAlive` | Target | всегда |
| `bigAlive` | Target | хомяк на крыше |
| `bigAlive` | Threat | хомяк не на крыше |
| `bigNotAlive`, `mediumNotAlive`, `smallNotAliveRoad`, `smallNotAliveRoadAndRoof` | Threat | всегда |
| collectables (energetic, pizza, crystal, life, coin) | Collectible | всегда |

### Зарегистрированные стратегии

| Стратегия | Файл | Генерирует | Ограничение |
|---|---|---|---|
| `SwitchLaneStrategy` | `Strategies/SwitchLaneStrategy.cs` | `SwitchLane` | все Threat-типы |
| `JumpStrategy` | `Strategies/JumpStrategy.cs` | `Jump` | только `smallNotAliveRoad` + `smallNotAliveRoadAndRoof` |

### Ключевые константы

- `JumpFireDist = 1.5f` — дистанция, при которой стреляет Jump
- `BotConsts.JumpLandingOffset` — смещение мира к моменту приземления
- `BotConsts.SwitchLaneDecisionTravel` — смещение к завершению перестроения
- `ProjectedWorld.GetHamsterLeftX()` — левый край хомяка (HamsterRightX − HamsterWidth)

### Актуальные документы по боту

- `docs/Planning/bot_implementation_plan.md` — поэтапный roadmap (этапы 1–15), статус выполнения.
- `docs/Planning/in-progress/bot_current_state.md` — текущие возможности бота и тестируемые сценарии.
