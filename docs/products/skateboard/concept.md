# Суперудар «Скейтборд»

## Решение

Skateboard — отдельный gameplay mode. Не skin.

`Hamster` остаётся стабильным root. Общий shift-root двигает обе actor-ветки между двумя линиями. `HamsterActorSwitcher` на root включает целиком `normal_actor` или `skateboard_actor`. Никакого `actor_slot` и точечного mix компонентов.

```text
Hamster                                      active
  Hamster
  HamsterActorSwitcher
└─ shift_transform_animations                общий lane shift
   ├─ normal_actor                           active default
   │  └─ transform_animations                обычный vertical Transform Animator
   │     ├─ collision_body
   │     ├─ skin_slot
   │     └─ effects_slot
   └─ skateboard_actor                       inactive default
      └─ surface_transform                    road/roof alignment
         ├─ visual_collision_root             scale 0.7
         │  ├─ collision_body
         │  │  ├─ PolygonCollider2D
         │  │  ├─ CollisionController
         │  │  └─ SpritePhysicsShapeColliderSync
         │  └─ skin_slot                     SkinVisualHost
         └─ gameplay_collision_sensor         stable skateboard trigger
```

Skateboard actor не меняет Transform по Y. Прыжок нарисован sprite animation. Новый skateboard `effects_slot` пока не нужен: визуальные эффекты живут в его animations.

## Игровые правила

- Ride: hamster уязвим; collision обрабатывается как обычный run damage.
- Shift между двумя линиями работает всегда.
- Обычный vertical Transform jump отключён.
- Skateboard jump и landing: hamster неуязвим. Outcome зависит от immutable `StartedOnRoof` текущего cycle.
- До первого jump действует timeout `10 s` gameplay time.
- Первый jump отменяет timeout навсегда.
- Всего ровно `3` jumps: `1+1+1`, `2+1`, `1+2`, `3`.
- Mode заканчивается после landing tail третьего израсходованного jump; impact срабатывает в contact frame.
- Активация разрешена из stable `Run` и `RoofRun`.
- Ride: current/проходимая roof-chain — support. Roof side после gap — damage. Roof box — damage.
- StartedOnRoad jump: любой physical obstacle уничтожается, roof тоже.
- StartedOnRoof jump: любой contact с `bigNotAlive/mediumNotAlive` сохраняется как potential support; прочий physical уничтожается.
- StartedOnRoof landing: top contact принимает roof. Side/inside miss конкретной roof уничтожает её. Без support общий root плавно опускается на road.
- Collectable всегда pickup при contact. Decor игнорируется.

Один jump-cycle тратит один из трёх jumps. В старте создаётся immutable `StartedOnRoof`. Double input до contact усиливает cycle без второго списания. Все 5 animations идут `1.5x`; contact `0.556 s`, полный cycle `0.833 s`. Visual и FSM используют один multiplier. Следующий cycle буферизуется только в landing tail.

## Combo и landing impact

Combo растёт только у последовательных jumps без возврата в ride-chain:

| Landing | Normal jump | Super jump | Shake |
|---|---|---|---|
| 1 / single | `1 hamster width` | `2 hamster widths` | x1 |
| 2 подряд | `3 hamster widths` | `6 hamster widths` | x2 |
| 3 подряд | весь visible screen | весь visible screen | x3 |

Obstacle делает короткую дугу за `5 frames @ 60 FPS`; через ещё `3 frames` — destroy. Высота: combo 1 `13%..9.1%`, combo 2 `19.5%..10.725%`, combo 3 `27.3%..12.285%` от высоты obstacle. Combo 3 сильнее combo 2 на любой равной normalized distance. Ближние targets стартуют раньше и прыгают выше. Три wave-группы: combo 1 `0 / 0.04 / 0.08 s`, combo 2 `0 / 0.08 / 0.16 s`, combo 3 `0 / 0.13 / 0.26 s`; каждый target получает jitter `0..12 ms` внутри своей группы. Camera shake: `0.18 s`, базовая амплитуда `0.08 units`, множитель combo `1x/2x/3x`.

