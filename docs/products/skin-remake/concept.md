# Skin Remake — целевая концепция

## 1. Зафиксированные решения

| Решение | Обоснование |
|---|---|
| Отдельный prefab-визуал на скин | Изолирует SpriteRenderer, controller, clips и art-данные. |
| `SkinSlot` внутри общей transform-иерархии | Visual получает Shift и action trajectory без копирования gameplay-анимаций. |
| Общий независимый collider | Сохраняет текущую физику. Art каждого скина выравнивается по pivot и контрольному контуру default hamster. |
| Transform Animator — источник времени и gameplay events | Skin clip только показывает действие. |
| Смена скина только в меню | В забеге visual не меняется; выбранный скин применяется при следующем spawn. |
| Mapping action -> visual state хранится у скина | Простой скин переиспользует клипы; сложный различает действия. |
| Один Multiple sheet на действие | Проще slicing, FPS, итерации и независимая замена art. |

Риск исходной идеи: prefab нельзя делать новым gameplay-owner. Collider, collision, pause/start listeners, event hub и выбор действия остаются на Hamster. Skin prefab содержит только visual.

## 2. Целевая иерархия и роли

```text
Hamster
└─ shift_transform_animations
   └─ tranform_animations
      ├─ collision_body          # прежние BoxCollider2D + CollisionController
      ├─ skin_slot               # instance выбранного SkinVisual prefab
      └─ effects_slot
```

`collision_body` повторяет текущие size, offset и положение collider базового скина. Допустим небольшой декоративный выход visual за контур, если он не создаёт ложное ожидание столкновения.

| Компонент | Ответственность |
|---|---|
| `SkinVisualHost` | Постоянный компонент Hamster: slot, bind/unbind, кеш сигналов, pause/start/finish. |
| `SkinVisualRuntime` | Владеет instance выбранного prefab и Addressables lease в течение забега. |
| `SpriteAnimatorController` | В момент выбора transform action создаёт семантический context и запускает visual через host. |
| `SkinVisual` | Компонент prefab: Animator, SpriteRenderer, action mapping, visual-only events. |
| Editor validator | Проверяет prefab contract, mappings, clips, pivot/PPU/sorting и Addressable entry. |

Host и router существуют до `Hamster.Awake()`. Mechanics получают стабильную ссылку; загрузка prefab меняет только target host.

## 3. Семантический контракт

Вместо `IsJump` используется context:

```text
SkinActionContext
- Action
- Variant: Normal | Super
- Outcome: Normal | Damage
- Duration
- ContactTime?
- ActionId
```

`Action`:

| Action | Смысл |
|---|---|
| `GroundRun` | Бег по дороге. |
| `RoofRun` | Бег по крыше. |
| `RunFromRoof` | Пассивный сход/падение с крыши. |
| `GroundJump` | Обычный прыжок, jump-over и damage-варианты. |
| `JumpOnObstacle` | Прыжок на разрушаемое препятствие. |
| `JumpOnRoof` | Подъём с дороги на крышу. |
| `RoofJump` | Прыжок между крышами. |
| `JumpFromRoof` | Прыжок с крыши на дорогу. |
| `JumpOnObstacleFromRoof` | Прыжок с крыши на препятствие. |

Mapping поддерживает wildcard по `Variant` и `Outcome`. Несколько context могут ссылаться на один Animator state. Validator обязан доказать, что каждый runtime-context имеет точное правило или fallback.

Правила событий:

- Contact/end приходят только из существующих transform clips.
- Skin AnimationEvents разрешены только для косметики: звук, пыль, частицы.
- Damage feedback — отдельный host-сигнал. Gameplay invulnerability сохраняет текущую длительность `1` с и больше не зависит от `sprite_blink_end` конкретного скина.
- Normal -> Super сохраняет `ActionId`. Если mapping ведёт в тот же clip, visual не перезапускается; обновляются duration/context. Другой clip переключается вместе с super transform.

## 4. Синхронизация длительности

Transform action всегда authoritative. Для one-shot используется единая политика `FitToAction`:

| Policy | Применение | Поведение |
|---|---|---|
| `Loop` | `GroundRun`, `RoofRun` | Clip loop до следующего context. |
| `FitToAction` | Основной one-shot | `visualSpeed = clipLength / actionDuration`; clip заканчивается вместе с transform. |

Скорость меняется на locomotion/action layer через параметр state, не через глобальный `Animator.speed`: damage/cosmetic layers не должны ускоряться.

