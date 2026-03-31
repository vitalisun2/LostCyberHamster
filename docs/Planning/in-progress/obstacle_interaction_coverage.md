# Покрытие взаимодействий бота с объектами

Дерево всех геймплейных взаимодействий хомяка с объектами в разрезе позиции и доступных действий.
Цель — видеть текущие белые пятна и из них составлять задачи на расширение логики бота.

## Условные обозначения

| Символ | Значение |
|---|---|
| ✅ | Бот планирует и исполняет корректно |
| ⚠️ | Бот генерирует действие, но проекция состояния некорректна |
| ❌ | Бот не планирует — действие отсутствует |

### Позиции хомяка
- **Road/bottom** — нижняя дорожка (`HamsterOnBottom=true`)
- **Road/top** — верхняя дорожка (`HamsterOnBottom=false, HamsterOnRoof=false`)
- **Roof** — на крыше bigNotAlive/mediumNotAlive (`HamsterOnRoof=true`)

### Действия бота (BotAction enum)
Зарегистрированы в `StepExecutor`: `SwitchLane`, `Jump`.  
Отсутствует: `SuperJump` — ни стратегии, ни хэндлера нет.

> Важно: `SuperJump` нужен только там, где обычный `Jump` неэффективен или невозможен (напр. `bigAlive`). Для малых препятствий (`smallNotAliveRoad`) SuperJump выполним, но бесполезен — трата энергии без профита.

---

## 1. Угрозы (Threat)

### 1.1 smallNotAliveRoad ✅ полностью покрыт
Расположение: только на дороге (road/bottom или road/top).

```
[Хомяк на Road — obstacle на той же дорожке]
├── SwitchLane → избегаем, уходим на другую дорожку        ✅
└── Jump → JumpOver                                         ✅
    SuperJump физически возможен, но не нужен — тратит энергию без выгоды.

[Хомяк на Roof — smallNotAliveRoad на дороге]
    Хомяк на крыше не пересекается с road-level объектами.
    Угрозой становится при RunFromRoof-спуске на эту дорожку.
    ❌ Бот не учитывает это при планировании спуска с крыши.
    (Попадает в T-4: RunFromRoof safety planning.)
```

### 1.2 smallNotAliveRoadAndRoof
Два физических контекста одного типа: стоит на дороге или стоит на крыше bigNotAlive.

```
[Контекст A: стоит на дороге, хомяк на той же дорожке]
├── SwitchLane → избегаем                                   ✅
├── Jump → JumpOver                                         ✅
└── SuperJump → SuperJumpOver                               ❌

[Контекст B: стоит на крыше bigNotAlive, хомяк на дороге]
    Сам по себе не является прямой угрозой для хомяка на дороге —
    только создаёт риск при прыжке на эту крышу.
    Учитывается в JumpStrategy.IsLaneClearAtCompletion (проверка зоны приземления).
    ├── JumpOnRoof (safe) — если бот прыгает на bigNotAlive,
    │   а smallNotAliveRoadAndRoof находится за пределами зоны приземления ✅ (через JumpStrategy landing check)
    └── JumpOnRoofDamage — если бот прыгает на bigNotAlive и зацепляет
        smallNotAliveRoadAndRoof. Landing check должен отклонить такую ветку  ✅

[Контекст C: стоит на крыше bigNotAlive, хомяк в RoofRun]
    Хомяк на той же крыше, объект впереди как Threat.
    Физически IsTopLane=true → та же "полоса" в снимке → ProblemResolver видит угрозу.
    ├── SwitchLane → уход на другую крышу или вниз на дорожку          ⚠️ (SwitchLane генерируется, но корректность проекции после смены крыш не гарантирована)
    ├── RoofJump → прыжок к следующей крыше или над препятствием        ⚠️ (JumpHandler стреляет RoofJumpRequest, но JumpStrategy.ApplyJumpEffects обнуляет HamsterOnRoof=false — проекция приземления неверна)
    └── SuperRoofJump                                                   ❌
```

### 1.3 bigNotAlive
Расположение: дорога (road/bottom или road/top).

```
[Хомяк на Road — та же дорожка]
├── SwitchLane → ✅
├── Jump → JumpOnRoof (прыжок на крышу)                    ❌  (JumpStrategy.IsSmallObstacle = false → отклоняет)
└── SuperJump → SuperJumpOnRoof                             ❌

[Хомяк уже на Roof bigNotAlive — следующий bigNotAlive впереди]
    RoofRunMechanics автоматически переходит к следующей крыше —
    это не bot-действие, а автоматическая механика.
    Угроза: smallNotAliveRoadAndRoof на следующей крыше (см. п. 1.2 Контекст C).
```

### 1.4 mediumNotAlive
Аналогично bigNotAlive (JumpMechanics и SuperJumpMechanics обрабатывают `mediumNotAlive`
через тот же хэндлер `HandleBigNotAlive`).

