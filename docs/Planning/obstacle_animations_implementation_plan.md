# Obstacle Animations Implementation Plan (final)

Короткая инструкция, как добавлять такие же анимации для любых препятствий, исходя из реализованной архитектуры.

## Что под капотом (важно понимать)
- Используем единый `ObstacleAnimatorController` с пустым состоянием `Play`.
- В рантайме для каждого препятствия подменяем клип через `AnimatorOverrideController`.
- Animation Clips подгружаются пачкой через Addressables по label: `<Location> obstacle animations` (учитывается регистр; напр.: `New York obstacle animations`).
- Поиск нужного клипа идёт по convention: `{spriteName}_{animationType}` (сейчас используем `idle`).
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
- Имя по convention: `{spriteName}_idle.anim` (пример: `obstacle_new_york_granny_idle`)
- Включи Loop Time; FPS ~ 8–12 или расставь тайминги кадрам вручную

4) Addressables (обязательно)
- Включи Addressable у клипа
- Address: то же, что имя клипа (удобно)
- Label: `<Location> obstacle animations` — с верным регистром, напр.: `New York obstacle animations`
- Имя группы Addressables не важно, важен label

На этом всё: код и префабы уже готовы — ничего менять не нужно.

## Что делает код за тебя
- На старте уровня `LevelDataProvider` загружает все `AnimationClip` по label и сохраняет в `LevelData`.
- `ObstacleFactory` при создании препятствия ищет клип по имени `{spriteName}_idle` и, если нашёл, подставляет его в `Animator` через `AnimatorOverrideController`.
- Если клипа нет — используется статичный спрайт, Animator отключается.
- Для анимаций мы сразу выставляем первый кадр (в Editor) и валидируем каждый кадр по размеру (через `sprite.rect`).

## Чек-лист на новый animated obstacle
- [ ] PNG со спрайт-листом готов; размер кадра соответствует типу (SmallAlive/BigAlive)
- [ ] Импортирован как Sprite (Multiple), нарезан Grid By Cell Size
- [ ] У всех кадров Pivot = Bottom Center
- [ ] Создан клип `{spriteName}_idle` с Loop
- [ ] Клип помечен Addressable; Label = `<Location> obstacle animations` (регистр важен)
- [ ] Имя клипа совпадает с convention (для автопоиска)

## Проверка и типичные ошибки
- Клип не подхватился: проверь label (регистр!), адрес клипа и имя клипа по convention.
- Спрайт прозрачный на первом кадре: убедись, что кадры есть в клипе; в Editor первый кадр подставляем автоматически.
- Плывёт коллайдер: проверь, что у всех кадров одинаковый Pivot (Bottom Center) и размеры кадра корректны.

## Расширение (на будущее)
- Поддержка `{spriteName}_walk` для движущихся препятствий добавляется тем же способом; код уже готов принимать другой `animationType`.

## Размеры (из `Consts.cs`)
```csharp
// BigAlive (granny/hipster)
public const int BIG_ALIVE_WIDTH = 100;
public const int BIG_ALIVE_HEIGHT = 210;

// SmallAlive (dog/homeless)
public const int SMALL_ALIVE_WIDTH = 150;
public const int SMALL_ALIVE_HEIGHT = 108;

public const float PixelsPerUnit = 100f;
```

Этого плана достаточно, чтобы за 3–5 минут подготовить такую же анимацию для любого нового препятствия без правок кода.
