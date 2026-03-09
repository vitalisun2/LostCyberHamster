# Level Assembly Refactor: Reference-Based Architecture

## Статус: Концептуальная проработка

---

## 1. Проблема

### Текущее состояние

Сборка уровней работает по принципу **Copy-Paste**:

1. `PatternsCollection.json` хранит паттерны с конкретными sprite именами (привязка к локации)
2. Консольная утилита `LevelBuilder.exe` копирует полные данные паттернов в level JSON (deep clone)
3. Level JSON содержит полные копии всех паттернов со всеми данными

### Что не устраивает

- **Изменение паттерна → пересборка всех уровней.** Поменял позицию одного препятствия в `easy_run` → нужно руками пересобрать каждый уровень, использующий этот паттерн.
- **Паттерны привязаны к локации.** `PatternsCollection.json` содержит sprite имена вида `obstacle_new_york_*`. Паттерны невозможно переиспользовать для Парижа или другой локации без ручной замены всех спрайтов.
- **Нет per-level кастомизации спрайтов.** Нельзя в одном уровне заменить конкретную собаку на кота, не потеряв связь с паттерном.
- **Внешняя утилита.** Сборка уровней через консольный `LevelBuilder.exe` — лишний шаг вне Unity.

---

## 2. Целевая архитектура: Трёхслойная модель разрешения

Аналогия: **Unity Prefab Variants** (базовый префаб → вариант → инстанс-оверрайды) или **CSS-каскадность** (defaults → class → inline).

### Слой 1: Patterns Collection (СТРУКТУРА)

Паттерны описывают **только геймдизайн и физику** — типы препятствий и их позиции. Никаких sprite имён.

```json
{
  "patterns": [
    {
      "name": "easy_run",
      "description": "простой участок, мало препятствий",
      "obstacles": [
        { "id": 0, "type": 2, "x": 22.2, "y": -2.8 },
        { "id": 1, "type": 0, "x": 61.6, "y": -2.8 },
        { "id": 2, "type": 9, "x": 45.0, "y": 1.2 }
      ]
    }
  ]
}
```

**Ключевые свойства:**
- Каждый obstacle имеет стабильный `id` внутри паттерна (не индекс массива — id сохраняется при перестановке/удалении)
- `type` — это `ObstacleTypeEnum` (0-11), определяет поведение и размеры
- `x`, `y` — позиции в мировых координатах
- `spriteName` **отсутствует** — паттерн "слеп" к визуалу
- Паттерны **location-agnostic**: один `easy_run` работает для любой локации

### Слой 2: Location Theme (ВИЗУАЛЬНАЯ ПАЛИТРА)

Уже существующий `obstacle_sprite_to_type_mappings.json`, расширенный полем `default`:

```json
{
  "location": "new_york",
  "obstacle_sprite_to_type_mappings": [
    {
      "type": 0,
      "sprites": ["obstacle_new_york_dog", "obstacle_new_york_homeless"],
      "default": "obstacle_new_york_dog"
    },
    {
      "type": 1,
      "sprites": [
        "obstacle_new_york_businessman",
        "obstacle_new_york_granny",
        "obstacle_new_york_hipster",
        "obstacle_new_york_big_alive_1_idle",
        "obstacle_new_york_big_alive_3_idle",
        "obstacle_new_york_big_alive_7_idle"
      ],
      "default": "obstacle_new_york_businessman"
    },
    {
      "type": 4,
      "sprites": ["obstacle_new_york_car_1"],
      "default": "obstacle_new_york_car_1"
    }
  ]
}
```

**Ключевые свойства:**
- Каждая локация определяет свою визуальную палитру
- `sprites` — все доступные спрайты для данного типа в этой локации
- `default` — спрайт по умолчанию (используется, если нет оверрайда)
- Коллекционные предметы (`coin`, `pizza`, `crystal`, `life`, `energetic`) — универсальные, не зависят от локации

### Слой 3: Level JSON (ССЫЛКИ + ОВЕРРАЙДЫ + ДЕКОР)

Уровень хранит **только то, что уникально для него**:

```json
{
  "skyTexture": "bg_new_york_morning",
  "background2Texture": "bg_2_new_york_morning",
  "backgroundTexture": "bg_new_york_morning",
  "roadTexture": "",
  "location": "new_york",

  "patternSequence": [
    {
      "ref": "easy_run",
      "overrides": []
    },
    {
      "ref": "small_jumps",
      "overrides": [
        {
          "obstacleId": 3,
          "spriteName": "obstacle_new_york_granny"
        }
      ]
    },
    {
      "ref": "medium_difficulty",
      "overrides": [
        {
          "obstacleId": 0,
          "spriteName": "obstacle_new_york_hipster"
        }
      ]
    }
  ],

  "decorationPatterns": [
    {
      "decorationTiles": [
        { "name": "decor_bush_1", "xPos": 5, "yPos": 10 },
        { "name": "decor_tree_1", "xPos": 12, "yPos": 8 }
      ]
    }
  ]
}
```

**Ключевые свойства:**
- `patternSequence` — упорядоченный список ссылок на паттерны (по имени)
- `overrides` — точечные замены спрайтов конкретных препятствий в конкретном паттерне
- Обращение к препятствиям по стабильному `obstacleId` (не по индексу массива)
- Один и тот же паттерн может встречаться в `patternSequence` несколько раз — оверрайды привязаны к конкретному вхождению (позиции в массиве), а не к имени паттерна глобально
- `decorationPatterns` — уникальный per-level декор (без изменений относительно текущей системы)

