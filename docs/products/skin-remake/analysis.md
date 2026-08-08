# Skin Remake — текущее состояние

## 1. Система скинов

| Блок | Реализация | Факт |
|---|---|---|
| Каталог | `Assets/Content/skins/skins.json` | Три скина: `0` default, `2` neon runner, `1` quantum scout. |
| Bootstrap | `InitSkinsLoadingTask` -> `SkinManager.Init()` | Загружает каталог, все preview-спрайты и все `RuntimeAnimatorController`. |
| Фабрика | `SkinFactory` | Разрешены только ID `{0,1,2}`. Новый ID без правки кода вызывает ошибку. |
| Сохранение | `PlayerData.AppliedSkinId`, `PurchasedSkinIds` | Default: выбран и куплен ID `0`. |
| Меню | `CharacterScreenController` | `Skin.HamsterSprite` используется только как preview. Покупка и экипировка — разные нажатия. |
| Экипировка | `SkinManager.PutOnSkin()` | Сохраняет ID и сразу меняет controller активного hamster через `HelpMethods.ApplyOverrideController()`. |

`TutorialSkinLessonController` жёстко использует ID `2`. Порядок записей `0,2,1` делает его следующим скином после default.

## 2. Текущие visual-ассеты

- `Assets/Animations/Hamster/sprite_animations_for_skins/DefaultSkinAnimator.controller` — общий граф с `IsJump` и `IsBlink`.
- `ElectricStrikeSkinAnimator.overrideController` и `EnergyShieldSkinAnimator.overrideController` заменяют только `default_run` и `default_jump`.
- Gameplay-кадры лежат в `Assets/Content/shared/sprites/hamster_*.png`. Это Multiple-sprite sheets по два slice.
- `Assets/Content/skins/skin_*.png` — отдельные картинки меню.
- `default_blink.anim` меняет alpha и вызывает `sprite_blink_end`. Это событие сбрасывает gameplay-флаг `IsDamaged`.
- В `DefaultSkinAnimator` есть orphan states с skin-клипами. Активный граф на них не переходит.

Visual timing уже не совпадает с gameplay: transform jump длится до `2.5` с, а electric strike jump — около `0.183` с. Gameplay завершает transform-событие, не sprite-клип.

## 3. Hamster prefab и создание в игре

Текущая иерархия:

```text
Hamster
└─ shift_transform_animations
   └─ tranform_animations        # фактический typo в prefab
      ├─ sprite_animations
      │  ├─ SpriteRenderer
      │  ├─ Animator + SpriteAnimatorController
      │  ├─ SpriteAnimatorEventsDispatcher
      │  ├─ BoxCollider2D
      │  └─ CollisionController
      └─ effects_slot
```

`Game.unity` хранит прямую ссылку на `Assets/Content/prefabs/Hamster.prefab`. Hamster не Addressable; старый `assetBundleName: prefabs` в `.meta` текущим spawn-путём не используется.

| Порядок | Runtime |
|---:|---|
| 1 | `GameSceneInstaller` биндует prefab через Zenject. |
| 2 | `GameEntryPoint` передаёт его в loading bundle как `characterPrefab`. |
| 3 | `InitCharacterLoadingTask` вызывает обычный `Instantiate` под `EnvironmentRoot`. |
| 4 | Активный prefab сразу выполняет `Hamster.Awake()` и `OnEnable()`. |
| 5 | `Awake()` кеширует collider bounds, Transform/Sprite controllers и dispatchers; mechanics получают эти ссылки. |
| 6 | Загружается runtime суперудара. |
| 7 | `AddGameListeners()` один раз сканирует дочерние `IGameListener`. |
| 8 | Применяется skin override controller; затем задаётся `LevelData.Hamster`. |

Поздно созданный visual не попадёт в ссылки mechanics и snapshot listeners. Постоянный hamster-owned facade нужен даже при установке visual внутри `InitCharacterLoadingTask`.

Pipeline может войти в `INTRO` до spawn hamster. Новый host обязан синхронизироваться с текущим `GameState`, а не ждать уже прошедший `OnIntro`.

## 4. Transform и sprite-сигналы

Transform Animator задаёт траекторию, время и gameplay-события. Все активные jump-механики при этом вызывают один `SpriteAnimatorController.Jump()` -> `IsJump`.

| Смысл действия | Transform clip | Normal / Super, с | Главные события |
|---|---|---:|---|
| Ground jump | `transform_jump` / `transform_super_jump` | `1.0 / 1.2` | mid, end |
| Jump on obstacle | `transform_jump_on` / `transform_super_jump_on` | `1.8167 / 2.1167` | contact, end |
| Jump on roof | `transform_jump_on_roof` / `transform_super_jump_on_roof` | `1.0 / 1.2` | roof end |
| Roof jump | `transform_roof_jump` / `transform_super_roof_jump` | `1.0 / 1.2` | roof end |
| Jump from roof | `transform_jump_from_roof` / `transform_super_jump_from_roof` | `1.3333 / 1.3` | from-roof end |
| Passive run from roof | `transform_run_from_roof` | `0.5` | collision check, end |
| Jump on obstacle from roof | `transform_jump_on_from_roof` / `transform_super_jump_on_obstacle_from_roof` | `2.5 / 2.35` | contact, end |

Дополнительные зависимости:

- Medium-roof варианты сохраняют те же длительности и события.
- Normal jump может обновиться до Super в окне `0.3` с. Transform запускает новый super clip; текущий Sprite Animator различие не видит.
- `TransformAnimatorEventsDispatcher` завершает state, damage и obstacle contact. Эти события нельзя переносить в skin clips.
- `sprite_blink_end` сейчас влияет на gameplay invulnerability. Это единственная критичная gameplay-зависимость от visual clip.
- `ShiftTransformAnimatorController` отдельно меняет линию за `0.45` с. `SkinSlot` должен наследовать Shift и Transform.

## 5. Addressables

| Факт | Текущее состояние |
|---|---|
| Версия | Addressables `2.7.2`. |
| Доставка | Remote catalog выключен; Local Build/Load. |
| Skin groups | `Skins`, `skins override controllers`, `skins ulta prefabs`. |
| Packing | LZ4, Pack Together, cache/CRC, IncludeInBuild. |
| Dependencies | Clips и gameplay sheets не explicit entries; входят через controllers. |
| Lifetime | Skin handles теряются; `Addressables.Release` отсутствует. Повторный `SkinManager.Init()` не очищает список. |

В проекте уже есть безопасный паттерн: `AddressableLoader` + `AddressableLease<T>`. `SuperAttackFactory` держит lease весь забег и освобождает при уничтожении hamster.

Группа `skins ulta prefabs` содержит эффекты суперударов. SkinVisual prefab туда добавлять нельзя.

## 6. Ограничения перехода

1. `BoxCollider2D` и `CollisionController` сейчас лежат на visual-узле, но являются общей gameplay-частью.
2. Collider bounds кешируются в `Hamster.Awake()`. Сменный visual не должен задавать физическую геометрию.
3. `SpriteAnimatorController` и dispatcher кешируются до async-загрузки visual.
4. `AddGameListeners()` не увидит listener внутри поздно загруженного prefab.
5. Скин не может определять длительность действия или момент gameplay contact/end.
6. Старые ID, tutorial, quest events, preview и persistence должны сохраниться при миграции.
