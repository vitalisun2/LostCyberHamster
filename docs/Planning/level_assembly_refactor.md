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
- [x] **Seed-based randomness:** Отложить. Реализовать после базовой системы как отдельную фичу.
- [x] **Оверрайд позиции:** Не нужен, избыточно. Override только для замены спрайта.
- [x] **Multiple pattern instances:** Работает автоматически — overrides привязаны к конкретному элементу массива `patternSequence` (по позиции вхождения), а не к имени паттерна глобально. Два вхождения `easy_run` имеют независимые overrides.
- [x] **Совместимость Level Editor:** Переработка Level Editor целиком под новый формат. Старый формат совместим со старым кодом, новый Level Editor работает только с новым форматом. Скрипт миграции конвертирует существующие уровни.