---

## 3. Алгоритм разрешения (Resolution Chain)

При загрузке уровня (в Level Editor или в Runtime):

```
Вход: level JSON + PatternsCollection + Location Theme

Для каждого элемента patternSequence[i]:
  1. Найти паттерн по ref в PatternsCollection
     → Ошибка + warning, если паттерн не найден
  
  2. Для каждого obstacle в паттерне:
     a. Проверить overrides уровня:
        - Есть override для этого obstacleId? → использовать указанный spriteName
     
     b. Нет override? → Обратиться к Location Theme:
        - По obstacle.type найти маппинг для location уровня
        - Взять default спрайт
        - (Опционально в будущем: если sprites > 1, использовать seed для детерминированного выбора)
     
  3. Собрать финальный ObstacleModel = {spriteName, type, x, y}

Результат: полностью разрешённый LevelInfo с конкретными спрайтами
```

**Важно:** Resolution происходит на этапе загрузки. К моменту, когда данные попадают в `ObstacleFactory` / `ObstacleSpawner`, все ссылки уже разрешены. Runtime-код не требует изменений.

---

## 4. Детерминированная вариативность (Seed-Based Randomness)

Для автоматического визуального разнообразия без ручных оверрайдов.

**Проблема:** Если для type:1 (BigAlive) в НЙ есть 7 спрайтов, а в паттерне 3 BigAlive, то без оверрайдов все три будут `default` (businessman). Скучно.

**Решение:** Необязательное поле `spriteSeed` на уровне level JSON:

```json
{
  "patternSequence": [
    {
      "ref": "easy_run",
      "spriteSeed": 42,
      "overrides": []
    }
  ]
}
```

При resolution, если нет ручного оверрайда для данного obstacle:
1. Инициализировать RNG с `seed = spriteSeed + obstacleId`
2. Выбрать спрайт из массива `sprites` Location Theme
3. Результат детерминирован: одинаковый при каждом запуске

Если `spriteSeed` не указан — всегда используется `default`.

**Это опциональная фича.** Можно реализовать позже, после базовой системы.

---

## 5. UI: Сборка уровней в Level Editor

Консольная утилита `LevelBuilder.exe` больше не нужна. Формирование `patternSequence` переносится в Level Editor.

### 5.1 Pattern Sequence Panel

Новая панель в окне Level Editor (сбоку или внизу), содержащая:

**Левая часть — Библиотека паттернов:**
- Список всех паттернов из `PatternsCollection.json`
- Поиск/фильтрация по имени
- Preview: при наведении/выборе — краткое описание и количество препятствий

**Правая часть — Последовательность уровня:**
- Упорядоченный список паттернов текущего уровня (`patternSequence`)
- Drag & Drop для перестановки порядка
- Кнопки добавления/удаления
- Перетаскивание из библиотеки в последовательность

**Взаимодействие:**
- Двойной клик по паттерну в последовательности → фокусировка на нём в viewport тайлмапа
- Иконка оверрайда рядом с паттерном, если у него есть overrides
- Кнопка «Сбросить оверрайды» на паттерне

### 5.2 Sprite Override в Viewport

Когда уровень отображён на тайлмапе (как сейчас):

1. Клик по препятствию → в инспекторе/сайд-панели показывается:
   - Тип (`BigAlive`, `Car`, и т.д.)
   - Текущий спрайт (resolved из темы или оверрайд)
   - Выпадающий список всех доступных спрайтов для этого типа в данной локации
2. Выбор другого спрайта → сохраняется как override в level JSON
3. Визуальная индикация: спрайты с оверрайдом подсвечиваются (рамка или иконка)

### 5.3 Режимы работы Level Editor (обновлённые)

| Режим | Паттерны | Декор | Оверрайды |
|-------|---------|-------|-----------|
| Templates (PatternsCollection) | Полное редактирование (позиции, типы) | Запрещён | Нет (нет уровня) |
| Location Level | Read-only (из PatternsCollection) | Полное редактирование | Полное редактирование |

---

## 6. Надёжность: Стабильные ID vs Индексы

### Проблема хрупких индексов

Если оверрайд ссылается на obstacle по индексу в массиве, то добавление/удаление/перестановка препятствий в паттерне ломает все оверрайды.

### Решение: Стабильные ID

Каждый obstacle в паттерне получает `id` — целое число, уникальное внутри паттерна. ID назначается при создании obstacle и никогда не переиспользуется.

```json
{
  "name": "easy_run",
  "obstacles": [
    { "id": 0, "type": 2, "x": 22.2, "y": -2.8 },
    { "id": 1, "type": 0, "x": 61.6, "y": -2.8 },
    { "id": 2, "type": 9, "x": 45.0, "y": 1.2 }
  ]
}
```

Если удалить obstacle с id=1, оставшиеся сохраняют свои id (0 и 2). Новый obstacle получит id=3.

**Валидация при загрузке:**
- Override ссылается на несуществующий obstacleId → warning в консоли, override игнорируется
- Паттерн из `ref` не найден в PatternsCollection → ошибка, уровень не загружается

---

## 7. Workflow-сценарии