```
[Хомяк на Road — та же дорожка]
├── SwitchLane → ✅
├── Jump → JumpOnRoof (прыжок на крышу)                    ❌
└── SuperJump → SuperJumpOnRoof                             ❌
```

### 1.5 bigAlive (хомяк НЕ на крыше)
Когда хомяк на Road, `bigAlive` = Threat. Обычный Jump даёт `JumpDamageForBigAlive`
(нельзя перепрыгнуть обычным прыжком — слишком высокий).

```
[Хомяк на Road — та же дорожка]
├── SwitchLane → ✅
└── SuperJump → SuperJumpOver                               ❌  (единственный способ перепрыгнуть оставаясь на дорожке)

[Хомяк на Road — другая дорожка]
    Не угрозa. Потенциально Target если уйти и прыгнуть с другой дорожки → см. п. 2.2.
```

---

## 2. Цели (Target)

> Бот полностью игнорирует Target-объекты: `ProblemResolver` обрабатывает только `ThreatCollision`.
> Если `smallAlive` или `bigAlive` (с крыши) окажется на пути, хомяк получит урон.

### 2.1 smallAlive
Расположение: дорога (road/bottom или road/top).
Классификация: всегда `Target` (никогда не Threat).

```
[Хомяк на Road — тот же lane]
├── SwitchLane → уйти мимо (не получить профит, но избежать столкновения)  ❌
├── Jump → JumpOnObstacle (напрыг, получаем бонус)                          ❌
├── Jump → JumpOver (перепрыгнуть мимо)                                     ❌
└── SuperJump → SuperJumpOnObstacle / SuperJumpOver                         ❌

[Хомяк на Roof — falling onto smallAlive]
├── RoofJump → JumpOnObstacleFromRoof                                       ❌
└── SuperRoofJump → SuperJumpOnObstacleFromRoof                             ❌

Примечание: JumpMechanics поддерживает все эти исходы, бот их не планирует.
```

### 2.2 bigAlive (хомяк НА крыше → bigAlive как Target)
Когда хомяк на Roof, `bigAlive` = Target.

```
[Хомяк на Roof — bigAlive на дороге под крышей]
├── RoofJump → JumpOnObstacleFromRoof (спрыгнуть прямо на bigAlive)        ❌
└── SuperRoofJump → SuperJumpOnObstacleFromRoof                             ❌
```

---

## 3. Коллектиблы (Collectible)

> Бот полностью игнорирует Collectible-объекты: `ProblemResolver` обрабатывает только `ThreatCollision`.
> Монеты, энергия, кристаллы, жизни и пицца не собираются ботом.

```
[Collectible на той же дорожке, что хомяк]
└── Автосбор при прохождении (механика игры) — бот не уклоняется, работает само

[Collectible на другой дорожке]
└── SwitchLane → подойти и автособрать                     ❌

[Collectible любого типа: energetic / pizza / crystal / life / coin]
    Всё одинаково — нет планировщика для них.              ❌
```

---

## 4. Крышные переходы (специальный сценарий)

Этот сценарий охватывает движение по крыше: подъём, бег, спуск.

```
[Подъём на крышу — Road → Roof]
├── Jump → JumpOnRoof на bigNotAlive                       ❌  (JumpStrategy не генерирует для bigNotAlive)
├── Jump → JumpOnRoof на mediumNotAlive                    ❌
├── SuperJump → SuperJumpOnRoof                            ❌
└── SwitchLane: после прыжка на крышу — никак не задействовано

[Бег по крыше — RoofRun]
    Автоматически: RoofRunMechanics переходит к следующей крыше или запускает RunFromRoof.
    Bot-действия только при наличии smallNotAliveRoadAndRoof на текущей крыше (см. 1.2 Контекст C).

[Спуск с крыши — Roof → Road (RunFromRoof)]
    RunFromRoof — автоматический, не управляется ботом.
    Бот не проверяет, что зона спуска (≈1.9 units) свободна от угроз.   ❌
    При планировании JumpOnRoof бот не учитывает препятствия в RunFromRoof-зоне.

[Прыжок с крыши — RoofJump]
    RoofJump с крыши на другой объект:
    ├── → bigNotAlive (другая крыша): RoofJump                            ❌ (нет RoofJump стратегии)
    ├── → bigAlive: JumpOnObstacleFromRoof                                ❌
    ├── → smallAlive: JumpOnObstacleFromRoof                              ❌
    └── вниз на дорогу: JumpFromRoof (дефолт при отсутствии препятствий) ❌

[SuperRoofJump с крыши]
    Всё то же, но дальность прыжка больше.                               ❌
```

---

## Итоговая матрица

