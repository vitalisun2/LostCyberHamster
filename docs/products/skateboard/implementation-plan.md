# Skateboard: план реализации

- [x] ✅ Подготовить prefab и asset skeleton: canonical/old Hamster, два actor, flat skin sprite folders, normal/skateboard modes, пустые skateboard visual prefabs и Addressables entries.

- [x] ✅ Добавить skateboard sprite assets. Настроить общие pivot, PPU и Custom Physics Shapes для всех кадров.

- [x] ✅ Собрать skateboard Animator Controller, clips и `SkinVisual` mappings: ride A/B, push, jump, super-jump; landing живёт в хвосте jump clips.

- [ ] Расширить skin catalog и visual loading: normal/skateboard variant одного slug, fallback skateboard `default`.

- [ ] Реализовать `HamsterActorSwitcher`: active actor, текущий mode, возврат normal actor. Сохранить общий lane shift.

- [ ] Реализовать `SpritePhysicsShapeColliderSync`: cache physics paths при visual load, `PolygonCollider2D.SetPath()` при смене sprite.

- [ ] Добавить `SkateboardAttack : ISuperAttackRuntime`: activation только из stable `Run`, lifecycle, cleanup, timeout `10 s` до первого jump.

- [ ] Добавить skateboard FSM: ride, jump, super-jump, landing impact, три jumps и combo `1+1+1`, `2+1`, `1+2`, `3`.

- [ ] Добавить единый gate normal jump/roof/energy mechanics на active skateboard mode. Ride оставляет current damage policy.

- [ ] Расширить collision policy: skateboard jump игнорирует damage и уничтожает obstacle через super-attack channel.

- [ ] Реализовать landing impact: snapshot обеих линий, bump, delayed destroy с pool token guard, radius/wave/falloff, camera shake.

- [ ] Подтвердить activation и exit policy для damage, roof и air. Зафиксировать combo window, landing frame, bump/destroy/wave timing, shake параметры.

- [ ] Пройти integration проверки: timeout, combos, pause, finish, ride damage, jump destroy, обе линии, pooled reuse, cleanup и visual fallback.

- [ ] Пересобрать Addressables/Windows AssetBundles после prefab migration. Проверить новый canonical Hamster path в catalog.