### Сценарий 1: Создание нового уровня
1. Открыть Level Editor → File → New Level
2. Указать локацию (New York), время суток (Morning) → подтягиваются фоны и тема
3. В панели библиотеки — перетащить паттерны в последовательность: `easy_run`, `small_jumps`, `medium_difficulty`
4. На viewport отобразится собранный уровень с дефолтными спрайтами из темы НЙ
5. Расставить декор
6. Сохранить → level JSON содержит ссылки + декор, никаких копий

### Сценарий 2: Кастомизация спрайтов
1. Открыть существующий уровень
2. Видишь `obstacle_new_york_dog` (дефолт для type:0 в NY)
3. Кликаешь на собаку → в сайд-панели выпадающий список: `dog`, `homeless`
4. Выбираешь `homeless` → Level Editor сохраняет override
5. Все остальные type:0 в уровне остаются дефолтными

### Сценарий 3: Изменение паттерна (балансировка)
1. Переключиться на Templates → открыть `PatternsCollection.json`
2. Передвинуть obstacle в паттерне `easy_run`
3. Сохранить
4. Открыть любой уровень, использующий `easy_run` → позиции обновились автоматически
5. Спрайтовые оверрайды и декор не затронуты

### Сценарий 4: Новая локация (Париж)
1. Создать папку `02_Paris/` с `obstacle_sprite_to_type_mappings.json`
2. Заполнить маппинги парижскими спрайтами: `obstacle_paris_poodle` для type:0, и т.д.
3. Создать новый уровень с `"location": "paris"`
4. Использовать **те же самые паттерны** из PatternsCollection
5. Все спрайты автоматически резолвятся в парижские версии

---

## 8. Затрагиваемые компоненты

| Компонент | Текущее состояние | Что меняется |
|-----------|------------------|-------------|
| `PatternsCollection.json` | Содержит `spriteName` | Убрать `spriteName`, добавить `id` каждому obstacle |
| `LevelBuilder.exe` | Копирует полные паттерны | Больше не нужен — логика переезжает в Level Editor |
| Level JSON формат | Полные копии паттернов | Ссылки (`ref`) + `overrides` + декор |
| `LevelDataManager.cs` | Загружает плоские данные | Новый resolution layer: разрешает ссылки через PatternsCollection + Theme |
| `LevelTilemapEditor.cs` | Редактирует obstacles напрямую | В режиме locations: UI для оверрайдов и sprite выбора |
| `LevelTilemapUi.cs` | Текущий UI | Новая панель Pattern Sequence + Sprite Override Panel |
| `LevelInfo.cs` | `List<Pattern> patterns` | Новая модель: `List<PatternRef> patternSequence` |
| `ObstacleFactory.cs` | Читает `spriteName` из level | **Без изменений** — получает уже resolved данные |
| `ObstacleSpawner.cs` | Итерирует по patterns | **Без изменений** — работает с resolved данными |
| `obstacle_sprite_to_type_mappings.json` | Маппинг type→sprites | Добавить поле `default` |

---

## 9. Миграция существующих данных

### Этап 1: PatternsCollection
- Убрать `spriteName` из каждого obstacle
- Назначить стабильные `id` (0, 1, 2, ...) каждому obstacle в каждом паттерне
- Автоматизируется скриптом

### Этап 2: Существующие уровни (level_01, level_02)
- Конвертировать из формата «полные копии» в формат «ссылки + оверрайды»
- Для каждого паттерна в уровне: найти соответствующий паттерн в PatternsCollection по имени
- Сравнить sprite каждого obstacle с default из Location Theme
- Если отличается → записать как override
- Декор переносится как есть
- Автоматизируется скриптом миграции

### Этап 3: Runtime код
- Добавить resolution layer между загрузкой JSON и передачей в ObstacleFactory
- ObstacleFactory/ObstacleSpawner не трогаем

---

## 10. Принятые решения

- [x] **Категоризация паттернов:** Не нужна. Достаточно naming convention (`easy_run`, `medium_difficulty`, `peak` и т.д.).
- [x] **Seed-based randomness:** Реализовать в рамках основной работы (не откладывать).
- [x] **Оверрайд позиции:** Не нужен, избыточно. Override только для замены спрайта.
- [x] **Multiple pattern instances:** Работает автоматически — overrides привязаны к конкретному элементу массива `patternSequence` (по позиции вхождения), а не к имени паттерна глобально. Два вхождения `easy_run` имеют независимые overrides.
- [x] **Совместимость Level Editor:** Переработка Level Editor целиком под новый формат. Старый формат совместим со старым кодом, новый Level Editor работает только с новым форматом. Скрипт миграции конвертирует существующие уровни.

---
---

# ЧАСТЬ 2: ТЕХНИЧЕСКОЕ ЗАДАНИЕ

---

## T1. Новые и изменяемые модели данных (C#)

### T1.1 Новый формат PatternsCollection

**Файл:** `Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`

Каждый obstacle в паттерне получает поле `id`, теряет поле `spriteName`:

```json
{
  "patterns": [
    {
      "name": "easy_run",
      "description": "простой участок, мало препятствий",
      "nextObstacleId": 3,
      "obstacles": [
        { "id": 0, "type": 2, "x": 22.2, "y": -2.8 },
        { "id": 1, "type": 0, "x": 61.6, "y": -2.8 },
        { "id": 2, "type": 9, "x": 45.0, "y": 1.2 }
      ]
    }
  ]
}
```

Поле `nextObstacleId` — счётчик для генерации следующего id. При добавлении нового obstacle в редакторе: `newObstacle.id = pattern.nextObstacleId++`. Гарантирует уникальность id даже после удалений.

