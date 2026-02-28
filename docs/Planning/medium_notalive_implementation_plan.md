# Medium NotAlive Implementation Plan

## Контекст

Добавляем второй размер "неживых" препятствий (машины). Сейчас есть только `bigNotAlive` (452x172). Добавляем `mediumNotAlive` (344x140) — более низкие и узкие машины.

**Спрайты уже импортированы:**
- `obstacle_new_york_car_1.png` — **452x172** (перерисован) → **bigNotAlive**
- `obstacle_new_york_car_2.png` — 344x140 → **mediumNotAlive**
- `obstacle_new_york_car_3.png` — 344x140 → **mediumNotAlive**

**Поведение:** mediumNotAlive идентично bigNotAlive — хомяк запрыгивает на крышу, бежит по ней, может спрыгнуть. Разница только в высоте и ширине.

**Enum-значение:** `mediumNotAlive = 11` (после `decor = 10`)

---

## Нейминг и архитектурные решения

### Имя нового типа: `mediumNotAlive`

Варианты рассматривались:
- ~~`bigNotAlive2`~~ — непонятно, чем отличается
- ~~`smallNotAliveRoof`~~ — путаница со smallNotAlive
- **`mediumNotAlive`** — ясно отражает суть: между big и small, неживой, с крышей

### Префаб: один общий `MediumOrBigNotAlivePrefab`

Текущий `BigNotAlivePrefab` переименовывается в **`MediumOrBigNotAlivePrefab`**.

**Почему НЕ `NotAlivePrefab`:** существует `smallNotAlive` (люки, коробки) — название `NotAlivePrefab` вносит путаницу.

**Почему один префаб:**
- Структура идентична: Root → Transform → Sprite (SpriteRenderer + Animator + BoxCollider2D)
- Коллайдер устанавливается динамически из `sprite.bounds`
- AnimatorOverrideController подставляет клип в рантайме
- Отдельный MediumNotAlivePrefab = лишняя сущность без функциональной разницы

**Что переименовывается:**
- `BigNotAlivePrefab` → `MediumOrBigNotAlivePrefab` (поле в LevelData)
- `BigNotAlivePrefabName` → `MediumOrBigNotAlivePrefabName` (константа в Consts)
- Файл `BigNotAlivePrefab.prefab` → `MediumOrBigNotAlivePrefab.prefab`
- Addressables адрес: `BigNotAlivePrefab` → `MediumOrBigNotAlivePrefab`

---

## Анимации хомяка: генерация medium-клипов

### Подход: прямое редактирование YAML (разовая операция)

Агент генерирует medium-клипы **непосредственно** — правит YAML .anim файлы. Отдельный Editor-скрипт НЕ создаётся, т.к. это одноразовая операция.

### Текущие roof-анимации (для bigNotAlive, высота крыши = 1.55 units)

| Клип | Y-keyframes | Описание |
|------|------------|----------|
| `transform_jump_on_roof` | 0 → 1.667 → **1.55** | Прыжок на крышу |
| `transform_roof_run` | **1.55** | Бег по крыше |
| `transform_roof_jump` | **1.55** → 2.46 → 2.78 → 2.39 → **1.55** | Прыжок на крыше |
| `transform_jump_from_roof` | **1.55** → 2.80 → 1.95 → 0 | Спрыг с крыши |
| `transform_run_from_roof` | **1.55** → 0 | Сбег с крыши |
| `transform_jump_on_from_roof` | **1.55** → 3.20 → 0.80 → 2.20 → 0 | Прыжок на др. объект с крыши |
| `transform_super_jump_on_roof` | 1.273 → 3.12 → 3.13 → **1.55** | Суперпрыжок на крышу |
| `transform_super_roof_jump` | 2.2 → 3.32 → 4.2 → 3.37 → **1.55** | Суперпрыжок на крыше |
| `transform_super_jump_from_roof` | 2.2 → 4.29 → 2.29 → 0.01 | Суперспрыг с крыши |
| `transform_super_jump_on_obstacle_from_roof` | 2.82 → 4.39 → 3.89 → 1.07 → 1.84 → 1.53 → 0 | Суперпрыжок на объект с крыши |

### Расчёт высоты medium крыши

- big sprite: 172px / 100 PPU = **1.72 units**, roof Y в анимации = **1.55**
- medium sprite: 140px / 100 PPU = **1.40 units**
- Коэффициент масштабирования: **140 / 172 = 0.81395**
- Medium roof Y = 1.55 × 0.81395 ≈ **1.2616 units**

