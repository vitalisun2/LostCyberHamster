# BotV3 Problem-Driven Planning

## Контекст

Тайминговый рефакторинг уже сделал `ProjectedWorld` и action-specific стратегии, но в planner всё ещё оставалась смысловая дыра:

- шаги порождаются от `nearest same-lane Threat`,
- а не от явной текущей проблемы / decision point.

Из-за этого в дерево попадали safe-кандидаты без достаточной причинно-следственной связи. Симптомы:

- лишние `SwitchLane`, которые просто переносят проблему;
- выбор `Jump` как побочный эффект symptom-based scorer, а не как лучший ответ на проблему.

## Что хотим получить

Для threat-only этапа planner должен работать так:

1. По snapshot найти **следующую обязательную проблему**.
2. Сгенерировать только те шаги, которые решают именно её.
3. После проекции шага найти уже **следующую** проблему в новом состоянии.
4. Сравнивать ветки как альтернативные способы решения последовательности проблем.

Ключевая идея:

- `Threat` — это категория объекта.
- `Problem` — это конкретный `Threat`, который при `run baseline` на текущей линии приводит к collision.

## Целевая архитектура

### 1. `ProblemResolver`

Новый слой между perception и action generation.

Отвечает на вопрос:

- какая проблема является текущим `decision point`;
- в какой `worldShift` без ответа возникнет collision.

На текущем этапе достаточно:

- `ProblemKind.ThreatCollision`
- `ProblemDescriptor(SourceObstacle, DecisionWorldShift, Reason)`

### 2. `ActionGenerator`

Работает не от списка visible objects, а от явной проблемы:

- `Generate(snapshot, problem)`

Он больше не решает сам, какой obstacle достоин генерации шагов.

### 3. `IActionStrategy`

Стратегия должна отвечать не на вопрос:

- "могу ли я построить safe step вокруг obstacle?"

а на вопрос:

- "могу ли я решить этот problem?"

### 4. `BranchGenerator`

В каждом узле дерева делает:

- projection
- classify
- `resolve next problem`
- `generate solutions for that problem`

Если проблемы нет — ветка заканчивается.

### 5. `BranchEvaluator`

После возврата причинно-следственной связи scorer снова должен оставаться простым:

- safety
- total energy
- branch depth
- fire timing as tie-break

Без symptom-based эвристик вроде искусственного `wait now`.

## План реализации

1. Ввести `ProblemResolver` и `ProblemDescriptor`.
2. Перевести `IActionStrategy` на `problem -> step`.
3. Перевести `ActionGenerator` на `Generate(snapshot, problem)`.
4. Перевести `BranchSelector` и `BranchGenerator` на `resolve problem -> generate solutions`.
5. Удалить symptom-based `ShouldWaitNow`.
6. Вернуть scorer к общему правилу `safe -> energy -> depth -> fire`.
7. Обновить edit mode tests под новую семантику.
8. Прогнать compile + test level automation.
9. Прочитать `STAB/BOT/ECO` и сверить, что planner снова предпочитает причинно оправданный `SwitchLane`, а не лишний `Jump`.

## Статус

Выполнено:

- `ProblemResolver` и `ProblemDescriptor` добавлены.
- Planner переведён на `problem -> solutions`.
- `ShouldWaitNow` удалён.
- `BranchEvaluator` возвращён к `safe -> energy -> depth -> fire`.
- `SwitchLaneStrategy` расширен с одного safe-window до нескольких окон, чтобы planner видел поздний допустимый `SwitchLane`, а не только ранний zigzag.
- Автопрогон test level прошёл (`WIN level=3 stars=3`).
- Собран coverage `test_level` из 20 `v3_cov_*` паттернов для `SwitchLane` / `Jump` / `NoOp` edge-cases.
- Coverage-автопрогон на 20 паттернах прошёл без урона (`WIN level=3 stars=3`).

Следующий шаг:

- визуально проверить новый coverage-level: что на `NoOp` бот реально не дёргается, а на jump-only и split-window кейсах выбирает ожидаемое действие без лишних манёвров.

## Принципы

- Не наказывать узкий кейс "два switch подряд".
- Не добавлять отдельный хакающий baseline-filter вне planner'а.
- Держать причину шага на этапе **порождения**, а не лечить симптом на этапе **scoring**.
- Разделять:
  - `почему шаг появился`
  - `когда внутри окна fire его лучше выполнить`