### T1.2 Новый формат Level JSON

**Файлы:** `Assets/Content/locations/{location}/levels/{partOfDay}/level_NN/level_NN.json`

```json
{
  "skyTexture": "bg_new_york_morning",
  "background2Texture": "bg_2_new_york_morning",
  "backgroundTexture": "bg_new_york_morning",
  "roadTexture": "",
  "location": "new_york",

  "patternSequence": [
    {
      "ref": "easy_run",
      "spriteSeed": 42,
      "overrides": []
    },
    {
      "ref": "small_jumps",
      "spriteSeed": 17,
      "overrides": [
        { "obstacleId": 3, "spriteName": "obstacle_new_york_granny" }
      ]
    }
  ],

  "decorationPatterns": [
    {
      "decorationTiles": [
        { "name": "decor_bush_1", "xPos": 378, "yPos": -16 }
      ]
    }
  ]
}
```

### T1.3 Расширение obstacle_sprite_to_type_mappings.json

**Файлы:** `Assets/Content/locations/{location}/obstacle_sprite_to_type_mappings.json`

Добавить поле `default` в каждый маппинг:

```json
{
  "obstacle_sprite_to_type_mappings": [
    {
      "type": 0,
      "sprites": ["obstacle_new_york_dog", "obstacle_new_york_homeless"],
      "default": "obstacle_new_york_dog"
    }
  ]
}
```

Правило: `default` всегда первый элемент массива `sprites`. Это позволяет не хранить `default` явно, а вычислять как `sprites[0]`. Но для явности и читаемости оставляем отдельное поле.

### T1.4 Новые C# классы

**Файл:** `Assets/Scripts/Common/Models/PatternRef.cs` (новый)

```csharp
using System;
using System.Collections.Generic;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class PatternRef
    {
        public string @ref;
        public int spriteSeed;
        public List<SpriteOverride> overrides = new();
    }
}
```

**Файл:** `Assets/Scripts/Common/Models/SpriteOverride.cs` (новый)

```csharp
using System;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class SpriteOverride
    {
        public int obstacleId;
        public string spriteName;
    }
}
```

**Файл:** `Assets/Scripts/Common/Models/PatternTemplate.cs` (новый)

Модель для паттерна в PatternsCollection (без spriteName, с id):

```csharp
using System;
using System.Collections.Generic;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class PatternTemplate
    {
        public string name;
        public string description;
        public int nextObstacleId;
        public List<ObstacleSlot> obstacles = new();
    }
}
```

**Файл:** `Assets/Scripts/Common/Models/ObstacleSlot.cs` (новый)

```csharp
using System;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class ObstacleSlot
    {
        public int id;
        public int type;
        public float x;
        public float y;
    }
}
```

**Файл:** `Assets/Scripts/Common/Models/PatternsCollection.cs` (новый)

```csharp
using System;
using System.Collections.Generic;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class PatternsCollection
    {
        public List<PatternTemplate> patterns = new();
    }
}
```

**Файл:** `Assets/Scripts/Common/Models/LocationTheme.cs` (новый)

Модель для десериализации `obstacle_sprite_to_type_mappings.json`:

```csharp
using System;
using System.Collections.Generic;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class LocationTheme
    {
        public List<SpriteTypeMapping> obstacle_sprite_to_type_mappings = new();
    }

    [Serializable]
    public class SpriteTypeMapping
    {
        public int type;
        public List<string> sprites = new();
        public string @default;
    }
}
```

**Файл:** `Assets/Scripts/Common/Models/LevelInfoRef.cs` (новый)

Новый top-level контейнер для level JSON нового формата:

```csharp
using System;
using System.Collections.Generic;

namespace Assets.Scripts.Common.Models
{
    [Serializable]
    public class LevelInfoRef
    {
        public string skyTexture;
        public string background2Texture;
        public string backgroundTexture;
        public string roadTexture;
        public string location;

        public List<PatternRef> patternSequence = new();
        public List<DecorationPattern> decorationPatterns = new();
    }
}
```

### T1.5 Существующие классы — без изменений

Следующие классы **не меняются**, они продолжают использоваться как resolved-модели:

- `LevelInfo` — финальный результат resolution (передаётся в ObstacleFactory/ObstacleSpawner как сейчас)
- `Pattern` — resolved паттерн с obstacles, содержащими spriteName
- `ObstacleModel` — resolved obstacle с конкретным spriteName
- `DecorationPattern`, `DecorationTile` — без изменений
- `ObstacleTypeEnum` — без изменений

---

## T2. Level Resolution Service (ядро рефакторинга)

### T2.1 Класс LevelResolver

**Файл:** `Assets/Scripts/System/LevelManagement/LevelResolver.cs` (новый)

Чистая логика без Unity-зависимостей (тестируемая). Преобразует `LevelInfoRef` → `LevelInfo`.

```csharp
namespace Assets.Scripts.System.LevelManagement
{
    public static class LevelResolver
    {
        /// <summary>
        /// Разрешает ссылочный формат уровня в конкретный LevelInfo
        /// с заполненными spriteName для каждого obstacle.
        /// </summary>
        public static LevelInfo Resolve(
            LevelInfoRef levelRef,
            PatternsCollection patterns,
            LocationTheme theme)
        {
            // Реализация — см. алгоритм ниже
        }
    }
}
```

### T2.2 Алгоритм Resolve — детальный псевдокод