Destroy идёт через существующий super-attack channel/pool unspawn. Wave StartedOnRoad уничтожает все physical, roof тоже. Wave StartedOnRoof: roof bump-only/live, roof box и прочий physical destroy. Current support preserve без bump. Collectable bump-only, восстанавливает позицию, остаётся pickup. Decor ignore. Combo 3 сохраняет правый scroll buffer; natural exit не отменяет pending wave. Pool identity фиксируется в target snapshot.

## State flow

```text
Inactive
  -> Riding + 10s timer
  -> Jumping / SuperJumping
  -> LandingImpact
  -> Riding, если jumpsLeft > 0
  -> Exit, если jumpsLeft == 0

Riding timeout до первого jump -> Exit
Damage during Riding -> обычная потеря life/damage immunity -> немедленный Exit
Level finish / dispose -> cleanup -> Normal actor
```

Независимо от jump phase работает surface state:

```text
Road
Roof
DroppingToRoad
```

Jump остаётся sprite-only. `surface_transform` меняет Y только при roof alignment/спуске и двигает visual с collider вместе.

## Visual assets

Обязательные skateboard animations:

- ride/balance loop A;
- ride/balance loop B;
- foot push loop;
- normal jump + board flip;
- higher double super-jump + stronger flip.

Animator живёт на загружаемом visual prefab внутри skateboard `skin_slot`, как в normal mode. Actor root Animator для sprite animation не нужен.

Каждый character skin может иметь две visual-версии: normal и skateboard. Одинаковые slugs; skateboard fallback — `default`.

```text
Assets/Animations/Hamster/
  ShiftTransformAnimator.controller
  normal_mode/
    TransformAnimator.controller
    transform_*.anim
    skin_visuals/<slug>/...
  skateboard_mode/
    skin_visuals/<slug>/<slug>-skin-visual.controller

Assets/Content/prefabs/skins/
  normal_mode/<slug>/<slug>-skin-visual.prefab
  skateboard_mode/<slug>/<slug>-skin-visual.prefab

Assets/Content/skins/
  normal_mode/<slug>/*.png
  skateboard_mode/<slug>/*.png
```

`skins.json`, portraits и shared effects остаются общими, вне mode-folders.

## Sprite collider

Каждый skateboard sprite frame получает ручной простой `Custom Physics Shape`, примерно `6–12` points. Единые pivot, PPU, scale.

Не вычислять contour каждый frame. При visual load один раз прочитать physics shapes всех sprites и кешировать paths по ссылке на `Sprite`. На frame change только вызвать `PolygonCollider2D.SetPath()` готовыми paths. Так collider точно следует нарисованному jump; повторного geometry analysis и allocations нет.

`SpritePhysicsShapeColliderSync` живёт на `collision_body`. Текущий `SpriteRenderer` приходит из visual prefab/host при runtime bind. До появления sprites collider может иметь только placeholder shape.

`visual_collision_root` масштабирует visual и animated polygon до `0.7`. `gameplay_collision_sensor` задаёт canonical board baseline `-0.425`. Surface alignment двигает общий `surface_transform`; sprite отдельно не двигается.

`gameplay_collision_sensor` — trigger `1.64 x 0.85`: canonical baseline и stable X-footprint для roof-chain. Animated PolygonCollider нужен для точного collision и top-vs-side landing. Оба routes идут через одну policy.

## Ownership

`Hamster`

- общие state/events/lives/energy/mechanics;
- serialized refs: switcher, surface, normal host, skateboard host;
- ownership super attack и visual leases.

`HamsterActorSwitcher`

- refs на два actor;
- active/inactive switch;
- только active actor/collider branch, не gameplay authority;
- без timer/jump/combo/damage/loading.

`SkateboardAttack : ISuperAttackRuntime`

- mode lease/lifecycle;
- timeout, 3 jumps, combo FSM, immutable jump snapshot;
- немедленный exit после принятого ride damage;
- gates normal jump/roof mechanics;
- cleanup на exit/finish/dispose.

`SkateboardSurfaceController`

- хранит `Road / Roof / DroppingToRoad`;
- ведёт current support и прогноз roof landing;
- выравнивает общий root по canonical board baseline;
- переводит stale landing support на безопасное падение к дороге;
- не решает damage/destroy/bump.

`SkateboardInteractionPolicy`

