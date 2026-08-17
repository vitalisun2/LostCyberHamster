# Skateboard prefab: анализ

Статус: актуальный gameplay-контракт и архитектура Skateboard mode.

## Target prefab

```text
Hamster                                      active
  Hamster
  HamsterActorSwitcher
└─ shift_transform_animations                active; общий shift двух линий
   ├─ normal_actor                           active
   │  └─ transform_animations
   │     ├─ collision_body
   │     ├─ skin_slot
   │     └─ effects_slot
   └─ skateboard_actor                       inactive
      └─ surface_transform
         ├─ visual_collision_root             scale 0.7
         │  ├─ collision_body
         │  │  ├─ PolygonCollider2D
         │  │  ├─ CollisionController
         │  │  └─ SpritePhysicsShapeColliderSync
         │  └─ skin_slot
         └─ gameplay_collision_sensor         BoxCollider2D trigger
```

Нет `actor_slot`. Оба actor — прямые children общего shift-root. Включён ровно один actor целиком. Vertical Transform Animator существует только в `normal_actor`. Skateboard jump не двигает Transform; `surface_transform` меняет Y только для roof/road alignment.

## Road и roof

Активация: stable `Run` или `RoofRun`. Air state отклоняется.

`SkateboardSurfaceController` держит только geometry `Road / Roof / DroppingToRoad`, current support и landing `Support/MissedRoof`. Stable sensor задаёт board baseline и X-footprint. Animated polygon определяет top-vs-side только в contact frame. Общий `surface_transform` двигает visual+colliders вместе.

`SkateboardAttack.StartJump` создаёт immutable `{ ActionId, StartedOnRoof }`. Origin не выводится из позднего CurrentRoof/session. Road/Roof policy каждого нового cycle может отличаться после drop.

Единая `SkateboardInteractionPolicy` возвращает `Collect / Damage / Destroy / PreserveSupport / BumpOnly / Ignore`. Collision и landing wave только исполняют decision. Ride vulnerable; проходимая roof-chain preserve, side/gap damage. StartedOnRoad jump уничтожает все physical. StartedOnRoof jump сохраняет любые roof contacts; landing top принимает support, конкретный side/inside miss уничтожает roof. Roof box уничтожается в jump и даёт damage в Ride. Collectable contact = pickup.

Принятый ride-damage lifecycle: `DamageEvent` сначала выполняет обычную потерю life и включает damage immunity/visual, затем `SkateboardAttack` немедленно завершает mode lease. Pending impact/shake очищаются, normal actor включается, текущая живая roof support восстанавливается; уже unspawned roof трактуется как road. Повторная активация блокируется, пока `Hamster.IsDamaged == true`.

Normal `RoofRunMechanics` молчит при active skateboard. На exit текущая support возвращается в `Hamster.LastObstacle`; road exit сбрасывает normal Transform Animator в default pose.

Gameplay FSM держит timing, Animator Events не участвуют. Один multiplier `1.5`: RideA/RideB/Push/Jump/SuperJump. Contact `0.556 s`, cycle `0.833 s`, tail `0.278 s`. Visual и contact timing ускорены вместе. Landing tail принимает следующий cycle; double input не списывает второй budget.

Landing impact получает immutable request из cycle. StartedOnRoad wave уничтожает все physical. StartedOnRoof: roof bump-only/live, current support preserve, roof box/прочий physical destroy. Collectable bump-only/live; decor ignore. Tuning прежний: normal `1/3/screen`, super `2/6/screen`, combo3 right buffer, wave/falloff/shake. Natural final wave не отменяется. Target хранит outcome и spawn identity; поздний mutable mode state не перечитывается.

Camera и `ObstacleSpawner` приходят явно: `GameSceneInstaller -> GameEntryPoint bundle -> InitCharacterLoadingTask -> SuperAttackFactory -> Surface/Landing`. Factory собирает shake, landing и attack. Attack не выпускает `this` из constructor; Landing не подписан на Attack.

Новый skateboard `effects_slot` сейчас не нужен. Dust/flip/impact рисуются внутри его visual animations. Отдельный effect host добавлять только после реального требования.

`HamsterActorSwitcher` — отдельный компонент на том же GameObject, что `Hamster`. Он хранит только refs `normal_actor`/`skateboard_actor`, переключает active state и сообщает mode. Timer, прыжки, combo, damage, visual loading сюда не входят. Термин и отдельный слой `endpoints` не нужны: конкретные refs добавляются только когда появится реальная зависимость.

Switcher реализован как idempotent full-branch toggle. При старте нормализует prefab в `normal_actor active / skateboard_actor inactive`; при смене сначала выключает прежнюю ветку, затем включает целевую. Общий `shift_transform_animations` не меняется.

