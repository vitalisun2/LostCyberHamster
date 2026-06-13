# JumpOn -> JumpOver plan drop analysis — 2026-06-13

## Scope

Регресс на `01_New_York/Morning/level_01`: после `JumpOn` по `smallAlive` бот возвращается в `Run`, не исполняет следующий `JumpOver` и теряет жизнь на `smallAlive`.

## Источники

- `LostCyberHamster/EditorLogs/diagnostic_log.txt`, прогон 2026-06-13 17:14.
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanningGraphBuilder.cs`.
- `LostCyberHamster/Assets/Scripts/Bot/Planning/PlanBuilder.cs`.
- `LostCyberHamster/Assets/Scripts/Bot/RuntimeBotController.cs`.
- `LostCyberHamster/Assets/Scripts/Bot/Execution/PlanExecutor.cs`.

## Проверенные гипотезы

### JumpOverStrategy не создаёт action после последнего JumpOn

Статус: опровергнута.

Временная точечная диагностика `PlanningGraphBuilder` показала:

```text
[17:14:55.290] [Bot TRACE] FIRST_DEAD_END depth=4 path=JumpOver -> SwitchLane -> JumpOver -> SwitchLane nextObstacleIndex=5 projection=18.37
```

Значит ветка после последнего `JumpOn` начинается с `JumpOver`; стратегия применялась и action был создан. Временная диагностика удалена после прогона.

### План не устанавливается, потому что нет полной leaf-ветки

Статус: подтверждена.

`PlanningGraphBuilder.ExploreNode()` добавляет в `branches` только leaf-узлы:

- если достигнут `MaxSearchDepth` и нет unresolved situation;
- если candidates отсутствуют и unresolved situation отсутствует;
- если unresolved situation отсутствует при наличии candidates.

Если кандидаты есть, но все пути позже приходят в dead-end, leaf-ветка не добавляется. В этом кейсе первый dead-end найден на глубине 4, путь до него: `JumpOver -> SwitchLane -> JumpOver -> SwitchLane`.

`PlanBuilder.Build()` выбирает только `graphResult.Branches`. Если `branches` пустой, он возвращает `BotPlan.Empty(...)` вместе с `DeadEndReport`.

## Корень проблемы

Planner построил локально правильный первый шаг `JumpOver` после `JumpOn`, но не вернул его в исполняемый план, потому что вся ветка дальше упирается в dead-end на глубине 4.

Текущая архитектура all-or-nothing: исполняемым становится только полный успешный leaf-branch. Частично безопасный prefix ветки, ведущей к более позднему dead-end, отбрасывается целиком.

Из-за этого после завершения последнего `JumpOn`:

1. `PlanExecutor.AdvanceHead()` очищает план, потому что `JumpOn` был последним action.
2. `RuntimeBotController` запрашивает replan по `ActionCompleted`.
3. `PlanBuilder` видит путь `JumpOver -> ...`, но не имеет ни одной полной leaf-ветки и возвращает пустой план.
4. Executor не получает `JumpOver`.
5. Хомяк остаётся в `Run` и через ~0.21с получает damage.

## Почему dead-end причины не содержат JumpOverStrategy

`PlanningDeadEndReport` хранит причины только первого узла, где `candidates.Count == 0`. В текущем кейсе это узел глубины 4, а не ближайшая ситуация после `JumpOn`.

Поэтому отсутствие `JumpOverStrategy` в `[Bot DEAD_END] causes` не означает, что `JumpOver` не строился. Оно означает только, что финальный провал найден позже, после нескольких смоделированных действий.

## Реализованное решение

Реализован первый вариант: поддержка dead-end branches как fallback, когда успешных leaf-веток нет.

Изменения:

- `PlanningGraphBuilder` собирает успешные `branches` и отдельные `deadEndBranches`.
- Каждая dead-end branch хранит safe-prefix и свой `PlanningDeadEndReport`.
- `PlanBuilder` сначала выбирает обычную успешную ветку.
- Если успешных веток нет, `PlanBuilder` выбирает dead-end branch fallback.
- `PlanEvaluator.SelectBestDeadEnd()` предпочитает максимальное продвижение: `FinalNextObstacleIndex`, затем `FinalProjectionWorldShift`, затем `ActionCount`.

## Проверка после реализации

`dotnet build LostCyberHamster/Assembly-CSharp.csproj --no-restore`:

- результат: успешно;
- ошибок: 0;
- warnings: существующие проектные warnings вне scope задачи.

Прогон `01_New_York/Morning/level_01` после реализации:

```text
[17:36:05.130] [Bot PLAN] JumpOn -> JumpOver -> SwitchLane -> JumpOver -> SuperJumpOver -> SwitchLane
[17:36:08.244] [Bot PLAN] JumpOver -> SwitchLane -> JumpOver -> JumpOver -> SuperJumpOver -> SwitchLane -> SwitchLane
...
[17:37:06.408] [CollisionController] damage ... obstacle=bigAlive ...
[17:37:06.411] [Bot DEAD_END] confirmed=true reason=ActionCompleted ... depth=0
```

Итог: прежняя ранняя смерть на `smallAlive` после `JumpOn` ушла. Бот выполняет safe-prefix dead-end веток и подтверждает более поздний dead-end на `bigAlive`.