```
function Resolve(levelRef, patternsCollection, theme):
    result = new LevelInfo()
    result.skyTexture = levelRef.skyTexture
    result.background2Texture = levelRef.background2Texture
    result.backgroundTexture = levelRef.backgroundTexture
    result.roadTexture = levelRef.roadTexture
    result.decorationPatterns = levelRef.decorationPatterns  // as-is

    // Построить словарь type → SpriteTypeMapping для быстрого lookup
    themeLookup = Dictionary<int, SpriteTypeMapping>
    for each mapping in theme.obstacle_sprite_to_type_mappings:
        themeLookup[mapping.type] = mapping

    // Построить словарь name → PatternTemplate
    patternLookup = Dictionary<string, PatternTemplate> (case-insensitive)
    for each pt in patternsCollection.patterns:
        patternLookup[pt.name] = pt

    result.patterns = new List<Pattern>()

    for i = 0 to levelRef.patternSequence.Count - 1:
        patternRef = levelRef.patternSequence[i]

        // 1. Найти шаблон паттерна
        if not patternLookup.TryGetValue(patternRef.ref, out template):
            LogError("Pattern '{patternRef.ref}' not found in PatternsCollection")
            continue

        // 2. Построить словарь overrides для быстрого lookup
        overrideLookup = Dictionary<int, SpriteOverride>
        for each ov in patternRef.overrides:
            overrideLookup[ov.obstacleId] = ov

        // 3. Разрешить каждый obstacle
        resolvedPattern = new Pattern()
        resolvedPattern.name = template.name
        resolvedPattern.description = template.description
        resolvedPattern.obstacles = new List<ObstacleModel>()

        for each slot in template.obstacles:
            resolved = new ObstacleModel()
            resolved.type = slot.type
            resolved.x = slot.x
            resolved.y = slot.y

            // Приоритет 1: ручной override
            if overrideLookup.TryGetValue(slot.id, out override):
                resolved.spriteName = override.spriteName
            // Приоритет 2: seed-based выбор из темы
            else if themeLookup.TryGetValue(slot.type, out mapping):
                if patternRef.spriteSeed != 0 AND mapping.sprites.Count > 1:
                    // Детерминированный выбор
                    rng = new System.Random(patternRef.spriteSeed + slot.id)
                    index = rng.Next(mapping.sprites.Count)
                    resolved.spriteName = mapping.sprites[index]
                else:
                    resolved.spriteName = mapping.default
            else:
                // Тема не содержит маппинга для этого типа
                // Для коллекционных предметов (type 5-9) — имена универсальные
                resolved.spriteName = GetUniversalSpriteName(slot.type)

            resolvedPattern.obstacles.Add(resolved)

        result.patterns.Add(resolvedPattern)

    return result
```

### T2.3 Универсальные имена (коллекции, не зависящие от локации)

```csharp
private static string GetUniversalSpriteName(int type)
{
    return (ObstacleTypeEnum)type switch
    {
        ObstacleTypeEnum.collectableEnergetic => "energetic",
        ObstacleTypeEnum.collectablePizza => "pizza",
        ObstacleTypeEnum.collectableCrystal => "crystal",
        ObstacleTypeEnum.collectableLife => "life",
        ObstacleTypeEnum.collectableCoin => "coin",
        _ => throw new ArgumentException($"No universal sprite for type {type}")
    };
}
```

---

## T3. Интеграция в Runtime

### T3.1 LevelDataProvider — изменения

**Файл:** `Assets/Scripts/System/LevelManagement/LevelDataProvider.cs`

Метод `LoadLevelInfo` меняется:

**Было:**
```csharp
levelData.LevelInfo = JsonUtility.FromJson<LevelInfo>(asset.text);
```

**Станет:**
```csharp
// 1. Десериализовать level JSON нового формата
var levelRef = JsonUtility.FromJson<LevelInfoRef>(asset.text);

// 2. Загрузить PatternsCollection
var patternsAsset = await LoadPatternsCollectionAsync();
var patterns = JsonUtility.FromJson<PatternsCollection>(patternsAsset.text);

// 3. Загрузить Location Theme
var themeAsset = await LoadLocationThemeAsync(levelRef.location);
var theme = JsonUtility.FromJson<LocationTheme>(themeAsset.text);

// 4. Resolve
levelData.LevelInfo = LevelResolver.Resolve(levelRef, patterns, theme);
```

### T3.2 Загрузка PatternsCollection и LocationTheme

Добавить в `LevelDataProvider`:

```csharp
private static async Task<TextAsset> LoadPatternsCollectionAsync()
{
    // PatternsCollection.json регистрируется в Addressables
    // с адресом "PatternsCollection"
    return await Addressables.LoadAssetAsync<TextAsset>("PatternsCollection");
}

private static async Task<TextAsset> LoadLocationThemeAsync(string location)
{
    // obstacle_sprite_to_type_mappings.json регистрируется в Addressables
    // с адресом "{location}_theme" (например, "new_york_theme")
    var address = $"{location}_theme";
    return await Addressables.LoadAssetAsync<TextAsset>(address);
}
```

Адреса в Addressables:
- `PatternsCollection` → `Assets/Content/locations/level_design_templates/levels/PatternsCollection.json`
- `new_york_theme` → `Assets/Content/locations/01_New_York/obstacle_sprite_to_type_mappings.json`
- `paris_theme` → `Assets/Content/locations/02_Paris/obstacle_sprite_to_type_mappings.json`