## Visual skins двух режимов

Skateboard — gameplay mode, не skin. Но выбранный character skin имеет две visual-версии: normal и skateboard.

Оба actor используют одинаковое имя `skin_slot` и отдельный экземпляр `SkinVisualHost`. Это сохраняет один prefab-контракт. Пока не создаём `SkateboardVisualHost`: сначала расширяем общий visual contract настолько, насколько реально нужно skateboard-анимациям.

Каждый visual prefab: `SpriteRenderer + Animator + SkinVisual` на root. Normal и skateboard variants имеют одинаковые slug: `default`, `neon-runner`, `quantum-scout`. Нет skateboard variant — fallback на skateboard `default`.

Default skateboard visual использует clips `ride_1`, `ride_2`, `push`, `jump`, `super_jump` на `12 FPS`. Первые три loop. Прыжки one-shot; последние frames уже содержат landing. Controller не имеет transitions: будущий mode FSM выбирает state через `SkinVisual` mappings.

Skin catalog хранит `SkinVisualAddress` и `SkateboardSkinVisualAddress`. Пустой skateboard address у non-default skin означает fallback на skateboard `default`. При старте забега оба visual prefab загружаются в свои `skin_slot`; `Hamster` владеет двумя runtime leases и освобождает оба вместе.

Target asset layout:

```text
Assets/Animations/Hamster/
  ShiftTransformAnimator.controller
  normal_mode/
  skateboard_mode/

Assets/Content/prefabs/skins/
  normal_mode/<slug>/<slug>-skin-visual.prefab
  skateboard_mode/<slug>/<slug>-skin-visual.prefab

Assets/Content/skins/
  normal_mode/<slug>/*.png
  skateboard_mode/<slug>/*.png

Assets/Content/prefabs/
  Hamster.prefab
  Hamster-old.prefab
```

Shared `skins.json`, portraits и unrelated effects остаются вне mode-folders.

## Collider sprite animation

`PolygonCollider2D` живёт в skateboard `collision_body`; visual prefab остаётся в `skin_slot`. Все sprite frames получают ручной упрощённый `Custom Physics Shape`, общий pivot/PPU/scale.

Shapes не вычислять каждый frame. При загрузке visual один раз прочитать `Sprite.GetPhysicsShape*()` всех кадров и закешировать готовые paths по `Sprite`. При смене кадра только применить paths через `PolygonCollider2D.SetPath()`. Результат: collider синхронен нарисованному прыжку; повторного анализа геометрии и лишних allocations нет.

Реализация: каждый skateboard visual хранит serialized manifest кадров. Host сообщает sync о bind/unbind visual. Sync заранее кеширует все paths, а в `LateUpdate` реагирует только на смену ссылки `Sprite`; несколько paths сохраняют отдельные контуры тела и доски.

`visual_collision_root` масштабирует visual и animated collider до `0.7`. Canonical board baseline берётся из unscaled `gameplay_collision_sensor`, не из меняющихся sprite bounds. `surface_transform.localY = 0` остаётся road-точкой.

`gameplay_collision_sensor` вне scaled root: trigger `1.64 x 0.85`, world bottom `-0.425`. Он задаёт stable baseline/X footprint и закрывает прозрачные gaps. Main PolygonCollider следует frame и даёт top-vs-side landing geometry.

## Ownership и DI

- `Hamster`: общие state/events/mechanics и явные refs на switcher/surface/оба hosts.
- `HamsterActorSwitcher`: только actor/collider branch.
- `SkateboardAttack`: mode authority, timeout, budget/combo/FSM, immutable jump snapshot, cleanup.
- `SkateboardSurfaceController`: только support/top/side/miss/alignment/drop geometry.
- `SkateboardInteractionPolicy`: единственный mode/type decision point.
- `CollisionController` и `LandingImpact`: executors одного policy.
- Skateboard visual Animator: ride/push/jump/super-jump/landing timing.
- Общий shift Animator: lane shift обоих actor.

Super attack catalog: skateboard ID `3`, unlock level `4`, charge per obstacle `20`. До отдельной UI-иконки карточка использует `skin_default`; gameplay effect prefab не нужен.

Проект использует Zenject, не Ninject. `GameSceneInstaller` bind-ит Hamster prefab; `GameEntryPoint` получает его через `[Inject]`; runtime instance создаётся обычным `Instantiate`. Prefab refs сериализуются Inspector-ом. Отдельный Zenject binding для switcher не нужен.

## Фактическое состояние файлового шага

