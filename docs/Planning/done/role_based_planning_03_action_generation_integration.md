# Module plan: action generation integration

## Назначение

Упростить action generation: один role-based decision point для текущего planning node, один проход по активным стратегиям, без required/optional split.

## Факты по текущему коду

- `ActionGenerator.Generate` сейчас вызывает `TryDetectRequiredDecisionPoint`, затем `DetectOptionalDecisionPoints`.
- Optional target поиск сейчас ограничен `!IsOnRoof`, `!IsShifting`, `Energy >= 40`.
- В новой модели `IsOnRoof` не блокирует focus lane scan; roof state только меняет набор huntable target'ов.
- Старый generator удаляет super jump-on candidates, если ordinary jump-on покрывает тот же target.
- Логи `NO_DECISION` и `NO_ACTIONS` сейчас завязаны на `DecisionPointKind` и первый obstacle.

## Целевая форма

Нужен role-based generator. Название класса выбрать по минимальному diff:

- `ActionGeneratorNew`, если старый path должен жить параллельно без риска;
- или refactor существующего `ActionGenerator` после полного перевода contracts.

Алгоритм:

1. Проверить входы.
2. Спроецировать world через `PlanningSnapshotProjector.Project`.
3. Вызвать `DecisionPointDetectorNew.TryDetect(...)` один раз.
4. Если point нет, вернуть пустой список.
5. Передать point всем активным role-based strategies.
6. Оставить duplicate filter для super jump-on после подключения соответствующих стратегий.
7. Логи переписать на focus lane, first obstacle, roles и chain bounds.

## Что переиспользовать

- `PlanningSnapshotProjector.Project`.
- `RemoveSuperJumpOnCandidatesCoveredByOrdinaryJumpOn` логику после подключения jump-on strategies.
- `PlannedAction` и `BotActionKind`.
- Существующие strategy order правила, но на первом этапе список содержит только SwitchLane.

## Что не делать

- Не строить optional decision points.
- Не хранить `FireBeforeObstacle`.
- Не выбирать focus lane в generator.
- Не создавать fallback/no-op actions.
- Не смешивать старые и role-based strategies в одном generator.

## Валидация будущей реализации

- Один planning node вызывает detector один раз.
- При отсутствии point возвращается пустой actions list.
- При наличии point каждая активная role-based strategy вызывается ровно один раз.
- SwitchLane-only этап возвращает только `BotActionKind.SwitchLane`.
