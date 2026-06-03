# Role-based Decision Point Model

## Цель

Упростить planning-архитектуру бота: убрать сценарные target-specific chain builders и оставить одну универсальную модель decision point, где препятствия описаны ролями, а ветки строятся из actions стратегий.

Дополнительная цель refactor'а - сделать систему проще и поддерживаемее: меньше специальных сущностей, меньше дублирования ответственности, больше ясного разделения detector / strategy / graph / evaluator / executor.

## Текущая проблема

Сейчас `DecisionPointKind` заранее выбирает сценарий всей ситуации: `BlockingThreat`, `GroundJumpOnTarget`, `JumpOnFromRoofTarget`, `RoofJumpOnTarget`.

Из-за этого часть логики охоты за target живёт в builders/composers, хотя нижний уровень уже умеет:

- генерировать safe actions стратегиями;
- симулировать результат каждого action;
- строить дерево веток;
- выбирать лучшую ветку через evaluator;
- исполнять выбранный план по head-action.

## Предлагаемая модель

`DecisionPointDetector` строит один `DecisionPoint` для текущего planning node, внутри которого лежит универсальный `ObstacleChain` выбранной focus lane.

`ObstacleChain` содержит элементы препятствий. Каждый элемент знает:

- obstacle snapshot;
- world index;
- lane;
- роли obstacle.

Пример ролей:

- `BlockingThreat` - препятствие опасно и требует реакции, если находится на релевантной линии.
- `RoofSupport` - на obstacle можно запрыгнуть и бежать по крыше.
- `Target` - obstacle может быть выгодной целью для jump-on.
- `RoofOccupantHazard` - опасный occupant на roof path.
- `Collectible` - будущая отдельная роль для бонусов.

Один obstacle может иметь несколько ролей. Например:

- `bigNotAlive`: `BlockingThreat + RoofSupport`.
- `smallAlive`: `BlockingThreat + Target`.
- `smallNotAliveRoad`: `BlockingThreat`.

## Построение chain

Универсальный `ObstacleChainBuilder` берёт отсортированный snapshot и собирает relevant chain/window.

Базовые правила:

- obstacles уже сортируются по `LeftX` в snapshot;
- detector сначала выбирает focus lane;
- chain содержит obstacles только focus lane;
- obstacles другой линии участвуют только в target scan для выбора focus lane;
- плотные obstacles одной линии входят в один chain;
- если gap между obstacles меньше ширины хомяка, это один chain;
- если gap больше или равен ширине хомяка, следующий obstacle можно обрабатывать отдельным future decision;
- для passive roof continuation может сохраняться отдельное roof-specific правило gap.

После action, например `SwitchLane`, graph симулирует новый `PlanningState`; следующий `DecisionPoint` строится заново уже для новой ситуации и новой lane.

## Генерация actions

`ActionGenerator` передаёт один role-based `DecisionPoint` всем стратегиям.

Стратегии сами фильтруют элементы chain по lane и role:

- `SwitchLaneStrategy` ищет `BlockingThreat` на текущей линии и safe окно ухода на другую линию.
- `JumpOverStrategy` ищет `BlockingThreat`, который можно перепрыгнуть.
- `JumpOnRoofStrategy` ищет `RoofSupport`, на который можно безопасно запрыгнуть.
- `JumpOnStrategy` ищет ground `Target`.
- `JumpOnFromRoofStrategy` ищет `Target`, достижимый с roof state.
- `PassiveRoofExitStrategy` моделирует безопасный нулевой переход `RoofRun -> Run`, когда это нужно для продолжения ветки.

Стратегия добавляет `PlannedAction` только если:

- action применим к текущему planning state;
- найдено safe fire-window;
- runtime resolver подтверждает ожидаемый результат;
- post-action safety не приводит к damage.

Unsafe actions не попадают в дерево.

## Построение и выбор ветки