### T3.3 ObstacleFactory / ObstacleSpawner — без изменений

Эти классы продолжают работать с `LevelInfo`, в котором каждый `ObstacleModel` уже содержит `spriteName`. Resolution происходит ДО их вызова. Код не трогаем.

---

## T4. Интеграция в Level Editor

### T4.1 LevelDataManager — изменения

**Файл:** `Assets/Editor/LevelEditor/LevelDataManager.cs`

#### Новые методы:

```csharp
/// Загружает PatternsCollection.json
public static PatternsCollection LoadPatternsCollection()
{
    var path = Path.Combine(Consts.LocationsPath,
        Consts.TemplatesLocationName, "levels", "PatternsCollection.json");
    var json = File.ReadAllText(path);
    return JsonUtility.FromJson<PatternsCollection>(json);
}

/// Загружает LocationTheme (obstacle_sprite_to_type_mappings.json) для указанной локации
public static LocationTheme LoadLocationTheme(string locationFolder)
{
    var path = Path.Combine(Consts.LocationsPath,
        locationFolder, "obstacle_sprite_to_type_mappings.json");
    var json = File.ReadAllText(path);
    return JsonUtility.FromJson<LocationTheme>(json);
}

/// Загружает level JSON нового формата
public static LevelInfoRef LoadLevelRef(string filePath)
{
    var json = File.ReadAllText(filePath);
    return JsonUtility.FromJson<LevelInfoRef>(json);
}

/// Сохраняет level JSON нового формата
public static void SaveLevelRef(LevelInfoRef levelRef, string filePath)
{
    var json = JsonUtility.ToJson(levelRef, true);
    File.WriteAllText(filePath, json);
}
```

#### Существующие методы `LoadLevel` и `SaveLevel`:

Остаются для режима Templates (PatternsCollection), но работают с `PatternsCollection` моделью вместо `LevelInfo`.

### T4.2 LevelTilemapEditor — изменения

**Файл:** `Assets/Editor/LevelEditor/LevelTilemapEditor.cs`

#### Режим Templates (PatternsCollection):

Работает как сейчас, но модель данных — `PatternsCollection` / `PatternTemplate` / `ObstacleSlot` вместо `LevelInfo` / `Pattern` / `ObstacleModel`.

При редактировании obstacles:
- Добавление нового obstacle → `slot.id = template.nextObstacleId++`
- Удаление obstacle → id не переиспользуется
- Нет поля `spriteName` в данных — для отображения на тайлмапе используем Location Theme (effective location = New York по дефолту, как сейчас через `ResolveLocationForObstacles`)

#### Режим Location Level:

1. Загрузить `LevelInfoRef` вместо `LevelInfo`
2. Загрузить `PatternsCollection` и `LocationTheme`
3. Вызвать `LevelResolver.Resolve()` для получения отображаемого `LevelInfo`
4. Показать на тайлмапе resolved данные (как сейчас)
5. При сохранении — сохранять `LevelInfoRef` (ссылки + overrides), не resolved данные

### T4.3 Pattern Sequence Panel (новый UI)

**Файл:** `Assets/Editor/LevelEditor/PatternSequencePanel.cs` (новый)

Встраивается в `LevelTilemapUi` как новая секция, видимая только в режиме Location Level.

**Элементы UI:**

```
┌─────────────────────────────────────────────┐
│ Pattern Sequence                            │
├──────────────────┬──────────────────────────┤
│ Available:       │ Level Sequence:          │
│                  │                          │
│ ☐ easy_run       │ 1. easy_run        [x]  │
│ ☐ easy_run_2     │ 2. small_jumps     [x]  │
│ ☐ easy_run_3     │ 3. medium_diff     [x]  │
│ ☐ small_jumps    │ 4. easy_run  (!)   [x]  │
│ ☐ small_jumps_2  │                          │
│ ☐ bonus_strip    │ [▲] [▼] [+ Add]         │
│ ☐ medium_diff    │                          │
│ ...              │ Seed: [42] [Randomize]   │
│                  │                          │
│ [Filter: ____]   │                          │
└──────────────────┴──────────────────────────┘
```

**Функциональность:**
- Левый список: все паттерны из PatternsCollection. Фильтрация по имени через текстовое поле.
- Правый список: текущий `patternSequence` уровня. Каждый элемент — `PatternRef`.
- `[x]` — удалить паттерн из последовательности.
- `[▲] [▼]` — передвинуть выбранный паттерн вверх/вниз.
- `[+ Add]` — добавить выбранный паттерн из левого списка в конец последовательности.
- `(!)` — иконка, показывающая наличие overrides у этого вхождения.
- Двойной клик по паттерну в правом списке — фокусировка viewport на этом паттерне.
- `Seed` — поле для ввода spriteSeed текущего выбранного паттерна. `[Randomize]` — генерирует случайный seed.

**Реализация:** Unity IMGUI (`EditorGUILayout`) или UI Toolkit (`VisualElement`), в зависимости от текущего подхода в `LevelTilemapUi`. Текущий `LevelTilemapUi` использует UI Toolkit (`VisualElement`), значит новая панель тоже на UI Toolkit.

### T4.4 Sprite Override Panel (новый UI)

При клике на obstacle в viewport (режим Location Level):