### Стратегия: пропорциональное масштабирование Y

Все Y-значения (keyframe values + inSlope/outSlope для Y) умножаются на коэффициент `140/172`:
- `newY = oldY × (140 / 172)`
- Это сохраняет форму кривых (арки прыжков, плавность) и соотношения

Пример для `transform_jump_on_roof`:
- 0 → 0, 1.667 → 1.357, 1.55 → 1.262

### Runtime: подмена клипов через AnimatorOverrideController

Новые medium-состояния в Animator Controller **НЕ нужны**. Используем тот же подход, что и для obstacle-анимаций:

1. Загрузить оба набора клипов при старте уровня
2. Перед триггером roof-анимации — `AnimatorOverrideController` подменяет big-клип на medium-клип (если obstacle = mediumNotAlive)
3. Animator Controller хомяка остаётся без изменений

Это требует:
- Хранения medium-клипов (загрузка по label или прямой path)
- Метода `SwapRoofClips(bool isMedium)` в `TransformAnimatorController`

---

## Test Level Launcher

### Цель
Пункт меню `Tools → Test Level → Launch` для мгновенного запуска игры на тестовом уровне.

### Как работает запуск уровня в игре
1. `LevelController.SetCurrentLevel(levelName)` → записывает в `GameDataManager.PlayerData.CurrentLevel`
2. `SceneManager.LoadScene("Game")` → загружает игровую сцену
3. `LevelDataProvider.LoadLevelData()` читает `CurrentLevel` и грузит JSON через Addressables

### Тестовый уровень
Создаётся `Assets/Content/locations/99_Test_Level/` с полной структурой. JSON содержит паттерны для тестирования:
- bigNotAlive (car_1) на обеих линиях
- mediumNotAlive (car_2, car_3) на обеих линиях
- smallNotAliveRoadAndRoof на крышах big и medium
- Переход big→medium и medium→big на одной линии
- Различные комбинации для всех видов прыжков

---

## Пошаговый план реализации

### Фаза 1: Новый тип и константы
| # | Задача | Кто |
|---|--------|-----|
| 1.1 | Добавить `mediumNotAlive` в `ObstacleTypeEnum` (значение 11) | Агент |
| 1.2 | Добавить `MEDIUM_NOTALIVE_WIDTH = 344`, `MEDIUM_NOTALIVE_HEIGHT = 140` в `Consts.cs` | Агент |
| 1.3 | Добавить производные `MEDIUM_NOTALIVE_WIDTH_UNITS` / `HEIGHT_UNITS` | Агент |

### Фаза 2: Переименование префаба
| # | Задача | Кто |
|---|--------|-----|
| 2.1 | Переименовать файл `BigNotAlivePrefab.prefab` → `MediumOrBigNotAlivePrefab.prefab` | Агент (mv + edit prefab YAML) |
| 2.2 | Обновить Addressables YAML (m_Address) | Агент (edit obstacles prefabs.asset) |
| 2.3 | Обновить все ссылки в коде: `Consts`, `LevelData`, `LevelDataProvider`, `ObstacleFactory`, `ObstacleAnimationPreviewer`, `copilot-instructions.md` | Агент |

### Фаза 3: ObstacleFactory и валидация
| # | Задача | Кто |
|---|--------|-----|
| 3.1 | `GetPrefab()` — добавить `mediumNotAlive` → тот же `MediumOrBigNotAlivePrefab` | Агент |
| 3.2 | `GetRendererSpriteByModelTypeAndName()` — добавить `mediumNotAlive` в список obstacle-типов | Агент |
| 3.3 | `ValidateAnimationSprite()` — добавить case mediumNotAlive (344x140) | Агент |
| 3.4 | `LevelDataValidator.ValidateObstacleSprite()` — добавить case mediumNotAlive | Агент |
| 3.5 | `ObstacleSpritePostprocessor` — добавить MEDIUM_NOTALIVE размеры | Агент |

