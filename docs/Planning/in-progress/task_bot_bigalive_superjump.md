# T-1: SuperJump для bigAlive (forced)

## Описание задачи

Научить бота использовать `SuperJump` как запасной вариант, когда он сталкивается с угрозой
типа `bigAlive` (прохожий/противник), а уйти на другую дорожку (`SwitchLane`) невозможно.

Обычный `Jump` над `bigAlive` физически невозможен — всегда даёт `JumpDamageForBigAlive`.
Единственная альтернатива SwitchLane — `SuperJump` → `SuperJumpOver`.

**Сценарий:** хомяк на дорожке X, впереди `bigAlive` на той же дорожке, соседняя дорожка
заблокирована другим препятствием (`bigNotAlive` или иным) → бот выбирает `SuperJump`.

---

## Что нужно изменить в коде

### 1. `BotAction.cs` — добавить значение SuperJump

```
Assets/Scripts/Bot/Models/BotAction.cs
```

Добавить `SuperJump` в enum после `Jump`.

### 2. `SuperJumpStrategy.cs` — новая стратегия

```
Assets/Scripts/Bot/Planning/Strategies/SuperJumpStrategy.cs
```

По образцу `JumpStrategy`. Отличия:
- `CanApply(type)` → только `bigAlive` на дороге (`!HamsterOnRoof`)
- Действие: `BotAction.SuperJump`
- Проекция (`ApplyEffects`): `HamsterOnRoof=false`, `HamsterOnBottom` без изменений,
  позиция хомяка перемещается за препятствие — аналогично `JumpStrategy.ApplyJumpEffects`
  для случая Over
- Стоимость: 20 единиц энергии (`SuperJumpEnergyCost` из `BotConsts`)
- Пороговые расстояния огня (`FireDist`, `FireDelay`): взять из `BotConsts`
  или определить аналогично прыжку с поправкой на дистанцию SuperJump

### 3. `SuperJumpHandler.cs` — новый хэндлер

```
Assets/Scripts/Bot/Execution/Handlers/SuperJumpHandler.cs
```

По образцу `JumpHandler`. Отличия:
- При `BotAction.SuperJump`: стреляет `SuperJumpRequest` (когда `!HamsterOnRoof`)
- Не использует крышный путь (SuperRoofJump не нужен в рамках этой задачи)

### 4. `StepExecutor.cs` — регистрация хэндлера

```
Assets/Scripts/Bot/Execution/StepExecutor.cs
```

Зарегистрировать `SuperJumpHandler` для `BotAction.SuperJump` аналогично
тому, как зарегистрирован `JumpHandler` для `BotAction.Jump`.

### 5. `ActionGenerator.cs` — регистрация стратегии

```
Assets/Scripts/Bot/Planning/ActionGenerator.cs
```

Зарегистрировать `SuperJumpStrategy` аналогично `JumpStrategy`.
Стратегия должна генерировать шаг `SuperJump` только при условии, что:
- Текущий тип угрозы = `bigAlive`
- Хомяк не на крыше
- Энергии достаточно (≥ SuperJumpEnergyCost)

---

## Тестовый уровень

**Имя уровня:** `test_threat_bigalive_superjump`  
**Адрес:** `01_New_York/Morning/test_threat_bigalive_superjump`

### Паттерны

После реализации кода добавить в `PatternsCollection.json` следующие паттерны
и собрать новый тестовый уровень из них.

#### Паттерн 01 — forced SuperJump from bottom

**Имя:** `test_threat_bigalive_superjump_01_forced_bottom`  
**Описание:** `bigAlive` на нижней дорожке, верхняя дорожка заблокирована `bigNotAlive` → бот не может
уйти вбок, вынужден использовать `SuperJump`. 3 повторения.