```
┌────────────────────────────────────┐
│ Obstacle Override                  │
│                                    │
│ Pattern: easy_run                  │
│ Obstacle ID: 3                     │
│ Type: bigAlive (1)                 │
│                                    │
│ Current sprite: businessman        │
│ Source: [Theme default]            │
│                                    │
│ Available sprites:                 │
│ ○ businessman (default)            │
│ ● granny                          │
│ ○ hipster                          │
│ ○ big_alive_1_idle                 │
│ ○ big_alive_3_idle                 │
│ ○ big_alive_7_idle                 │
│                                    │
│ [Reset to default]                 │
└────────────────────────────────────┘
```

**Функциональность:**
- Показывает информацию о выбранном obstacle.
- `Source` — откуда взят текущий спрайт: `[Theme default]`, `[Seed random]`, или `[Manual override]`.
- Список доступных спрайтов берётся из `LocationTheme` по типу obstacle.
- Выбор спрайта → запись/обновление override в `LevelInfoRef.patternSequence[i].overrides`.
- `[Reset to default]` — удаляет override для этого obstacle.

---

## T5. Seed-Based Deterministic Randomness

### T5.1 Механика

Поле `spriteSeed` (int) в каждом `PatternRef`:
- `spriteSeed == 0` → все obstacle получают `default` спрайт из темы (нет рандома)
- `spriteSeed != 0` → для каждого obstacle без override перебирается детерминированный RNG

### T5.2 Алгоритм выбора спрайта

```csharp
// Для каждого obstacle без ручного override:
if (spriteSeed != 0 && mapping.sprites.Count > 1)
{
    var rng = new System.Random(spriteSeed + obstacleSlot.id);
    var index = rng.Next(mapping.sprites.Count);
    spriteName = mapping.sprites[index];
}
else
{
    spriteName = mapping.@default;
}
```

**Свойства:**
- Один и тот же seed + obstacleId → всегда один и тот же спрайт
- Разные seed → разное визуальное распределение
- Ручной override имеет приоритет над seed
- Seed хранится per-PatternRef, не per-level (разные вхождения одного паттерна могут иметь разные seed)

### T5.3 UI интеграция

В Pattern Sequence Panel:
- При выборе PatternRef в правом списке появляется поле `Seed: [___]`
- Кнопка `[Randomize]` → `spriteSeed = UnityEngine.Random.Range(1, int.MaxValue)`
- При изменении seed → viewport обновляется (re-resolve и перерисовка)

В Sprite Override Panel:
- Поле `Source` показывает `[Seed random]` если спрайт определён через seed
- Ручной выбор спрайта перекрывает seed для конкретного obstacle

---

## T6. Скрипт миграции существующих данных

### T6.1 MigratePatternsCollection

**Файл:** `Assets/Editor/Migration/LevelFormatMigration.cs` (новый)

Меню: `Tools → Migration → Migrate PatternsCollection`

```
Алгоритм:
1. Прочитать текущий PatternsCollection.json (старый формат с LevelInfo)
2. Для каждого Pattern:
   a. Создать PatternTemplate с тем же name, description
   b. Для каждого ObstacleModel (i = 0..N):
      - Создать ObstacleSlot { id = i, type, x, y }
      - spriteName отбрасывается
   c. nextObstacleId = N
3. Записать новый PatternsCollection.json
4. Backup старого файла как PatternsCollection_backup.json
```

### T6.2 MigrateLevelFiles

Меню: `Tools → Migration → Migrate Level Files`

```
Алгоритм:
1. Для каждого level_NN.json в locations/{location}/levels/:
   a. Прочитать LevelInfo (старый формат)
   b. Загрузить PatternsCollection (уже мигрированный, новый формат)
   c. Загрузить LocationTheme для location
   d. Создать LevelInfoRef:
      - Скопировать текстуры (sky, background, background2, road)
      - location = определить из пути файла
      - decorationPatterns = скопировать as-is
   e. Для каждого Pattern в LevelInfo:
      - Найти PatternTemplate по name в PatternsCollection
      - Создать PatternRef { ref = name, spriteSeed = 0, overrides = [] }
      - Для каждого ObstacleModel:
        * Найти соответствующий ObstacleSlot по позиции (совпадение type + x + y)
        * Определить default спрайт из LocationTheme для этого type
        * Если model.spriteName != default → override { obstacleId = slot.id, spriteName = model.spriteName }
   f. Записать level_NN.json в новом формате
   g. Backup старого файла
```

### T6.3 MigrateLocationThemes

Меню: `Tools → Migration → Migrate Location Themes`

```
Алгоритм:
1. Для каждого obstacle_sprite_to_type_mappings.json:
   a. Прочитать текущий формат
   b. Для каждого маппинга добавить "default": sprites[0]
   c. Записать обновлённый файл
   d. Backup старого файла
```

---

## T7. Addressables — изменения конфигурации

### T7.1 Новые записи

| Addressable Address | Asset Path |
|---|---|
| `PatternsCollection` | `Assets/Content/locations/level_design_templates/levels/PatternsCollection.json` |
| `new_york_theme` | `Assets/Content/locations/01_New_York/obstacle_sprite_to_type_mappings.json` |
| `paris_theme` | `Assets/Content/locations/02_Paris/obstacle_sprite_to_type_mappings.json` |

### T7.2 Существующие записи — без изменений

Уровни (`levels/new_york/01.json`), спрайты, анимации — адреса остаются те же. Меняется только содержимое level JSON.

---

## T8. Тесты

### T8.1 LevelResolverTests (Unit Tests)

**Файл:** `Assets/Editor/Tests/EditMode/LevelResolverTests.cs` (новый)

