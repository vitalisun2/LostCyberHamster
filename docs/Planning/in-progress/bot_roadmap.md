# Bot Roadmap

Документ плана развития. Отвечает на вопрос: в каком порядке развивать бота, какие архитектурные задачи делать когда, и какие test levels нужны на каждом этапе.

Связанный документ со статусом на сейчас: `bot_coverage.md`.

## Принцип организации

- `bot_coverage.md` фиксирует текущее состояние;
- `bot_roadmap.md` фиксирует последовательность следующих шагов;
- roadmap включает и feature coverage, и архитектурные задачи, но только как план работ, а не как статусную матрицу.

## Базовые договорённости

- бот остаётся детерминированным planner'ом, без вероятностных fallback-политик;
- основная модель пока threat-centric, без полного redesign;
- новые стратегии добавляются только после того, как нужный supporting layer готов;
- planner horizon сейчас сознательно шире камеры: `screen + 0.5 screen` вправо;
- поведение на крыше рассматривается как единый блок развития, а не как набор разрозненных фич.

## Этапы

### Phase 1. Road Threat Coverage

Цель: закрыть дорожные threat-cases без перехода на крышу как основной режим.

Входит:

- `JumpOnRoof` для `bigNotAlive` и `mediumNotAlive`;
- `SuperJumpOnRoof` для `bigNotAlive` и `mediumNotAlive`;
- test levels под forced roof-entry cases;
- проверка, что новые стратегии не ломают `test_switch_lane` и `test_jump_over`.

Критерий выхода:

- дорожные `bigNotAlive` и `mediumNotAlive` имеют не только `SwitchLane`, но и roof-альтернативы;
- planner стабильно выбирает корректный road-safe путь в блокирующих конфигурациях.

### Phase 2. Roof Coverage

Цель: оформить крышу как полноценный planner-domain, а не как набор частных исключений.

Входит:

- поведение хомяка на крыше и уточнение runtime mechanics;
- `RoofJumpOver` для roof small obstacle avoidance;
- roof target interactions: простой и super jump на target с крыши;
- переходы между крышами на одной линии;
- переходы между крышами на другой линии;
- roof switch lane;
- roof jump to roof / roof super jump to roof;
- safe descent logic: спуск с крыши, escape с крыши, переключение на дорожку;
- уточнение, какие roof interactions уже автоматом поддерживаются runtime, а что planner должен делать явно.

Критерий выхода:

- для roof-case есть формализованный набор planner actions;
- есть понятная карта supported roof transitions;
- bot умеет не только попадать на крышу, но и безопасно жить в roof-space.

### Phase 3. Targets

Цель: научить planner видеть не только угрозы, но и выгодные destructible targets.

Входит:

- расширение верхнего resolver, чтобы `Target` стал допустимой planner-задачей;
- включение target strategies для дороги;
- включение target strategies для крыши;
- правила приоритета: threat first, target opportunistically.

Критерий выхода:

- planner умеет выбрать target-case, когда это не ломает survival;
- road и roof target interactions проходят отдельные regression tests.

### Phase 4. Collectibles And Reward

Цель: научить planner различать более и менее выгодные safe branches.

Входит:

- branch reward model;
- учёт collectibles как эффекта траектории, а не как отдельного шага;
- rules for `Collectible vs Threat` under equal safety;
- test levels на выгоду ветки.

Критерий выхода:

- при равной safety бот предпочитает более выгодную ветку;
- reward model не ломает obstacle handling.

### Phase 5. Timing And Planner Quality

Цель: улучшить качество выбора без смены общей архитектурной модели.

Входит:

- перенос safe-window policy на другие action families, если это даст выигрыш;
- точечные улучшения evaluator / branch comparison;
- тесты на delayed decision и optimal safe timing.

Критерий выхода:

- planner умеет выбирать не только earliest safe window, но и лучший допустимый deterministic window;
- исчезают сценарии вида "безопасно, но тупо по таймингу".

## Архитектурные workstreams

### Done

- inter-replan `avoidance commitment` memory;
- перенос commitments через branch projection;
- delayed return на committed lane.
- расширенный planner horizon (`camera + 0.5 screen` вправо);
- deterministic `mid-safe` window selection для `SwitchLane`.

### Planned

| Workstream | Когда нужен |
|---|---|
| Roof domain formalization | `Phase 2` |
| `Target`-aware resolver | `Phase 3` |
| Branch reward model | `Phase 4` |
| Timing policy expansion beyond `SwitchLane` | `Phase 5` |

## Test Strategy

### Уже есть

- `test_switch_lane`
- `test_jump_over`

### Нужно добавить

#### Под Phase 1

- forced `bigNotAlive -> JumpOnRoof`
- forced `mediumNotAlive -> JumpOnRoof`
- forced `bigNotAlive -> SuperJumpOnRoof`
- forced `mediumNotAlive -> SuperJumpOnRoof`

#### Под Phase 2

- roof small obstacle avoidance
- roof target hit
- roof-to-roof same-lane transition
- roof-to-roof cross-lane transition
- roof escape / safe descent

#### Под Phase 3-4

- target preferred over idle safe path
- collectible branch reward under equal safety
- threat dominates reward when branch becomes unsafe

#### Под Phase 5

- multiple safe switch windows
- later window strictly better than earliest window

## Открытые уточнения по roof runtime

Нужно явно проверить в runtime, что из этого уже делается автоматически, а что planner обязан инициировать сам:

- бег по крыше после попадания на крышу;
- автоматический спуск с крыши;
- escape с крыши на другую линию;
- переход между крышами на одной линии;
- переход между крышами между линиями;
- super jump variants на крыше.

До этой проверки roof-phase лучше не дробить на слишком мелкие подпункты реализации.

## Следующий конкретный шаг

Следующий рабочий блок: `Phase 1`, то есть `JumpOnRoof` и `SuperJumpOnRoof` для дорожных `bigNotAlive` и `mediumNotAlive`, вместе с новыми test levels под forced roof-entry scenarios.