- Canonical prefab лежит в `Assets/Content/prefabs/Hamster.prefab` вместе с `.meta`.
- GUID `0be6ee3d4483271438e1571674c81ec6` и Hamster component fileID `2971939310045773830` сохранены; `Game.unity` продолжает ссылаться на них.
- `Hamster-old.prefab` хранит исходную hierarchy; его новый GUID `16bf434ef07c2f047ba105fb0a22ce11`, AssetBundle name очищен.
- Canonical hierarchy перестроена: sibling `normal_actor` active и `skateboard_actor` inactive под общим shift-root; root refs и оба hosts заполнены.
- Skateboard actor получил `PolygonCollider2D`, `CollisionController`, collider-sync stub и `skin_slot`; отдельного `effects_slot` нет.
- Existing normal animations, visual prefabs и sprites перенесены в `normal_mode` вместе с исходными `.meta`; проверка blob hash: `108/108` совпадают.
- Existing Addressables entries normal skins сохранили GUID.
- Для `default`, `neon-runner`, `quantum-scout` созданы empty Animator Controllers, visual prefabs `SpriteRenderer + Animator + SkinVisual`, sprite folders и уникальные skateboard Addressables entries.
- Default visual содержит sprites, пять clips и mappings. Collider-sync привязывается к загруженному visual через host.
- Prebuilt `Assets/AssetBundles/Windows/catalog.json` ещё содержит старый prefab path. Не править руками; пересобрать bundles после завершения prefab migration.

## File-review gate

Проверено: один canonical GUID; backup имеет другой GUID; scene указывает canonical; normal actor active; skateboard actor inactive; оба `skin_slot` имеют host; root serialized refs non-null; normal Addressables GUID не изменились; три skateboard addresses уникальны. Статический diff чистый; gameplay проверяется в Unity Live.

## Tools/Testing: Skateboard runner

`Tools/Testing` получает одну root-кнопку `Skateboard Mode Testing` и отдельную IMGUI page. Prefab/scene object не нужен.

Preparation-команда `Unlock & Select Skateboard` работает после Bootstrap: проверяет ID `3`, читает `RequiredPlayerLevel` из каталога, через testing-only seam `PlayerExperienceService` начисляет ровно недостающий XP по production level-up математике, проверяет unlock и вызывает настоящий `SuperAttackService.TrySelect(3)`. Экран Super Attacks открывать не требуется. Уже созданный gameplay Hamster не заменяет runtime: после выбора нужен следующий вход в уровень.

Раздел `Scripted Scenarios` содержит две action-кнопки: `Jump` и `Super Jump`. Каждая сама активирует Skateboard, фиксирует `surface=Road` из stable `Run` или `surface=Roof` из stable `RoofRun` с живой support и отправляет соответствующий production request. Runner проверяет accepted action, landing impact depth `1`, normal/super type, расход budget ровно на `1` и возврат в `Ride`, затем завершает mode.

Guided model наблюдает ручной input и сам восстанавливает Skateboard lease между шагами. Одновременно активен один passive watcher. Одна toggle-кнопка `Pause / Resume` сохраняет active check; timers и watchdog в `PAUSED` стоят. Один глобальный `Stop Check` останавливает runner и возвращает normal actor.

- `Timeout` фиксирует `Enter Mode — PASS`, не отправляет jump, ждёт `10` gameplay seconds и фиксирует `Timeout — PASS`. Проверяет first-jump waiting, целый budget, выключенный Skateboard и включённый normal actor.
- `Ride Collision` собирает шесть physical types в любом порядке. Каждый контакт обязан дать ровно `life -1`, завершить mode, включить normal actor и сохранить obstacle. Roof support не входит.
- `Jump Collision` собирает те же types. Life не меняется. StartedOnRoad уничтожает все physical. StartedOnRoof сохраняет `bigNotAlive/mediumNotAlive`, остальные уничтожает. Collectibles не входят.
- `Lane Shift` наблюдает ручной input. Проверяет Ride tap с завершением на другой линии, принятый jump во время active shift и отклонённый tap после старта jump.

Для точной корреляции `CollisionController` публикует один DEV diagnostic event: physical type, outcome, lives, obstacle live-state, jump phase и current roof support live-state. Event только наблюдает post-outcome state.

Normal lane contract: tap разрешён только в `Run/RoofRun` вне shift; jump во время shift принимается; tap после jump отклоняется. Skateboard держит non-run Hamster state через jump и landing tail, затем синхронизирует surface при возврате в Ride.

Page всегда заново resolve-ит live `Hamster`, configured `SkateboardAttack` и `GameManager`. Runtime refs живут только внутри active check.