Тесты для `LevelResolver.Resolve()` — ядра логики. Не требуют Unity runtime, работают с чистыми C# объектами.

#### Тест-кейсы:

**Базовый resolution:**
1. `Resolve_SinglePattern_ResolvesDefaultSprites` — один паттерн, нет overrides, нет seed → все спрайты = default из темы
2. `Resolve_CollectableTypes_ResolvedToUniversalNames` — types 5-9 → "energetic", "pizza", "crystal", "life", "coin"
3. `Resolve_MultiplePatterns_ResolvedInOrder` — три паттерна → result.patterns содержит три resolved паттерна в том же порядке

**Overrides:**
4. `Resolve_WithSpriteOverride_OverrideTakesPriority` — override для одного obstacle → спрайт = override, остальные = default
5. `Resolve_OverrideForNonexistentId_IgnoredWithoutError` — override с несуществующим obstacleId → пропускается, остальные resolved нормально
6. `Resolve_MultipleOverridesInPattern_AllApplied` — несколько overrides в одном PatternRef → все применены

**Seed-based randomness:**
7. `Resolve_WithSeed_DeterministicSpriteSelection` — seed=42 → один и тот же результат при повторном вызове
8. `Resolve_DifferentSeeds_DifferentResults` — seed=42 vs seed=99 → разные спрайты (при >1 спрайтах в теме)
9. `Resolve_SeedZero_AlwaysDefault` — seed=0 → все спрайты = default
10. `Resolve_SeedWithOverride_OverrideWins` — seed + override для того же obstacle → override имеет приоритет
11. `Resolve_SeedWithSingleSprite_ReturnsDefault` — seed задан, но sprites.Count == 1 → всегда default (нечего рандомизировать)

**Обработка ошибок:**
12. `Resolve_PatternNotFound_LogsErrorAndSkips` — ref ссылается на несуществующий паттерн → skip + warning
13. `Resolve_TypeNotInTheme_UsesUniversalName` — тип не найден в теме, но это collectible → universal name
14. `Resolve_EmptyPatternSequence_ReturnsEmptyPatterns` — пустой patternSequence → пустой result.patterns

**Multiple instances:**
15. `Resolve_SamePatternTwice_IndependentOverrides` — easy_run дважды с разными overrides → разные resolved спрайты

### T8.2 MigrationTests (Unit Tests)

**Файл:** `Assets/Editor/Tests/EditMode/MigrationTests.cs` (новый)

1. `MigratePatternsCollection_AssignsSequentialIds` — после миграции каждый obstacle имеет уникальный id
2. `MigratePatternsCollection_RemovesSpriteName` — после миграции ObstacleSlot не содержит spriteName
3. `MigrateLevel_PreservesDecorations` — decorationPatterns переносятся без изменений
4. `MigrateLevel_DetectsOverrides` — если sprite в уровне отличается от default темы → записывается override
5. `MigrateLevel_NoOverridesWhenDefault` — если все спрайты = default → overrides пуст

### T8.3 Интеграционные тесты (Editor Tests)

**Файл:** `Assets/Editor/Tests/EditMode/LevelLoadingIntegrationTests.cs` (новый)

1. `LoadResolvedLevel_MatchesOldFormat` — загрузить мигрированный level JSON + resolve → сравнить с результатом загрузки старого формата. Позиции, типы и спрайты должны совпасть.
2. `RoundTrip_SaveAndLoad_Preserves` — создать LevelInfoRef → save → load → resolve → проверить данные

---

## T9. Порядок реализации (этапы)

### Этап 1: Модели данных и LevelResolver
1. Создать новые C# модели (T1.4): `PatternTemplate`, `ObstacleSlot`, `PatternsCollection`, `PatternRef`, `SpriteOverride`, `LevelInfoRef`, `LocationTheme`
2. Реализовать `LevelResolver.Resolve()` (T2)
3. Написать и прогнать `LevelResolverTests` (T8.1)

### Этап 2: Миграция данных
4. Реализовать скрипт миграции `LevelFormatMigration` (T6)
5. Написать и прогнать `MigrationTests` (T8.2)
6. Мигрировать `PatternsCollection.json` (T6.1)
7. Мигрировать `obstacle_sprite_to_type_mappings.json` для всех локаций (T6.3)
8. Мигрировать существующие level JSON (level_01, level_02) (T6.2)

### Этап 3: Runtime интеграция
9. Обновить `LevelDataProvider` — загрузка нового формата + вызов `LevelResolver` (T3)
10. Настроить Addressables для PatternsCollection и LocationTheme (T7)
11. Прогнать интеграционные тесты (T8.3)
12. Проверить запуск игры на мигрированных уровнях

### Этап 4: Level Editor — базовая поддержка нового формата
13. Обновить `LevelDataManager` — методы для нового формата (T4.1)
14. Обновить `LevelTilemapEditor` — режим Templates работает с `PatternsCollection` / `PatternTemplate` (T4.2)
15. Обновить `LevelTilemapEditor` — режим Location Level загружает `LevelInfoRef` + resolve для отображения (T4.2)

### Этап 5: Level Editor — новый UI
16. Реализовать Pattern Sequence Panel (T4.3)
17. Реализовать Sprite Override Panel (T4.4)
18. Интеграция seed в UI (T5.3)

### Этап 6: Финализация
19. Удалить поддержку старого формата из Level Editor (после проверки)
20. Обновить `docs/architecture_knowledge_base.md`
