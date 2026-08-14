# Skateboard prefab: анализ

Статус: принятые решения brainstorming + файловый аудит переходной структуры. Compile/runtime ошибки пока не критерий: feature намеренно незавершён.

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

`SkateboardSurfaceController` держит независимое состояние `Road / Roof / DroppingToRoad`. На крыше collider и visual стоят под общим `surface_transform`. Высота берётся из реального obstacle bounds, без чисел normal transform clips.

Roof top — support. При landing выбирается любая roof support текущей линии под actor; same/next identity не важен. Roof side — obstacle. С крыши можно продолжить roof-chain или плавно спуститься на road. Из road новая roof support не создаётся.

Collision policy готова. Ride использует обычный damage path. Jump и landing не получают damage и уничтожают regular obstacle через super-attack channel. Roof top остаётся support и не уничтожается. Roof side следует общей policy: ride damage, jump/landing destroy.

Принятый ride-damage lifecycle: `DamageEvent` сначала выполняет обычную потерю life и включает damage immunity/visual, затем `SkateboardAttack` немедленно завершает mode lease. Pending impact/shake очищаются, normal actor включается, текущая живая roof support восстанавливается; уже unspawned roof трактуется как road. Повторная активация блокируется, пока `Hamster.IsDamaged == true`.

Normal `RoofRunMechanics` молчит при active skateboard. На exit текущая support возвращается в `Hamster.LastObstacle`; road exit сбрасывает normal Transform Animator в default pose.

Gameplay FSM держит timing, Animator Events не участвуют. Jump contact наступает через `10/12 s`, полный cycle — `1.25 s`. Landing tail принимает следующий cycle в combo. Double input усиливает текущий cycle до super variant без второго списания budget.

Landing impact реализован отдельной mechanics. В contact frame она фиксирует gameplay obstacles и collectables обеих линий; decor и точная CurrentRoof не входят. Normal radii: `1 / 3 / весь экран`; Super radii: `2 / 6 / весь экран`. Bump усилен на `30%`: combo 1 `13%..9.1%`, combo 2 `19.5%..10.725%`, combo 3 `27.3%..12.285%` высоты с distance falloff. Combo 3 строго сильнее combo 2 на равной normalized distance. Три wave-группы: combo 1 `0/.04/.08 s`, combo 2 `0/.08/.16 s`, combo 3 `0/.13/.26 s`; per-target jitter `0..12 ms`. Дуга `5 frames`, destroy через `3 frames`. Collectables получают bump и остаются live/pickable. Combo 3 snapshot имеет правый scroll buffer `~1.54 units + target width`; natural mode exit сохраняет pending wave, forced exit отменяет. Shake: `0.08 units`, `0.18 s`, множители `1/2/3`. Pool reuse защищён invalidation, live-list/CurrentRoof recheck и idempotent subscription.

Камера приходит явно через Zenject composition root: `GameSceneInstaller -> GameEntryPoint bundle -> InitCharacterLoadingTask -> SuperAttackFactory`. `SkateboardAttack` создаёт scoped `ICameraShake`; глобального `Camera.main` внутри gameplay mechanics нет.

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

`visual_collision_root` масштабирует только skateboard visual и collider до `0.7`. `skateboard_actor.localPosition.y = 0.39225`: ride physics bottom `-1.1675 * 0.7 + 0.39225` совпадает с normal collider bottom `-0.425`. Общий sprite pivot `(0.5, 0.225)` сохраняется. `surface_transform.localY = 0` остаётся чистой road-точкой для roof/road controller.

`gameplay_collision_sensor` остаётся вне scaled root. Его trigger `1.64 x 0.85`, local Y `-0.39225`; world baseline совпадает с normal collider. Sensor закрывает прозрачные gaps точного sprite shape и проводит все шесть physical types и collectibles через общий collision policy. Main PolygonCollider продолжает следовать нарисованному frame.

## Ownership и DI

- `Hamster`: общие state/events/mechanics и явные refs на switcher + оба hosts.
- `HamsterActorSwitcher`: только actor active state.
- `SkateboardAttack : ISuperAttackRuntime`: activation из живого `Run`/`RoofRun`, mode lifecycle, timeout `10 s`, три jump cycles, combo, surface flow, finish/dispose cleanup.
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
- Sprite assets, clips, mappings и gameplay logic намеренно отсутствуют. `_spriteRenderer` collider-sync будет привязан к загруженному visual позже.
- Prebuilt `Assets/AssetBundles/Windows/catalog.json` ещё содержит старый prefab path. Не править руками; пересобрать bundles после завершения prefab migration.

## File-review gate

Проверено: один canonical GUID; backup имеет другой GUID; scene указывает canonical; normal actor active; skateboard actor inactive; оба `skin_slot` имеют host; root serialized refs non-null; normal Addressables GUID не изменились; три skateboard addresses уникальны. `git diff --check` чистый. Compile/runtime review отложен до полной реализации feature.

## Tools/Testing: базовый Skateboard runner

`Tools/Testing` получает одну root-кнопку `Skateboard Mode Testing` и отдельную IMGUI page. Prefab/scene object не нужен.

Preparation-команда `Unlock & Select Skateboard` работает после Bootstrap: проверяет ID `3`, читает `RequiredPlayerLevel` из каталога, через testing-only seam `PlayerExperienceService` начисляет ровно недостающий XP по production level-up математике, проверяет unlock и вызывает настоящий `SuperAttackService.TrySelect(3)`. Экран Super Attacks открывать не требуется. Уже созданный gameplay Hamster не заменяет runtime: после выбора нужен следующий вход в уровень.

Gameplay-команды используют production events: charge `100` + `UltaEvent` для входа, `JumpRequest`/`SuperJumpRequest` для cycles. Есть timeout и четыре state-driven сценария: `1+1+1`, `2+1`, `1+2`, `3`; toggle `Super Jump` усиливает каждый cycle, не дублируя кнопки. Runner ждёт реальные `Ride / Landing`, а не фиксированные задержки, и проверяет impact depths. `Pause / Resume / Cancel` управляют lifecycle.

Повторное нажатие любой activation/scenario-кнопки при active mode проверяет rejected activation: budget, combo и phase не должны сброситься; текущий scenario продолжает работу. Отдельная кнопка для этого не нужна.

Jump collision, landing impact и roof behavior пока наблюдаются на обычном уровне во время сценариев. Dedicated obstacle staging, forced finish, pool-reuse race и skin-fallback automation отложены; в базовую page не входят.
