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
         ├─ collision_body
         │  ├─ PolygonCollider2D
         │  ├─ CollisionController
         │  └─ SpritePhysicsShapeColliderSync
         └─ skin_slot
```

Нет `actor_slot`. Оба actor — прямые children общего shift-root. Включён ровно один actor целиком. Vertical Transform Animator существует только в `normal_actor`. Skateboard jump не двигает Transform; `surface_transform` меняет Y только для roof/road alignment.

## Road и roof

Активация: stable `Run` или `RoofRun`. Air state отклоняется.

`SkateboardSurfaceController` держит независимое состояние `Road / Roof / DroppingToRoad`. На крыше collider и visual стоят под общим `surface_transform`. Высота берётся из реального obstacle bounds, без чисел normal transform clips.

Roof top — support. При landing выбирается любая roof support текущей линии под actor; same/next identity не важен. Roof side — obstacle. С крыши можно продолжить roof-chain или плавно спуститься на road. Из road новая roof support не создаётся.

Collision policy готова. Ride использует обычный damage path. Jump и landing не получают damage и уничтожают regular obstacle через super-attack channel. Roof top остаётся support и не уничтожается. Roof side следует общей policy: ride damage, jump/landing destroy.

Normal `RoofRunMechanics` молчит при active skateboard. На exit текущая support возвращается в `Hamster.LastObstacle`; road exit сбрасывает normal Transform Animator в default pose.

Gameplay FSM держит timing, Animator Events не участвуют. Jump contact наступает через `10/12 s`, полный cycle — `1.25 s`. Landing tail принимает следующий cycle в combo. Double input усиливает текущий cycle до super variant без второго списания budget.

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
