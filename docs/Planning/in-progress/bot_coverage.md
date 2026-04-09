# Bot Coverage

Документ текущего состояния. Отвечает на вопрос: что бот уже умеет сейчас, что включено в runtime, что ограничено, и какие test levels уже подтверждены.

Связанный документ с порядком развития: `bot_roadmap.md`.

## Текущий pipeline

```text
BotOrchestrator (event-driven)
  -> SnapshotBuilder
  -> BranchSelector
    -> ProblemResolver + ObjectClassifier
    -> ActionGenerator
    -> BranchGenerator (lookahead до 5 шагов)
    -> BranchEvaluator
  -> StepExecutor
```

### Что важно про runtime сейчас

- replanning запускается по `OnNewObjectAppeared` и когда committed plan исчерпан;
- completed step продвигает head, но не делает самостоятельный replan;
- planner horizon = текущая камера + дополнительная половина экрана вправо;
- `SwitchLane` выбирает midpoint последнего safe-window, и safety считается по всему интервалу transition;
- после последних правок работает межплановая память `avoidance commitment` для `SwitchLane`.

## Активные стратегии

Сейчас включены и валидируются только дорожные стратегии:

| Стратегия | Статус | Примечание |
|---|---|---|
| `SwitchLaneStrategy` | done | активна |
| `JumpOverStrategy` | done | активна |
| `SuperJumpOverStrategy` | done | активна для road `bigAlive` |

Остальные strategy classes существуют в коде, но пока не включены в `ActionGenerator`.

## Покрытие по классам ситуаций

### Дорога

| Ситуация | Текущее решение | Статус |
|---|---|---|
| `smallNotAliveRoad` | `SwitchLane`, `JumpOver` | done |
| `smallNotAliveRoadAndRoof` на дороге | `SwitchLane`, `JumpOver` | done |
| `bigAlive` на дороге | `SwitchLane`, `SuperJump` | done |
| `bigNotAlive` на дороге | `SwitchLane` | partial |
| `mediumNotAlive` на дороге | `SwitchLane` | partial |
| `smallAlive` на дороге | planner пока не работает с target-case | todo |

### Крыша

| Ситуация | Текущее решение | Статус |
|---|---|---|
| Бег по крыше и roof obstacles | roof strategies в коде есть, но не включены | todo |
| Переход с дороги на крышу | runtime mechanic есть, planner strategy block ещё не включён | todo |
| Переход между крышами | требует отдельной roof-phase в planner | todo |
| Спуск с крыши и безопасный escape | поведение частично есть в runtime, planner coverage нужно уточнить и формализовать | todo |
| Target interactions на крыше | planner пока не поддерживает roof target-case | todo |

### Targets и Collectibles

| Класс | Текущее состояние | Статус |
|---|---|---|
| `Target` как planner-задача | верхний resolver ещё не поднимает `Target` как objective | todo |
| Collectible на текущей линии | подбирается механикой игры без участия planner | done |
| Collectible на другой линии | planner reward/utility пока не считает | todo |
| Приоритизация `Collectible vs Threat` | reward model отсутствует | todo |

## Поддерживающие архитектурные слои

| Слой | Статус | Примечание |
|---|---|---|
| Расширенный planner horizon вправо | done | бот видит на половину экрана дальше правого края камеры |
| `avoidance commitment` после `SwitchLane` | done | предотвращает немедленный возврат под уже avoided threat |
| Перенос commitments через replans | done | память живёт в runtime |
| Перенос commitments через planner projection | done | lookahead видит те же ограничения |
| Delayed return на committed lane | done | `SwitchLane` умеет ждать release moment |
| `SwitchLane` safe-window selection | done | выбирается midpoint последнего safe-window по единой геометрии transition |
| Timing-window extension beyond `SwitchLane` | todo | другие action families пока используют более простую timing policy |
| Reward model для ветки | todo | `BranchOutcome` пока не учитывает бонусы |

## Тестовые уровни

| Уровень | Адрес | Что проверяет | Последний статус |
|---|---|---|---|
| `test_switch_lane` | `01_New_York/Morning/test_switch_lane` | дорожные avoidance-case, включая `bigAlive` | WIN |
| `test_jump_over` | `01_New_York/Morning/test_jump_over` | `JumpOver` через small obstacle | WIN |
| `test_superjump_over` | `01_New_York/Morning/test_superjump_over` | forced road `SuperJump` против `bigAlive` при опасном нижнем маршруте | WIN |

Примечание по качеству regression gates:

- `test_switch_lane` и `test_superjump_over` сейчас совпадают с intent уровня по логу;
- `test_jump_over` технически проходит, но уже не является чистым forced-`JumpOver` gate: в позднем фрагменте уровень допускает альтернативный `SuperJump` по `bigAlive`, поэтому его стоит ужесточить или разделить на более изолированные сценарии.

## Ограничения текущего состояния

- planner по-прежнему threat-centric: ближайшая same-lane угроза остаётся основной planner-задачей;
- roof coverage не включён;
- `Target` и `Collectible` пока не участвуют в выборе ветки как самостоятельные planner-objectives;
- reward model отсутствует;
- global optimization по уровню не планируется на текущем этапе.

## Что этот документ не делает

Этот документ не задаёт порядок работ. Порядок этапов, архитектурные фазы и planned test coverage теперь живут в `bot_roadmap.md`.
