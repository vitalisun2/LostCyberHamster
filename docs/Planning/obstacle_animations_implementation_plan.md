# Obstacle Animations Implementation Plan (final)

Короткая инструкция, как добавлять такие же анимации для любых препятствий, исходя из реализованной архитектуры.

## Что под капотом (важно понимать)
- Используем единый `ObstacleAnimatorController` с пустым состоянием `Play`.
- В рантайме для каждого препятствия подменяем клип через `AnimatorOverrideController`.
- Animation Clips подгружаются пачкой через Addressables по label: `<Location> obstacle animations` (учитывается регистр; напр.: `New York obstacle animations`).
- Поиск нужного клипа идёт по convention: `{spriteName}_{animationType}` — сначала ищется `walk`, потом `idle`.
- Для статичных препятствий остаётся прежняя логика спрайтов.
- Валидация: для статичных спрайтов проверяется размер текстуры; для анимаций — каждый кадр проверяется по `sprite.rect` (кадр внутри спрайт-листа) и по типу препятствия.

## Готовим ассеты (на каждый animated obstacle)
1) Sprite sheet (PNG)
- Кадры в один ряд; размеры кадра:
  - SmallAlive: 150×108 px
  - BigAlive: 100×210 px
- Pixels Per Unit: 100

2) Импорт в Unity
- Texture Type: Sprite (2D and UI)
- Sprite Mode: Multiple
- Slice: Grid By Cell Size (под соответствующий размер кадра)
- Pivot всем кадрам: Bottom Center (наш проектный стандарт)

3) Animation Clip
- Создай в `Assets/Animations/Obstacles/`
- Имя по convention: 
  - `{spriteName}_walk.anim` для движущихся препятствий (пример: `obstacle_new_york_granny_walk`)
  - `{spriteName}_idle.anim` для статичных анимированных препятствий (пример: `obstacle_new_york_dog_idle`)
- Включи Loop Time; FPS ~ 8–12 или расставь тайминги кадрам вручную

4) Addressables (обязательно)
- Включи Addressable у клипа
- Address: то же, что имя клипа (удобно)
- Label: `<Location> obstacle animations` — с верным регистром, напр.: `New York obstacle animations`
- Имя группы Addressables не важно, важен label

На этом всё: код и префабы уже готовы — ничего менять не нужно.

## Что делает код за тебя
- На старте уровня `LevelDataProvider` загружает все `AnimationClip` по label и сохраняет в `LevelData`.
- `ObstacleFactory` при создании препятствия ищет клип сначала по имени `{spriteName}_walk`, затем по `{spriteName}_idle` и, если нашёл, подставляет его в `Animator` через `AnimatorOverrideController`.
- Если клипа нет — используется статичный спрайт, Animator отключается.
- Для анимаций мы сразу выставляем первый кадр (в Editor) и валидируем каждый кадр по размеру (через `sprite.rect`).

## Чек-лист на новый animated obstacle
- [ ] PNG со спрайт-листом готов; размер кадра соответствует типу (SmallAlive/BigAlive)
- [ ] Импортирован как Sprite (Multiple), нарезан Grid By Cell Size
- [ ] У всех кадров Pivot = Bottom Center
- [ ] Создан клип `{spriteName}_walk` (движущееся) или `{spriteName}_idle` (статичное) с Loop
- [ ] Клип помечен Addressable; Label = `<Location> obstacle animations` (регистр важен)
- [ ] Имя клипа совпадает с convention (для автопоиска)
- [ ] Если используется `_walk` — препятствие автоматически получит дополнительное движение влево

## Проверка и типичные ошибки
- Клип не подхватился: проверь label (регистр!), адрес клипа и имя клипа по convention.
- Спрайт прозрачный на первом кадре: убедись, что кадры есть в клипе; в Editor первый кадр подставляем автоматически.
- Плывёт коллайдер: проверь, что у всех кадров одинаковый Pivot (Bottom Center) и размеры кадра корректны.

## Расширение (на будущее)
- Поддержка `{spriteName}_walk` для движущихся препятствий добавляется тем же способом; код уже готов принимать другой `animationType`.

---

## Движение препятствий относительно дороги (ObstacleMoveMechanics)

### Концепция двух слоёв движения

**Sprite Animation Layer (визуал):**
- `walk` / `idle` анимации — цикличное воспроизведение кадров спрайтов
- Управляется через Animator + AnimatorOverrideController
- Не влияет на физическое положение объекта

**Transform Movement Layer (физика/позиция):**
- `ScrollLeftMechanics` — базовый скролл дороги (применяется ко всем препятствиям, движение влево)
- `ObstacleMoveMechanics` — дополнительное движение **в ту же сторону** (влево), автоматически для препятствий с `walk` анимацией

### Автоматическая активация