- pure decision points: `Collect / Destroy / PreserveSupport / BumpOnly / Ignore`;
- одинаковая классификация для ride, jump и landing wave;
- владеет type/mode rules, но ничего не исполняет.

`SkateboardLandingImpactRuntime`

- snapshot viewport obstacles обеих линий;
- distance wave, bump, delay и falloff;
- super-attack destroy event;
- gameplay-time pause и pool guards;
- явный `ICameraShake`, полученный через composition root.

`CollisionController` получает decision и только исполняет collect/damage/destroy/preserve/ignore. `LandingImpact` выбирает targets/timing и исполняет ту же policy. Camera и `ObstacleSpawner` приходят явно через Zenject/runtime composition.

## Что переиспользовать

- `ShiftTransformAnimatorController` — общий lane shift.
- `ISuperAttackRuntime`, `UltaMechanics`, `SuperAttackFactory`.
- `JumpRequest`, `SuperJumpRequest`, `TapRequest`.
- `DoubleJumpDetector` как база combo-input.
- `ObstacleSpawner.SpawnedObstacles`.
- `DestroyObstacleBySuperAttackEvent` + pool unspawn.
- `SkinVisualHost`/Addressables lease pattern.

На mode active отключить normal jump/roof/energy mechanics единым gate. Не выключать компоненты по одному вручную.

## Почему full actor switch

| Вариант | Цена сейчас | Долгий риск | Решение |
|---|---:|---:|---|
| Точечно остановить Transform Animator, заменить visual/body | ниже | mixed ownership, stale cached refs | только prototype |
| Переключать цельные sibling actors | средняя | нужен явный mode contract | рекомендуем |

Минимальный чистый путь: общий `Hamster` + общий shift + два sibling actors + switcher на root. Это не полная подмена всего Hamster prefab: gameplay state и DI остаются стабильны.

Проект использует Zenject, не Ninject. Prefab bind идёт через `GameSceneInstaller`; runtime Hamster создаётся обычным `Instantiate`. Switcher не требует отдельного DI binding: refs сериализуются в canonical prefab.

## Legacy Skateboard Skin cleanup

Отменённый Skateboard-as-skin package удалён:

- `Assets/Content/prefabs/skins/skateboard/`;
- `Assets/Content/skins/skateboard/`;
- `Assets/Animations/Hamster/skin_visuals/skateboard/`;
- skin ID/address/localization/editor-validator/Addressables entries.

Это был placeholder старой идеи, не source art нового super attack. Legacy save ID `3` очищается до known-ID validation; migration держать минимум один release window.

## Current prefab-prep status

- Canonical: `Assets/Content/prefabs/Hamster.prefab`.
- Его GUID `0be6ee3d4483271438e1571674c81ec6` и Hamster fileID `2971939310045773830` сохранены; `Game.unity` link жив.
- `Assets/Content/prefabs/Hamster-old.prefab` создан с отдельным GUID; он хранит исходную hierarchy.
- Canonical prefab уже имеет active `normal_actor`, inactive `skateboard_actor`, switcher, два hosts и заполненные root refs.
- Skateboard actor уже имеет placeholder polygon collider, collision controller, collider-sync stub и `skin_slot`; лишнего `effects_slot` нет.
- Existing normal assets перенесены в `normal_mode` вместе с `.meta`; content hash `108/108` совпал.
- Для трёх skin slugs созданы skateboard controllers, visual prefabs, sprite folders и Addressables entries. Default содержит пять clips и mappings.
- Prebuilt Windows AssetBundle catalog пока содержит старый Hamster path; пересобрать после завершения prefab migration, не редактировать JSON вручную.

Подробный файловый аудит: [Analysis.md](Analysis.md).

## Минимальный план

1. Завершить prefab/asset skeleton через Unity AssetDatabase. Без gameplay logic.
2. Добавить skateboard ID/catalog + runtime class.
3. Реализовать actor switch и visual selection/fallback.
4. Добавить mode FSM, Road/Roof surface flow и gates normal mechanics.
5. Реализовать cached sprite collider sync.
6. Добавить jump collision policy.
7. Добавить landing bump/destroy/wave/shake. ✅
8. Проверить timeout, `1+1+1`, `2+1`, `3`, pause, damage ride, destroy jump, both lanes, pooled reuse, cleanup.
