# Skateboard: план реализации

- [x] ✅ Подготовить prefab и asset skeleton: canonical/old Hamster, два actor, flat skin sprite folders, normal/skateboard modes, пустые skateboard visual prefabs и Addressables entries.

- [x] ✅ Добавить skateboard sprite assets. Настроить общие pivot, PPU и Custom Physics Shapes для всех кадров.

- [x] ✅ Собрать skateboard Animator Controller, clips и `SkinVisual` mappings: ride A/B, push, jump, super-jump; landing живёт в хвосте jump clips.

- [x] ✅ Расширить skin catalog и visual loading: normal/skateboard variant одного slug, fallback skateboard `default`.

- [x] ✅ Реализовать `HamsterActorSwitcher`: active actor, текущий mode, возврат normal actor. Сохранить общий lane shift.

- [x] ✅ Реализовать `SpritePhysicsShapeColliderSync`: cache physics paths при visual load, `PolygonCollider2D.SetPath()` при смене sprite.

- [x] ✅ Добавить `SkateboardAttack : ISuperAttackRuntime`: activation только из stable `Run`, lifecycle, cleanup, timeout `10 s` до первого jump.

- [x] ✅ Добавить skateboard FSM: ride, jump, super-jump, landing impact, три jumps и combo `1+1+1`, `2+1`, `1+2`, `3`.

- [x] ✅ Добавить единый gate normal jump/roof/energy mechanics на active skateboard mode. Ride оставляет current damage policy.

- [x] ✅ Поддержать `Run` и `RoofRun`: отдельный `surface_transform`, roof-chain, landing на крышу, плавный спуск на дорогу. Roof top остаётся опорой; road-to-roof отсутствует.

- [x] ✅ Расширить collision policy: ride получает обычный damage; jump и landing уничтожают obstacle через super-attack channel. Roof top остаётся опорой.

- [x] ✅ Реализовать landing impact: snapshot обеих линий, bump, delayed destroy с pool reuse guard, radius/wave/falloff, camera shake.

- [x] ✅ При ride damage выполнить обычную потерю life и немедленно завершить mode с восстановлением normal actor/surface. Landing timing и shake параметры зафиксированы.

- [x] ✅ Доработать `Tools/Testing > Skateboard Mode Testing`: preparation, четыре scripted combos, `Super Jump`, `On Roof` gate, automatic timeout, passive Ride Damage / Jump Collision / Lane Shift watchers, общий checklist/status, pause-safe lifecycle.

- [x] ✅ Добавить узкий Skateboard collision diagnostic event. Проверять post-outcome physical type, damage/destroy/support/collect, lives, obstacle и current roof support.

- [x] ✅ Выровнять lane input contract: jump принимается во время shift; tap блокируется на всём jump/landing tail до Ride.

- [ ] Пройти Unity Live проверки: timeout с Pause/Resume, road/roof combos, Ride Damage checklist, Jump Collision checklist, Lane Shift guide, finish, pooled reuse, cleanup и visual fallback.

- [ ] Пересобрать Addressables/Windows AssetBundles после prefab migration. Проверить новый canonical Hamster path в catalog.