| Объект | Позиция хомяка | SwitchLane | Jump | SuperJump | RoofJump | SuperRoofJump |
|---|---|---|---|---|---|---|
| smallNotAliveRoad | Road same lane | ✅ | ✅ JumpOver | — не нужен | n/a | n/a |
| smallNotAliveRoadAndRoof | Road same lane | ✅ | ✅ JumpOver | — не нужен | n/a | n/a |
| smallNotAliveRoadAndRoof | Roof same roof | ⚠️ | ⚠️ RoofJump | n/a | ❌ | ❌ |
| bigNotAlive | Road same lane | ✅ | ❌ JumpOnRoof | ❌ | n/a | n/a |
| mediumNotAlive | Road same lane | ✅ | ❌ JumpOnRoof | ❌ | n/a | n/a |
| bigAlive | Road same lane | ✅ | ❌ (unavoidable) | ❌ SuperJumpOver | n/a | n/a |
| smallAlive | Road same lane | ❌ | ❌ JumpOnObstacle | ❌ | n/a | n/a |
| bigAlive | Roof (as Target) | ❌ | n/a | n/a | ❌ JumpOnFromRoof | ❌ |
| Collectibles | Other lane | ❌ | n/a | n/a | n/a | n/a |
| RunFromRoof zone | Roof → auto descent | ❌ no check | n/a | n/a | n/a | n/a |

---

## Roadmap задач по расширению покрытия

Задачи увеличивают покрытие ситуаций обработки ботом. Каждая задача = изменения в коде + тестовый уровень.

| ID | Описание | Статус | Документ |
|---|---|---|---|
| T-1 | SuperJump для `bigAlive` (forced) | ✅ Выполнено | [task_bot_bigalive_superjump.md](task_bot_bigalive_superjump.md) |
| T-2 | JumpOnRoof для `bigNotAlive`/`mediumNotAlive` | 🔮 Запланировано | — |
| T-3 | Roof coverage: `smallNotAliveRoadAndRoof` на крыше | 🔮 Запланировано | — |
| T-4 | RunFromRoof safety planning | 🔮 Запланировано | — |
| T-5 | Target planning для `smallAlive` | 🔮 Запланировано | — |
| T-6 | Target planning для `bigAlive` (с крыши) | 🔮 Запланировано | — |
| T-7 | Сбор Collectible с другой дорожки | 🔮 Запланировано | — |
| T-8 | Приоритизация Collectible vs Threat | 🔮 Запланировано | — |

---

## Детали задач

### Задачи-угрозы (Threat coverage)

**T-1. SuperJump для bigAlive (forced)** ✅
Выполнено. Файл задачи удалён, история в git: [task_bot_bigalive_superjump.md](task_bot_bigalive_superjump.md)
- `BotAction.SuperJump`, `SuperJumpStrategy`, `SuperJumpHandler` реализованы
- Покрывает: `bigAlive` → SuperJumpOver когда SwitchLane недоступен

**T-2. JumpOnRoof для bigNotAlive / mediumNotAlive**
- Расширить `JumpStrategy` или создать отдельную стратегию
- `IsSmallObstacle` → заменить на явное разделение: small → JumpOver, big → JumpOnRoof
- JumpStrategy должна строить шаг с семантикой JumpOnRoof и проекцией `HamsterOnRoof=true`
- Добавить проверку зоны RunFromRoof в проекции (препятствия в ~1.9u после приземления)
- Покрывает: `bigNotAlive`, `mediumNotAlive` → JumpOnRoof

**T-3. Roof coverage: smallNotAliveRoadAndRoof на крыше**

- Исправить `JumpStrategy.ApplyJumpEffects`: при `HamsterOnRoof=true` не обнулять `HamsterOnRoof`
- Проекция после RoofJump должна сохранять `HamsterOnRoof=true` если приземление на следующую крышу
- Добавить тестовый уровень для этого сценария

**T-4. RunFromRoof safety planning**

- При планировании JumpOnRoof проверять, что зона RunFromRoof (~1.9u) чиста от угроз
- Реализовать в проекции `BranchGenerator`: после JumpOnRoof добавить виртуальный шаг `RunFromRoof`
  и проверит его безопасность

### Задачи-цели (Target coverage)

**T-5. Добавить Target-based planning для smallAlive**
- Расширить `ProblemResolver` типом `ProblemKind.TargetProfit`
- Или обрабатывать Target прямо в ActionGenerator при наличии smallAlive на текущей линии
- Действия: JumpOnObstacle или JumpOver (в зависимости от дистанции и позиции)
- Добавить тестовый уровень

**T-6. Добавить Target-based planning для bigAlive (с крыши)**
- Хомяк на крыше → bigAlive на дороге как Target
- Добавить RoofJump-стратегию → JumpOnObstacleFromRoof

### Задачи-коллектиблы (Collectible collection)

**T-7. Сбор Collectible с другой дорожки**
- Расширить `ProblemResolver` типом `ProblemKind.CollectiblePickup`
- Или отдельный `CollectiblePlanner` поверх threat-плана
- SwitchLane к Collectible с учётом безопасности перестроения и возврата

**T-8. Приоритизация Collectible vs Threat**
- Зависит от T-7: выбирать SwitchLane к life-collectible при наличии угрозы с другой стороны (аналог старой логики LifeCollectible rank из BotV2-мемуаров)