Visual clip рисуется под длительность соответствующего transform action. `FitToAction` исправляет небольшое расхождение, а не компенсирует неподходящий art. Если один clip переиспользуется действиями с заметно разной длительностью, художник и геймдизайнер принимают ускорение либо добавляют отдельный clip. Удержания последнего кадра нет.

`ContactTime` нужен только сложным visuals. Отдельный gameplay mode может сам подгонять фазы до и после контакта; gameplay contact остаётся точным.

## 5. Каталоги и naming

Использовать существующий lowercase-стиль. Не создавать case-only соседей `Prefabs/Skins` на Windows/Git.

```text
Assets/Content/prefabs/skins/
└─ <skin-slug>/
   └─ <skin-slug>-skin-visual.prefab

Assets/Animations/Hamster/skin_visuals/
└─ <skin-slug>/
   ├─ <skin-slug>-skin-visual.controller
   └─ clips/
      ├─ run.anim
      ├─ jump.anim
      ├─ jump_on.anim
      └─ jump_on_from_roof.anim

Assets/Content/skins/
└─ <skin-slug>/
   └─ sprites/
      ├─ run.png
      ├─ jump.png
      ├─ jump_on.png
      └─ jump_on_from_roof.png
```

- `<skin-slug>`: стабильный lowercase kebab-case, например `neon-runner`.
- Новая область `skin_visuals` не ссылается на legacy `sprite_animations_for_skins`.
- Preview остаётся отдельным лёгким asset; перенос старых preview не нужен до cleanup.
- Slices: `run_00`, `run_01`, ...; одинаковые PPU, pivot, canvas и padding.
- Пустые separator frames не использовать. Прозрачный padding внутри frame допустим для общего canvas и защиты от bilinear bleed.
- Не trim-ить каждый кадр отдельно: меняющийся rect/pivot создаёт jitter.
- Глобальный SpriteAtlas на все скины не создавать: он свяжет их загрузку.

Один большой sheet на скин хуже для итераций и re-slice. Bundle isolation задаётся Addressables packing, не количеством textures. Поэтому выбран один Multiple sheet на действие.

## 6. Addressables и lifetime

- Новая группа `Skin Visuals`: Local, LZ4, IncludeInBuild, **Pack Separately**.
- Explicit Addressable entry — только root prefab. Controller, clips и sheets входят как implicit dependencies.
- Адрес: `skin-visual/<skin-slug>`; JSON хранит адрес, не имя файла/controller.
- Bootstrap грузит каталог и preview. Game loading грузит только выбранный visual.
- Visual привязывается до listener registration и запуска gameplay; сам prefab не содержит `IGameListener`.
- Паттерн: `AddressableLoader.LoadAssetAsync<GameObject>` -> `Object.Instantiate` в `skin_slot` -> lease живёт весь забег -> `Destroy(instance)` -> `Dispose(lease)`.
- Ошибка выбранного visual: загрузить default. Ошибка default: остановить game loading с явной ошибкой.
- Remote catalog сейчас выключен. DLC/удалённая доставка — отдельное продуктовое решение.

`SkinData` мигрирует аддитивно: новый `SkinVisualAddress` рядом со старым `HamsterOverrideController`. Preview, economy, IDs и persistence сохраняются.

## 7. Миграция без поломки

| Этап | Изменение | Условие выхода |
|---|---|---|
| 1. Host | Добавить common `collision_body`, `skin_slot`, host/router и additive catalog field. Legacy visual остаётся рабочим. | Старые три скина без визуальных и gameplay-регрессий. |
| 2. Pilot | Добавить один visual prefab через новый путь. Legacy skins продолжают override path. | Полный action mapping, корректный spawn, pause/start и release. |
| 3. Migration | Перенести default, neon runner, quantum scout в отдельные prefabs. | Visual parity и сохранение старых ID/save. |
| 4. Cutover | Удалить `HelpMethods.ApplyOverrideController`, controller field, override assets и orphan states. | Все skins используют prefab path; legacy content больше не referenced. |

Временное удвоение старых и новых assets допустимо до cutover. Новые prefab не должны ссылаться на legacy clips/sheets.

## 8. Критерии готовности pilot

- Collider совпадает с текущим default и не зависит от visual prefab.
- Все текущие action-context разрешаются mapping без `IsJump` fallback в gameplay.
- Transform events остаются единственным источником contact/end.
- Pilot visual загружается один раз на забег; lease освобождается.
- Pilot имеет независимые visual action assets, хотя временно может использовать общий art.
- Смена в меню влияет только на следующий spawn.
- Старые скины, tutorial ID `2`, quests и saves работают без миграции данных.