`ObstacleMoveMechanics` создаётся **автоматически**, если у препятствия найдена анимация `{spriteName}_walk`:
- Есть `walk` анимация → добавляется дополнительное движение влево (имитация ходьбы)
- Есть только `idle` / статичный спрайт → только базовый скролл дороги

**Скорость движения:**
- Определяется константой внутри класса `ObstacleMoveMechanics` (например, 0.5 units/sec)
- **Не настраивается через JSON** — фиксированная для всех движущихся препятствий
- Направление: **всегда влево** (в ту же сторону, что базовый скролл)

### Как работает код

1. В `ObstacleFactory.CreateObstacle()`:
   - Вызывается `TrySetupAnimation()` → возвращает тип найденной анимации (`walk` / `idle` / `null`)
   - Если найдена `walk` анимация → флаг `hasWalkAnimation = true`

2. В `Obstacle.InitializeMechanics()`:
   - Всегда создаётся `ScrollLeftMechanics` (базовый скролл)
   - Если `hasWalkAnimation == true` — создаётся `ObstacleMoveMechanics` с константной скоростью

3. В `Obstacle.OnUpdate()`:
   - Вызывается `_scrollLeftMechanics.Update()` (базовый скролл)
   - Если есть — вызывается `_obstacleMoveMechanics?.Update()` (доп. движение)

### Пример: Бабка с анимацией ходьбы

**JSON конфигурация (без изменений):**
```json
{
  "type": 3,
  "spriteName": "obstacle_new_york_granny",
  "x": 12.0,
  "y": 1.0
}
```

**Ассеты:**
- `obstacle_new_york_granny_walk.anim` (3 кадра, Loop, 8-12 FPS)
- Спрайт-лист: 3 кадра × 100×210 px (BigAlive)
- Addressables label: `New York obstacle animations`

**Результат:**
1. ObstacleFactory находит анимацию `obstacle_new_york_granny_walk`
2. Применяет её к Animator
3. Автоматически создаёт `ObstacleMoveMechanics` (т.к. это `walk`)
4. Бабка проигрывает анимацию ходьбы + физически движется влево чуть быстрее остальных препятствий

---

## Размеры (из `Consts.cs`)

Все размеры приведены к ETC2-совместимым (кратны 4):

```csharp
// BigAlive (granny/hipster)
public const int BIG_ALIVE_WIDTH = 100;
public const int BIG_ALIVE_HEIGHT = 212;

// SmallAlive (dog/homeless)
public const int SMALL_ALIVE_WIDTH = 152;
public const int SMALL_ALIVE_HEIGHT = 108;

// BigNotAlive (machines/vehicles)
public const int BIG_NOTALIVE_WIDTH = 452;
public const int BIG_NOTALIVE_HEIGHT = 172;

// SmallNotAlive (cones/hydrants)
public const int SMALL_NOTALIVE_WIDTH = 140;
public const int SMALL_NOTALIVE_HEIGHT = 108;

// Collectables/Bonuses
public const int BONUS_WIDTH = 80;
public const int BONUS_HEIGHT = 80;

// Background/Road
public const int BACKGROUND_WIDTH = 2000;
public const int BACKGROUND_HEIGHT = 240;

public const float PixelsPerUnit = 100f;
```

Этого плана достаточно, чтобы за 3–5 минут подготовить такую же анимацию для любого нового препятствия без правок кода.

---

## Необходимые доработки

### Интеграция с механикой прыжка хомяка

**Проблема:**
В текущей реализации механика прыжка хомяка рассчитывает траекторию и момент приземления, учитывая только базовое смещение препятствий вместе с миром (через `ScrollLeftMechanics`). 

Однако для анимированных препятствий с типом `walk` (например, бабушка) добавляется дополнительное движение влево через `ObstacleMoveMechanics` с константной скоростью (0.5 units/sec).

**Требуется:**
- Обновить расчёт траектории прыжка так, чтобы учитывалось **суммарное смещение** препятствия:
  - Базовое смещение (скорость мира)
  - Дополнительное смещение для `walk`-препятствий (константа из `ObstacleMoveMechanics`)
- При определении точки приземления нужно прогнозировать позицию препятствия с учётом обеих скоростей
- Возможно, потребуется передавать тип анимации (`AnimationType.Walk` / `Idle` / `None`) в систему коллизий или прыжков

**Текущее состояние:**
- Код анимации и движения реализован
- Механика прыжка работает корректно для статичных препятствий и препятствий с `idle` анимацией
- Для препятствий с `walk` анимацией расчёт траектории прыжка может быть неточным

**Приоритет:** Средний (влияет на точность геймплея при прыжках через движущиеся препятствия)

**Связанные файлы:**
- `Assets/Scripts/GameEngine/Mechanics/ObstacleMoveMechanics.cs` — константа `AdditionalSpeed`
- Файлы механики прыжка (требуется определить конкретные классы)
- `Assets/Scripts/Gameplay/Obstacle.cs` — свойство `AnimationType`
