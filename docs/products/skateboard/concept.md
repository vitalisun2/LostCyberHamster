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
         ├─ collision_body
         │  ├─ PolygonCollider2D
         │  ├─ CollisionController
         │  └─ SpritePhysicsShapeColliderSync
         └─ skin_slot                        SkinVisualHost
```

Skateboard actor не меняет Transform по Y. Прыжок нарисован sprite animation. Новый skateboard `effects_slot` пока не нужен: визуальные эффекты живут в его animations.

## Игровые правила

- Ride: hamster уязвим; collision обрабатывается как обычный run damage.
- Shift между двумя линиями работает всегда.
- Обычный vertical Transform jump отключён.
- Skateboard jump и landing: hamster неуязвим; physical contact уничтожает obstacle через super-attack channel.
- До первого jump действует timeout `10 s` gameplay time.
- Первый jump отменяет timeout навсегда.
- Всего ровно `3` jumps: `1+1+1`, `2+1`, `1+2`, `3`.
- Mode заканчивается после landing tail третьего израсходованного jump; impact срабатывает в contact frame.
- Активация разрешена из stable `Run` и `RoofRun`.
- Roof top — опора: ride/jump landing её не уничтожают.
- Roof side — obstacle: ride получает damage, jump уничтожает.
- Roof-chain продолжается на крыше текущей линии. Без опоры `surface_transform` плавно опускает visual и collider на дорогу вместе.
- Road остаётся road: skateboard jump не создаёт новое приземление на крышу.

Один jump-cycle тратит один из трёх jumps. Double input до contact усиливает текущий cycle до super-jump и второй jump не списывает. Contact: `0.833 s`; полный clip: `1.25 s`. Следующий cycle ставится в очередь в landing tail. Без очереди mode возвращается в Ride и combo сбрасывается.

## Combo и landing impact

Combo растёт только у последовательных jumps без возврата в ride-chain:

| Landing | Обе линии | Shake | Bump |
|---|---|---|---|
| 1 / single | радиус `1 hamster width` | x1 | base |
| 2 подряд | радиус `2 hamster widths` | x2 | stronger |
| 3 подряд | весь visible screen | x3 | strongest |

Obstacle делает короткую дугу высотой `5%` своей высоты за `4 frames @ 60 FPS`; через ещё `3 frames` — destroy. Combo 2 усиливает bump до `1.25x`. Combo 3: bump `1.5x` рядом, затухает до `0.975x` у края; wave растягивается максимум на `0.25 s`. Camera shake: `0.18 s`, базовая амплитуда `0.08 units`, множитель combo `1x/2x/3x`.

Destroy идёт через существующий super-attack channel/pool unspawn. Snapshot содержит только regular physical obstacles обеих линий; collectables, decor и все roof platforms исключены. Delayed target инвалидируется через `OnObstacleUnspawned` и перед destroy повторно проверяется по ссылке в live-list: reused pooled object не получает старый impact.

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

`skateboard_actor` имеет базовый local Y `0.756`: visual и collider вместе совпадают с road baseline normal actor. Sprite pivot остаётся общим `(0.5, 0.225)`, `surface_transform.localY = 0` остаётся базой roof/road alignment.

## Ownership

`Hamster`

- общие state/events/lives/energy/mechanics;
- serialized refs: switcher, normal host, skateboard host;
- compatibility property normal `SkinVisualHost` на время миграции.

`HamsterActorSwitcher`

- refs на два actor;
- active/inactive switch;
- current mode;
- без timer/jump/combo/damage/loading.

`SkateboardAttack : ISuperAttackRuntime`

- mode lease/lifecycle;
- timeout, 3 jumps, combo FSM, Road/Roof surface flow;
- немедленный exit после принятого ride damage;
- gates normal jump/roof mechanics;
- cleanup на exit/finish/dispose.

`SkateboardSurfaceController`

- хранит `Road / Roof / DroppingToRoad`;
- ведёт текущую roof support текущей линии;
- выравнивает общий `surface_transform` по реальному roof bounds;
- возвращает normal actor на фактическую поверхность.

`SkateboardLandingImpactMechanics`

- snapshot visible obstacles обеих линий;
- radius/wave/bump/delay/falloff;
- super-attack destroy event;
- gameplay-time pause/finish/pool guards;
- явный `ICameraShake`, полученный через composition root.

`CollisionController` применяет skateboard collision policy:

- ride -> current damage;
- jump/landing -> ignore damage + destroy regular obstacle;
- roof top -> support, остаётся жив;
- roof side -> ride damage или jump/landing destroy.

Destroy проходит отдельной ранней веткой до обычного damage path.

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
- Для трёх skin slugs созданы empty skateboard controllers, visual prefabs, sprite folders и Addressables entries. Sprites/clips/mappings/gameplay logic появятся следующим шагом.
- Prebuilt Windows AssetBundle catalog пока содержит старый Hamster path; пересобрать после завершения prefab migration, не редактировать JSON вручную.

Подробный файловый аудит: [Analysis.md](Analysis.md).

## TBD

- exact visual catalog/address schema;
- collider update event from visual Animator/SpriteRenderer;

## Минимальный план

1. Завершить prefab/asset skeleton через Unity AssetDatabase. Без gameplay logic.
2. Добавить skateboard ID/catalog + runtime class.
3. Реализовать actor switch и visual selection/fallback.
4. Добавить mode FSM, Road/Roof surface flow и gates normal mechanics.
5. Реализовать cached sprite collider sync.
6. Добавить jump collision policy.
7. Добавить landing bump/destroy/wave/shake. ✅
8. Проверить timeout, `1+1+1`, `2+1`, `3`, pause, damage ride, destroy jump, both lanes, pooled reuse, cleanup.