```json
{
    "name": "test_threat_bigalive_superjump_01_forced_bottom",
    "description": "bigAlive: forced SuperJump from bottom — top lane blocked by bigNotAlive, 3 reps",
    "obstacles": [
        { "spriteName": "obstacle_new_york_businessman", "type": 1, "x": 10.0, "y": -2.8 },
        { "spriteName": "obstacle_new_york_car_1",       "type": 4, "x":  8.0, "y": -1.8 },
        { "spriteName": "obstacle_new_york_businessman", "type": 1, "x": 22.0, "y": -2.8 },
        { "spriteName": "obstacle_new_york_car_1",       "type": 4, "x": 20.0, "y": -1.8 },
        { "spriteName": "obstacle_new_york_businessman", "type": 1, "x": 34.0, "y": -2.8 },
        { "spriteName": "obstacle_new_york_car_1",       "type": 4, "x": 32.0, "y": -1.8 }
    ]
}
```

Что проверяется: бот 3 раза подряд выбирает `SuperJump` (не `SwitchLane`, не `None`).

---

#### Паттерн 02 — forced SuperJump from top

**Имя:** `test_threat_bigalive_superjump_02_forced_top`  
**Описание:** `bigAlive` на верхней дорожке, нижняя заблокирована `bigNotAlive`. 3 повторения.

```json
{
    "name": "test_threat_bigalive_superjump_02_forced_top",
    "description": "bigAlive: forced SuperJump from top — bottom lane blocked by bigNotAlive, 3 reps",
    "obstacles": [
        { "spriteName": "obstacle_new_york_businessman", "type": 1, "x": 10.0, "y": -1.8 },
        { "spriteName": "obstacle_new_york_car_1",       "type": 4, "x":  8.0, "y": -2.8 },
        { "spriteName": "obstacle_new_york_businessman", "type": 1, "x": 22.0, "y": -1.8 },
        { "spriteName": "obstacle_new_york_car_1",       "type": 4, "x": 20.0, "y": -2.8 },
        { "spriteName": "obstacle_new_york_businessman", "type": 1, "x": 34.0, "y": -1.8 },
        { "spriteName": "obstacle_new_york_car_1",       "type": 4, "x": 32.0, "y": -2.8 }
    ]
}
```

Что проверяется: то же, но стартовая дорожка хомяка — верхняя.

---

#### Паттерн 03 (edge case) — SuperJump, затем немедленная угроза

**Имя:** `test_threat_bigalive_superjump_03_superjump_then_jump`  
**Описание:** После приземления с `SuperJump` сразу следует `smallNotAliveRoad` на той же
дорожке → проверяет цепочку `SuperJump + Jump` (двухшаговое планирование).

```json
{
    "name": "test_threat_bigalive_superjump_03_superjump_then_jump",
    "description": "bigAlive: forced SuperJump + immediate smallNotAliveRoad after landing — tests SuperJump+Jump chain",
    "obstacles": [
        { "spriteName": "obstacle_new_york_businessman", "type": 1, "x": 10.0, "y": -2.8 },
        { "spriteName": "obstacle_new_york_car_1",       "type": 4, "x":  8.0, "y": -1.8 },
        { "spriteName": "obstacle_new_york_manhole",     "type": 2, "x": 16.0, "y": -2.8 }
    ]
}
```

Что проверяется: бот строит цепочку из двух шагов: `SuperJump` над `bigAlive`, затем `Jump`
через `smallNotAliveRoad` в зоне приземления. Если цепочка невалидна — бот должен
выбрать альтернативу (`SwitchLane` перед первым препятствием, если возможно).

---

## После реализации

1. Добавить уровень в `invoke_run_all_test_levels.ps1` и в таблицу `docs/rules/workflow.md`.
2. Добавить `TestLevelLauncher` entry (`MenuItem`) в `Editor/TestLevelLauncher.cs`.
3. Обновить таблицу покрытия в `docs/Planning/in-progress/bot_current_state.md`.
4. Обновить статус T-1 в `docs/Planning/in-progress/obstacle_interaction_coverage.md`.