`PlanningGraphBuilder` остаётся главным механизмом построения веток:

1. Получает actions из `ActionGenerator`.
2. Для каждого action симулирует следующий `PlanningState`.
3. Рекурсивно строит продолжение ветки.
4. Закрывает leaf, если в текущем projected snapshot нет unresolved planning situation.
5. Передаёт готовые ветки в `PlanEvaluator`.

`PlanEvaluator` выбирает лучшую safe ветку по приоритетам:

- jump-on objective;
- energy cost;
- tap count;
- timing/projection tie-breakers.

`PlanExecutor` исполняет выбранный `BotPlan` по одному head-action. Если план пустой, бот просто продолжает бежать до следующего snapshot с релевантными obstacles.

## Пример: `bigNotAlive -> smallAlive -> smallNotAliveRoad`

Исходная ситуация:

- нижняя линия:
  - `bigNotAlive`;
  - после него `smallAlive`;
  - после `smallAlive` стоит `smallNotAliveRoad`;
- верхняя линия пустая.

### Role scan

Chain elements:

- `bigNotAlive`: `BlockingThreat + RoofSupport`.
- `smallAlive`: `BlockingThreat + Target`.
- `smallNotAliveRoad`: `BlockingThreat`.

### Узел 1: перед `bigNotAlive`

Стратегии генерируют actions:

- `SwitchLane` наверх.
  - Проходит, если upper lane safe.
- `JumpOnRoof(bigNotAlive)`.
  - Проходит, если fire-window и roof landing safe.

Tree branches:

- `SwitchLane`
- `JumpOnRoof(bigNotAlive)`

### Ветка A: `SwitchLane`

Симуляция:

- хомяк на верхней линии;
- верхняя линия пустая;
- unresolved planning situation нет.

Ветка закрывается как leaf:

```text
SwitchLane
```

Стоимость:

- energy: 0;
- taps: 1.

### Ветка B: `JumpOnRoof(bigNotAlive)`

Симуляция:

- хомяк переходит в `RoofRun`;
- energy уменьшается на стоимость `JumpOnRoof`.

Для цельности state transition добавляется/симулируется:

```text
PassiveRoofExit
```

Это нулевое действие ожидания естественного схода с крыши. Оно нужно не как input, а как planning-step, чтобы получить state `Run` после roof path.

После `PassiveRoofExit` хомяк снова в `Run` на нижней линии.

### Узел 2: перед `smallAlive`

Стратегии генерируют actions:

- `SwitchLane`.
  - Проходит, если верхняя линия safe.
- `JumpOver(smallAlive)`.
  - Проходит, если jump-over window safe.
- `JumpOn(smallAlive)`.
  - Fire-window и runtime resolver могут подтвердить попадание в target.
  - Но post-action safety проверяет конец всей анимации и re-entry после удаления target.
  - Если после отскока будет пересечение с `smallNotAliveRoad`, action отсекается.

Итоговые safe branches:

```text
SwitchLane
JumpOnRoof(bigNotAlive) -> PassiveRoofExit -> SwitchLane
JumpOnRoof(bigNotAlive) -> PassiveRoofExit -> JumpOver(smallAlive)
```

Ветка с `JumpOn(smallAlive)` не создаётся, потому что action не проходит post-action safety.

### Выбор

Evaluator сравнивает только safe ветки.

Ожидаемый выбор:

```text
SwitchLane
```

Причина:

- безопасно;
- дешевле по энергии;
- меньше лишних действий;
- target-ветка отсутствует, потому что `JumpOn(smallAlive)` unsafe.

## Вывод

В этой модели target-specific builders не нужны. Они заменяются:

- универсальным chain builder;
- role scan для obstacles;
- стратегиями, которые сами порождают safe actions;
- существующим tree/evaluator механизмом выбора лучшей ветки.

Так planning остаётся веточным, но семантика obstacle не размазывается по набору сценарных builders.