### Фаза 4: Коллизии и механики
| # | Задача | Кто |
|---|--------|-----|
| 4.1 | Добавить хелпер `IsRoofObstacle(ObstacleTypeEnum)` → true для big/medium NotAlive | Агент |
| 4.2 | `JumpMechanics.HandleObstacle()` — добавить case `mediumNotAlive` → `HandleBigNotAlive` | Агент |
| 4.3 | `RoofJumpMechanics._handlers` — добавить `mediumNotAlive` → `HandleBigNotAlive` | Агент |
| 4.4 | `SuperJumpMechanics._handlers` — добавить `mediumNotAlive` → `HandleBigNotAlive` | Агент |
| 4.5 | `SuperRoofJumpMechanics._handlers` — добавить `mediumNotAlive` → `HandleBigNotAlive` | Агент |
| 4.6 | `CollisionController.CanDamageOnRoofRun()` — mediumNotAlive тоже блокирует урон | Агент |
| 4.7 | `CollisionUtils.TryFindBigNotAliveUnderSmallNotAlive()` — искать и medium | Агент |
| 4.8 | `CollisionUtils.IsHitSmallNotAliveOnRoof()` — использовать `IsRoofObstacle()` | Агент |
| 4.9 | `HelpMethods.FindBigNotAliveUnderHamster()` — искать и medium, использовать `obstacle.ColliderWidth` вместо хардкода | Агент |
| 4.10 | `RoofRunMechanics.FindNextBigNotAliveOnSameLine()` — добавить medium | Агент |
| 4.11 | `RoofRunMechanics.CheckRoofShifted()` — обновлено через 4.9 | Агент |

### Фаза 5: Анимации хомяка для medium roof
| # | Задача | Кто |
|---|--------|-----|
| 5.1 | Сгенерировать 10 medium roof клипов (прямое редактирование YAML, коэффициент 140/172) | Агент |
| 5.2 | Добавить метод `SwapRoofClips(bool isMedium)` в `TransformAnimatorController` | Агент |
| 5.3 | Обновить mechanics — вызывать `SwapRoofClips` перед триггером roof-анимаций | Агент |
| 5.4 | Зарегистрировать medium-клипы в Addressables (или загружать по direct path) | Агент + проверка |

### Фаза 6: Данные, маппинги и тестовый уровень
| # | Задача | Кто |
|---|--------|-----|
| 6.1 | Обновить `obstacle_sprite_to_type_mappings.json` для 01_New_York (car_1→bigNotAlive, car_2/3→mediumNotAlive) | Агент |
| 6.2 | Обновить `level_design_templates` маппинги (добавить mediumNotAlive спрайты) | Агент |
| 6.3 | Создать `99_Test_Level/` со структурой и тестовым JSON | Агент |
| 6.4 | Создать `TestLevelLauncher.cs` (Tools → Test Level → Launch) | Агент |
| 6.5 | Зарегистрировать тестовый уровень и car-спрайты в Addressables | Ручная работа |

### Фаза 7: Финальное тестирование
| # | Задача | Кто |
|---|--------|-----|
| 7.1 | Запуск тестового уровня, проверка всех комбинаций прыжков | Ручная работа |
| 7.2 | Коммит и пуш | Агент |

---

## Ручная работа (минимум)

Единственная задача, которую агент **не может** выполнить:

| # | Задача | Почему ручная |
|---|--------|------|
| 6.5 | Зарегистрировать тестовый JSON и car-спрайты в Addressables | Addressables GUI в Unity Editor; программное добавление через AddressableAssetSettings API ненадёжно без запуска Unity |

Всё остальное (в т.ч. переименование префаба, генерация анимаций, тестовый уровень) агент делает сам.

---

## Важные заметки

### Хардкоды, которые нужно обобщить
- `HelpMethods.FindBigNotAliveUnderHamster()` — хардкод `BIG_NOTALIVE_WIDTH_UNITS` → использовать `obstacle.ColliderWidth`
- `CollisionUtils.TryFindBigNotAliveUnderSmallNotAlive()` — фильтр `== bigNotAlive` → использовать `IsRoofObstacle()`
- `RoofRunMechanics.FindNextBigNotAliveOnSameLine()` — аналогично

### ETC2 компрессия
- 452 ÷ 4 = 113 ✅ (car_1 перерисован на 452x172)
- 172 ÷ 4 = 43 ✅
- 344 ÷ 4 = 86 ✅
- 140 ÷ 4 = 35 ✅

---

## Статус

- [ ] Фаза 1: Константы и enum
- [ ] Фаза 2: Переименование префаба
- [ ] Фаза 3: Фабрика и валидация
- [ ] Фаза 4: Коллизии и механики
- [ ] Фаза 5: Анимации хомяка
- [ ] Фаза 6: Данные, маппинги и тестовый уровень
- [ ] Фаза 7: Тестирование
